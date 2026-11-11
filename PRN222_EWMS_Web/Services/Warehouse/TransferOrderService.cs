using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Transfer;

namespace PRN222_EWMS_Web.Services.Warehouse;

public class TransferOrderService(EwmsDbContext db, TransferStockService stock, TransferCapacityService capacity,
    TransferExpiryService expiry, TimeProvider clock, ILogger<TransferOrderService> logger) : ITransferOrderService
{
    public Task<List<ProductLookupDto>> GetAvailableProductsAsync(int warehouseId, string itemCondition = "GOOD", long? transferOrderId = null) =>
        stock.GetProductsAsync(warehouseId, itemCondition, transferOrderId);

    public async Task PopulateFormAsync(CreateTransferOrderViewModel model, long? orderId = null)
    {
        model.Warehouses = await db.Warehouses.AsNoTracking().Where(w => w.Status == "ACTIVE")
            .OrderBy(w => w.WarehouseName).Select(w => new LookupItemDto { Id = w.WarehouseId, Name = w.WarehouseName }).ToListAsync();
        model.Products = await stock.GetProductsAsync(model.FromWarehouseId, model.ItemCondition, orderId);
        foreach (var line in model.Lines) line.AvailableQuantity = model.Products.FirstOrDefault(p => p.ProductId == line.ProductId)?.UnreservedQuantity ?? 0;
    }

    public async Task<CreateTransferOrderViewModel> GetCreateFormAsync()
    {
        var model = new CreateTransferOrderViewModel { Lines = [new() { Quantity = 1 }] };
        await PopulateFormAsync(model);
        return model;
    }

    public async Task<EditTransferOrderViewModel?> GetEditFormAsync(long id, int userId)
    {
        await expiry.CancelIfExpiredAsync(id);
        var o = await db.TransferOrders.AsNoTracking().Include(t => t.TransferOrderLines).SingleOrDefaultAsync(t => t.TransferOrderId == id);
        if (o == null) return null;
        bool children = await TransferDataAccess.HasDocumentsAsync(db, id);
        bool editable = o.Status is "DRAFT" or "RELEASED" && !children;
        var model = new EditTransferOrderViewModel
        {
            TransferOrderId = id, TransferCode = o.TransferCode, Status = o.Status, RowVersion = o.RowVersion,
            RowVersionBase64 = Convert.ToBase64String(o.RowVersion), FromWarehouseId = o.FromWarehouseId, ToWarehouseId = o.ToWarehouseId,
            ItemCondition = o.ItemCondition, DispatchDate = o.DispatchDate, ExpectedDate = o.ExpectedDate, Note = o.Note,
            IsRelease = o.Status == "RELEASED", CanEdit = editable, CanCancel = editable,
            LockReason = children ? "Lệnh đã có phiếu gửi/nhận nên đã khóa toàn bộ chỉnh sửa. Muốn thêm hàng, hãy tạo lệnh khác."
                : editable ? null : o.CloseReason ?? $"Lệnh ở trạng thái {o.Status}, không thể chỉnh sửa.",
            Lines = o.TransferOrderLines.Where(l => !editable || l.RequestedQuantity > l.CancelledQuantity)
                .OrderBy(l => l.TransferLineId).Select(l => new CreateTransferOrderLineInput
                { TransferLineId = l.TransferLineId, ProductId = l.ProductId, Quantity = l.RequestedQuantity - (editable ? l.CancelledQuantity : 0) }).ToList()
        };
        await PopulateFormAsync(model, id);
        // Keep historical products visible in the locked form even after deactivation.
        var missingIds = model.Lines.Select(l => l.ProductId).Except(model.Products.Select(p => p.ProductId)).ToArray();
        model.Products.AddRange(await db.Products.AsNoTracking().Where(p => missingIds.Contains(p.ProductId))
            .Select(p => new ProductLookupDto { ProductId = p.ProductId, ProductCode = p.ProductCode, ProductName = p.ProductName, Unit = p.Unit }).ToListAsync());
        if (editable) model.Assessment = await AssessAsync(model, id, o.Status == "RELEASED");
        return model;
    }

    public async Task<TransferAssessment> AssessAsync(CreateTransferOrderViewModel model, long? orderId = null, bool forceRelease = false)
    {
        bool release = forceRelease || model.IsRelease;
        var (policyErrors, fieldErrors) = TransferPolicy.ValidateWithFields(model, TransferPolicy.Today(clock), release);
        var result = new TransferAssessment { Errors = policyErrors, FieldErrors = fieldErrors };
        var warehouses = await db.Warehouses.AsNoTracking().Where(w => w.WarehouseId == model.FromWarehouseId || w.WarehouseId == model.ToWarehouseId).ToListAsync();
        if (warehouses.Count != 2 || warehouses.Any(w => w.Status != "ACTIVE"))
        {
            const string whMsg = "Kho nguồn và kho đích phải tồn tại và đang hoạt động.";
            result.Errors.Add(whMsg);
            result.FieldErrors.TryAdd(nameof(model.ToWarehouseId), whMsg);
        }
        var destination = warehouses.FirstOrDefault(w => w.WarehouseId == model.ToWarehouseId);
        if (destination?.WarehouseType == "RENTED")
        {
            string? rentalError = destination.RentalStartDate == null || destination.RentalEndDate == null ? "Kho đích chưa có kỳ thuê hợp lệ."
                : model.ExpectedDate is DateOnly eta && (eta < destination.RentalStartDate || eta > destination.RentalEndDate)
                    ? "Ngày dự kiến đến phải nằm trong kỳ thuê của kho đích." : null;
            if (rentalError != null)
            {
                if (release)
                {
                    result.Errors.Add(rentalError);
                    result.FieldErrors.TryAdd(nameof(model.ExpectedDate), rentalError);
                }
                else
                {
                    result.Warnings.Add(rentalError);
                }
            }
        }
        if (result.Errors.Count > 0) return result;
        var ids = model.Lines.Select(l => l.ProductId).ToArray();
        var validProducts = await db.Products.AsNoTracking().Where(p => ids.Contains(p.ProductId) && p.Status == "ACTIVE")
            .Select(p => new { p.ProductId, p.ProductCode, p.ProductName }).ToListAsync();
        if (validProducts.Count != ids.Length) { result.Errors.Add("Có sản phẩm không tồn tại hoặc đã ngừng hoạt động. Hãy chọn lại sản phẩm."); return result; }
        var sources = await stock.GetSourcesAsync(model.FromWarehouseId, model.ItemCondition, orderId);
        for (int i = 0; i < model.Lines.Count; i++)
        {
            var line = model.Lines[i];
            long available = sources.Where(s => s.ProductId == line.ProductId).Sum(s => s.FreeQuantity);
            line.AvailableQuantity = available;
            if (line.Quantity > available)
            {
                var product = validProducts.Single(p => p.ProductId == line.ProductId);
                string message = $"{product.ProductCode} — {product.ProductName}: yêu cầu {line.Quantity:N0}, tồn khả dụng {available:N0}.";
                if (release)
                {
                    result.Errors.Add(message);
                    result.FieldErrors.TryAdd($"Lines[{i}].Quantity", $"Vượt tồn khả dụng ({available:N0}).");
                }
                else
                {
                    result.Warnings.Add(message);
                }
            }
        }
        var capacityResult = new TransferAssessment();
        await capacity.EvaluateAsync(model, capacityResult, orderId);
        result.CurrentCapacity = capacityResult.CurrentCapacity;
        result.ForecastCapacity = capacityResult.ForecastCapacity;
        result.Warnings.AddRange(capacityResult.Warnings);
        if (release) result.Errors.AddRange(capacityResult.Errors); else result.Warnings.AddRange(capacityResult.Errors);
        return result;
    }

    // All checks and reservations share a serializable transaction. Lock order matches staff dispatch actions.
    private async Task LockWarehousesAsync(int from, int to)
    {
        foreach (var id in new[] { from, to }.Distinct().Order())
        {
            await db.Warehouses.FromSqlInterpolated($"SELECT * FROM dbo.Warehouse WITH (UPDLOCK,HOLDLOCK) WHERE WarehouseID={id}").AsNoTracking().ToListAsync();
            await db.SupplySources.FromSqlInterpolated($"SELECT * FROM dbo.SupplySource WITH (UPDLOCK,HOLDLOCK) WHERE WarehouseID={id} AND SupplyState='PHYSICAL'").AsNoTracking().ToListAsync();
        }
    }

    private async Task ReserveAsync(TransferOrder order, IEnumerable<TransferOrderLine> lines)
    {
        var heldByLine = await db.Reservations.AsNoTracking().Where(r => r.TransferOrderId == order.TransferOrderId && r.Status == "ACTIVE")
            .GroupBy(r => r.TransferLineId!.Value)
            .Select(g => new { LineId = g.Key, Quantity = g.Sum(r => (long)r.ReservedQuantity - r.ConsumedQuantity - r.ReleasedQuantity) })
            .ToDictionaryAsync(g => g.LineId, g => g.Quantity);
        var sources = await stock.GetSourcesAsync(order.FromWarehouseId, order.ItemCondition);
        var remaining = sources.ToDictionary(s => s.SourceId, s => s.FreeQuantity);
        foreach (var line in lines.Where(l => l.RequestedQuantity > l.CancelledQuantity))
        {
            long needed = line.RequestedQuantity - line.CancelledQuantity - heldByLine.GetValueOrDefault(line.TransferLineId);
            if (needed < 0) throw new DbUpdateConcurrencyException("Giữ chỗ vượt số lượng của dòng hàng.");
            if (needed == 0) continue;
            foreach (var s in sources.Where(s => s.ProductId == line.ProductId))
            {
                int take = (int)Math.Min(needed, remaining[s.SourceId]);
                if (take <= 0) continue;
                db.Reservations.Add(new Reservation
                {
                    SupplySourceId = s.SourceId, TransferLineId = line.TransferLineId, TransferOrderId = order.TransferOrderId,
                    ProductId = line.ProductId, WarehouseId = order.FromWarehouseId, ItemCondition = order.ItemCondition,
                    SupplyState = "PHYSICAL", ReservedQuantity = take, Status = "ACTIVE", CreatedAt = clock.GetUtcNow().UtcDateTime
                });
                remaining[s.SourceId] -= take;
                needed -= take;
                if (needed == 0) break;
            }
            if (needed != 0) throw new DbUpdateConcurrencyException("Tồn nguồn đã thay đổi. Hãy kiểm tra lại.");
        }
        await db.SaveChangesAsync();
    }

    public async Task<(bool Success, string? Error, long TransferOrderId)> CreateTransferOrderAsync(CreateTransferOrderViewModel model, int userId)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await LockWarehousesAsync(model.FromWarehouseId, model.ToWarehouseId);
            model.Assessment = await AssessAsync(model);
            if (model.Assessment.Errors.Count > 0) return (false, string.Join(" ", model.Assessment.Errors), 0);
            await TransferDataAccess.SetActorAsync(db, userId, "Create transfer");
            var order = new TransferOrder
            {
                // Unique even when two managers create at the same time; no MAX(code) race.
                TransferCode = $"TR-{TransferPolicy.Today(clock):yyyyMMdd}-{Guid.NewGuid():N}"[..37],
                FromWarehouseId = model.FromWarehouseId, ToWarehouseId = model.ToWarehouseId, ItemCondition = model.ItemCondition,
                DispatchDate = model.DispatchDate, ExpectedDate = model.ExpectedDate, Note = model.Note?.Trim(),
                CreatedByUserId = userId, CreatedAt = clock.GetUtcNow().UtcDateTime, Status = model.IsRelease ? "RELEASED" : "DRAFT",
                TransferOrderLines = model.Lines.Select(l => new TransferOrderLine { ProductId = l.ProductId, RequestedQuantity = l.Quantity }).ToList()
            };
            db.TransferOrders.Add(order);
            await db.SaveChangesAsync();
            if (model.IsRelease) await ReserveAsync(order, order.TransferOrderLines);
            await TransferDataAccess.SetActorAsync(db, null, null);
            await tx.CommitAsync();
            return (true, null, order.TransferOrderId);
        }
        catch (Exception ex) when (ex is DbUpdateException or SqlException)
        {
            logger.LogWarning(ex, "Could not create transfer order.");
            return (false, "Dữ liệu vừa thay đổi hoặc không đáp ứng ràng buộc CSDL. Hãy tải lại và kiểm tra tồn kho trước khi lưu.", 0);
        }
        finally
        {
            await TransferDataAccess.TryClearActorAsync(db, logger);
            db.ChangeTracker.Clear();
        }
    }

    public async Task<(bool Success, string? Error)> UpdateTransferOrderAsync(EditTransferOrderViewModel model, int userId)
    {
        if (await expiry.CancelIfExpiredAsync(model.TransferOrderId)) return (false, "Lệnh đã quá ngày dự kiến đến và được tự động hủy. Hãy tạo lệnh khác.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var order = await TransferDataAccess.LockOrderAsync(db, model.TransferOrderId);
            if (order == null) return (false, "Lệnh chuyển kho không tồn tại.");
            if (await expiry.CancelLockedAsync(order)) { await tx.CommitAsync(); return (false, TransferPolicy.ExpiryReason); }
            if (order.Status is not ("DRAFT" or "RELEASED") || await TransferDataAccess.HasDocumentsAsync(db, order.TransferOrderId))
                return (false, "Lệnh đã có phiếu gửi/nhận hoặc đã kết thúc; toàn bộ chỉnh sửa bị khóa.");
            byte[] postedVersion;
            try { postedVersion = Convert.FromBase64String(model.RowVersionBase64 ?? ""); }
            catch (FormatException) { return (false, "Phiên bản dữ liệu không hợp lệ. Hãy tải lại trang."); }
            if (!order.RowVersion.SequenceEqual(postedVersion)) return (false, "Lệnh vừa được người khác cập nhật. Hãy tải lại trang để xem dữ liệu mới.");
            if (await db.DemandAllocations.AnyAsync(a => a.TransferOrderId == order.TransferOrderId)
                || await db.Reservations.AnyAsync(r => r.TransferOrderId == order.TransferOrderId && r.ConsumedQuantity > 0))
                return (false, "Lệnh đã có dữ liệu phân bổ/tiêu thụ hàng; không thể sửa thủ công.");
            bool release = order.Status == "RELEASED" || model.IsRelease;
            await LockWarehousesAsync(model.FromWarehouseId, model.ToWarehouseId);
            model.Assessment = await AssessAsync(model, order.TransferOrderId, release);
            if (model.Assessment.Errors.Count > 0) return (false, string.Join(" ", model.Assessment.Errors));
            bool sourceChanged = order.FromWarehouseId != model.FromWarehouseId || order.ItemCondition != model.ItemCondition;
            await TransferDataAccess.SetActorAsync(db, userId, sourceChanged
                ? "Edit transfer source; replace unused holds, preserving history in AuditLog"
                : "Edit transfer; adjust only changed demand and reservation balances");
            if (sourceChanged)
            {
                var error = await TransferDataAccess.RemoveUnusedHoldsAsync(db, order.TransferOrderId);
                if (error != null) return (false, error);
            }
            var oldLines = await db.TransferOrderLines.Where(l => l.TransferOrderId == order.TransferOrderId).ToListAsync();
            var holds = await db.Reservations.Where(r => r.TransferOrderId == order.TransferOrderId && r.Status == "ACTIVE").ToListAsync();
            var quantities = model.Lines.ToDictionary(l => l.ProductId, l => l.Quantity);
            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var line in oldLines)
            {
                int target = quantities.Remove(line.ProductId, out int qty) ? qty : 0;
                long excess = holds.Where(r => r.TransferLineId == line.TransferLineId)
                    .Sum(r => (long)r.ReservedQuantity - r.ConsumedQuantity - r.ReleasedQuantity) - (release ? target : 0);
                foreach (var hold in holds.Where(r => r.TransferLineId == line.TransferLineId).OrderByDescending(r => r.ReservationId))
                {
                    if (excess <= 0) break;
                    int take = (int)Math.Min(excess, hold.ReservedQuantity - hold.ConsumedQuantity - hold.ReleasedQuantity);
                    TransferDataAccess.ReleaseQuantity(hold, take, now);
                    excess -= take;
                }
                // Product is part of a composite FK: retain its key and reuse it if re-added later.
                if (target > 0) { line.RequestedQuantity = target; line.CancelledQuantity = 0; }
                else line.CancelledQuantity = line.RequestedQuantity;
            }
            foreach (var (productId, qty) in quantities)
                db.TransferOrderLines.Add(new TransferOrderLine { TransferOrderId = order.TransferOrderId, ProductId = productId, RequestedQuantity = qty });
            await db.SaveChangesAsync();
            // FromWarehouse/condition are alternate keys used by dispatch items. EF cannot mutate them.
            // SQL remains parameterized, row-version checked and inside the same locked transaction.
            string status = release ? "RELEASED" : "DRAFT";
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.TransferOrder SET FromWarehouseID={model.FromWarehouseId}, ToWarehouseID={model.ToWarehouseId}, ItemCondition={model.ItemCondition}, DispatchDate={model.DispatchDate}, ExpectedDate={model.ExpectedDate}, Note={model.Note}, Status={status} WHERE TransferOrderID={order.TransferOrderId} AND RowVersion={postedVersion}");
            if (changed != 1) throw new DbUpdateConcurrencyException();
            db.ChangeTracker.Clear();
            order = (await db.TransferOrders.SingleAsync(o => o.TransferOrderId == model.TransferOrderId));
            var updatedLines = await db.TransferOrderLines.Where(l => l.TransferOrderId == order.TransferOrderId).ToListAsync();
            if (release) await ReserveAsync(order, updatedLines);
            await TransferDataAccess.SetActorAsync(db, null, null);
            await tx.CommitAsync();
            return (true, null);
        }
        catch (Exception ex) when (ex is DbUpdateException or SqlException)
        {
            logger.LogWarning(ex, "Could not edit transfer {TransferOrderId}.", model.TransferOrderId);
            return (false, "Không thể lưu do dữ liệu vừa thay đổi hoặc ràng buộc CSDL. Hãy tải lại trang; các giữ chỗ cũ được giữ nguyên nếu giao dịch thất bại.");
        }
        finally { await TransferDataAccess.TryClearActorAsync(db, logger); db.ChangeTracker.Clear(); }
    }

    public async Task<(bool Success, string? Error)> CancelTransferOrderAsync(long id, int userId)
    {
        var order = await db.TransferOrders
            .Include(t => t.TransferOrderLines)
            .Include(t => t.Reservations)
            .FirstOrDefaultAsync(t => t.TransferOrderId == id);

        if (order == null)
            return (false, "Không tìm thấy lệnh chuyển kho.");

        if (order.Status == "COMPLETED" || order.Status == "CANCELLED" || order.Status == "CLOSED")
            return (false, $"Lệnh chuyển kho đã ở trạng thái '{order.Status}', không thể hủy.");

        if (order.Status != "DRAFT" && order.Status != "RELEASED")
            return (false, "Chỉ có thể hủy lệnh chuyển kho ở trạng thái Bản nháp (DRAFT) hoặc Đã phát hành (RELEASED).");

        bool hasDispatch = await db.TransferDispatches.AnyAsync(d => d.TransferOrderId == id);
        bool hasReceipt = await db.TransferReceipts.AnyAsync(r => r.TransferOrderId == id);
        if (hasDispatch || hasReceipt)
            return (false, "Lệnh chuyển kho đã phát sinh phiếu xuất chuyển hoặc nhận hàng, không thể hủy.");

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            order.Status = "CANCELLED";
            order.ClosedAt = DateTime.UtcNow;
            order.CloseReason = "Đã hủy bởi Quản lý kho";

            foreach (var line in order.TransferOrderLines)
            {
                line.CancelledQuantity = line.RequestedQuantity;
            }

            var activeReservations = order.Reservations.Where(r => r.Status == "ACTIVE").ToList();
            if (activeReservations.Count > 0)
            {
                db.Reservations.RemoveRange(activeReservations);
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return (false, $"Lỗi khi hủy lệnh chuyển kho: {ex.Message}");
        }
    }

    public async Task<TransferListViewModel> GetListAsync(
        int userId,
        string role,
        string? search = null,
        string? status = null,
        int? fromWarehouseId = null,
        int? toWarehouseId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int page = 1,
        int pageSize = 10)
    {
        var query = db.TransferOrders
            .AsNoTracking()
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.TransferOrderLines)
            .AsQueryable();

        // Section 4: Manager xem tất cả; Warehouse Staff chỉ xem lệnh liên quan đến kho nguồn HOẶC kho đích
        if (role == "WAREHOUSE_STAFF")
        {
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            int? staffWarehouseId = user?.WarehouseId;
            if (!staffWarehouseId.HasValue)
            {
                return new TransferListViewModel { CanCreate = false };
            }

            int whId = staffWarehouseId.Value;
            query = query.Where(t => t.FromWarehouseId == whId || t.ToWarehouseId == whId);
        }

        // Filters
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(t => t.TransferCode.Contains(term)
                || t.FromWarehouse.WarehouseName.Contains(term)
                || t.ToWarehouse.WarehouseName.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        if (fromWarehouseId.HasValue && fromWarehouseId.Value > 0)
        {
            query = query.Where(t => t.FromWarehouseId == fromWarehouseId.Value);
        }

        if (toWarehouseId.HasValue && toWarehouseId.Value > 0)
        {
            query = query.Where(t => t.ToWarehouseId == toWarehouseId.Value);
        }

        if (fromDate.HasValue)
        {
            var fromDt = fromDate.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(t => t.CreatedAt >= fromDt);
        }

        if (toDate.HasValue)
        {
            var toDt = toDate.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(t => t.CreatedAt <= toDt);
        }

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TransferListItemDto
            {
                TransferOrderId = t.TransferOrderId,
                TransferCode = t.TransferCode,
                FromWarehouseId = t.FromWarehouseId,
                FromWarehouseName = t.FromWarehouse.WarehouseName,
                ToWarehouseId = t.ToWarehouseId,
                ToWarehouseName = t.ToWarehouse.WarehouseName,
                ItemCondition = t.ItemCondition,
                Status = t.Status,
                DispatchDate = t.DispatchDate,
                ExpectedDate = t.ExpectedDate,
                CreatedAt = t.CreatedAt,
                CreatedByName = t.CreatedByUser.FullName,
                AssignedToName = t.AssignedToUser != null ? t.AssignedToUser.FullName : null,
                TotalQuantity = t.TransferOrderLines.Sum(l => l.RequestedQuantity),
                CanEdit = (role == "MANAGER" && t.Status != "COMPLETED" && t.Status != "CANCELLED" && t.Status != "CLOSED")
            })
            .ToListAsync();

        var warehouses = await db.Warehouses
            .AsNoTracking()
            .Where(w => w.Status == "ACTIVE")
            .Select(w => new LookupItemDto { Id = w.WarehouseId, Name = w.WarehouseName })
            .ToListAsync();

        return new TransferListViewModel
        {
            SearchTerm = search,
            Status = status,
            FromWarehouseId = fromWarehouseId,
            ToWarehouseId = toWarehouseId,
            FromDate = fromDate,
            ToDate = toDate,
            PageIndex = page,
            PageSize = pageSize,
            TotalItems = totalCount,
            Items = items,
            Warehouses = warehouses,
            CanCreate = (role == "MANAGER")
        };
    }

    // =========================================================================
    // 2. CHI TIẾT LỆNH CHUYỂN KHO (UC-062)
    // =========================================================================
    public async Task<TransferDetailsViewModel?> GetDetailsAsync(long id, int userId, string role)
    {
        var order = await db.TransferOrders
            .AsNoTracking()
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.TransferOrderLines)
                .ThenInclude(l => l.Product)
            .Include(t => t.TransferDispatches)
            .FirstOrDefaultAsync(t => t.TransferOrderId == id);

        if (order == null) return null;

        // Section 4 isolation check
        if (role == "WAREHOUSE_STAFF")
        {
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            int? staffWarehouseId = user?.WarehouseId;
            if (!staffWarehouseId.HasValue || (order.FromWarehouseId != staffWarehouseId.Value && order.ToWarehouseId != staffWarehouseId.Value))
            {
                return null;
            }
        }

        // Đếm số lượng đã dispatch và nhận
        var dispatchItems = await db.TransferDispatchItems
            .AsNoTracking()
            .Where(di => di.Dispatch.TransferOrderId == id && di.IsActive)
            .ToListAsync();

        var lines = new List<TransferLineItemDto>();
        foreach (var l in order.TransferOrderLines)
        {
            int dispatched = dispatchItems.Count(di => di.TransferLineId == l.TransferLineId);

            lines.Add(new TransferLineItemDto
            {
                TransferLineId = l.TransferLineId,
                ProductId = l.ProductId,
                ProductCode = l.Product.ProductCode,
                ProductName = l.Product.ProductName,
                Model = l.Product.Model,
                Unit = l.Product.Unit,
                RequestedQuantity = l.RequestedQuantity,
                CancelledQuantity = l.CancelledQuantity,
                DispatchedQuantity = dispatched,
                ReceivedQuantity = 0
            });
        }

        var dispatches = order.TransferDispatches
            .Select(d => new TransferDispatchSummaryDto
            {
                DispatchId = d.DispatchId,
                DispatchCode = d.DispatchCode,
                Status = d.Status,
                CreatedAt = d.CreatedAt,
                ItemCount = dispatchItems.Count(di => di.DispatchId == d.DispatchId)
            })
            .ToList();

        return new TransferDetailsViewModel
        {
            TransferOrderId = order.TransferOrderId,
            TransferCode = order.TransferCode,
            FromWarehouseId = order.FromWarehouseId,
            FromWarehouseName = order.FromWarehouse.WarehouseName,
            ToWarehouseId = order.ToWarehouseId,
            ToWarehouseName = order.ToWarehouse.WarehouseName,
            ItemCondition = order.ItemCondition,
            Status = order.Status,
            DispatchDate = order.DispatchDate,
            ExpectedDate = order.ExpectedDate,
            CreatedAt = order.CreatedAt,
            CreatedByName = order.CreatedByUser.FullName,
            AssignedToName = order.AssignedToUser?.FullName,
            Note = order.Note,
            RowVersion = order.RowVersion,
            CanEdit = (role == "MANAGER" && order.Status != "COMPLETED" && order.Status != "CANCELLED" && order.Status != "CLOSED"),
            Lines = lines,
            Dispatches = dispatches
        };
    }

}
