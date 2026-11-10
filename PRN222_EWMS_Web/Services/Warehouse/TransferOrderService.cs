using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Services.Purchasing;
using PRN222_EWMS_Web.ViewModels.Transfer;

namespace PRN222_EWMS_Web.Services.Warehouse;

public class TransferOrderService(EwmsDbContext db, IWarehouseCapacityEngine capacityEngine) : ITransferOrderService
{
    // =========================================================================
    // 1. DANH SÁCH LỆNH CHUYỂN KHO (THEO QUY TẮC CÔ LẬP KHO - SECTION 4)
    // =========================================================================
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

    // =========================================================================
    // 3. CHỨC NĂNG 37: TẠO LỆNH CHUYỂN KHO (MANAGER)
    // =========================================================================
    public async Task<CreateTransferOrderViewModel> GetCreateFormAsync()
    {
        var warehouses = await db.Warehouses
            .AsNoTracking()
            .Where(w => w.Status == "ACTIVE")
            .Select(w => new LookupItemDto { Id = w.WarehouseId, Name = w.WarehouseName })
            .ToListAsync();

        var products = await db.Products
            .AsNoTracking()
            .Where(p => p.Status == "ACTIVE")
            .Select(p => new ProductLookupDto
            {
                ProductId = p.ProductId,
                ProductCode = p.ProductCode,
                ProductName = p.ProductName,
                Unit = p.Unit,
                CategoryId = p.CategoryId,
                BrandId = p.BrandId
            })
            .ToListAsync();

        return new CreateTransferOrderViewModel
        {
            Warehouses = warehouses,
            Products = products,
            StaffUsers = [],
            ExpectedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            Lines = [new CreateTransferOrderLineInput { ProductId = 0, Quantity = 1 }]
        };
    }

    public async Task<(bool Success, string? Error, long TransferOrderId)> CreateTransferOrderAsync(
        CreateTransferOrderViewModel model,
        int userId)
    {
        // 1. Validation kho nguồn và kho đích
        if (model.FromWarehouseId == model.ToWarehouseId)
        {
            return (false, "Select two different valid warehouses.", 0);
        }

        var fromWh = await db.Warehouses.FindAsync(model.FromWarehouseId);
        var toWh = await db.Warehouses.FindAsync(model.ToWarehouseId);
        if (fromWh == null || fromWh.Status != "ACTIVE" || toWh == null || toWh.Status != "ACTIVE")
        {
            return (false, "Một trong hai kho được chọn không tồn tại hoặc đã ngừng hoạt động.", 0);
        }

        // 2. Validation Condition
        if (model.ItemCondition != "GOOD" && model.ItemCondition != "DAMAGED")
        {
            return (false, "Tình trạng hàng phải là GOOD hoặc DAMAGED.", 0);
        }

        // 3. Validation ETA
        if (model.ExpectedDate.HasValue && model.ExpectedDate.Value < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return (false, "Ngày dự kiến chuyển đến (ETA) không được nhỏ hơn ngày hiện tại.", 0);
        }

        // 4. Validation dòng sản phẩm
        var validLines = model.Lines.Where(l => l.ProductId > 0 && l.Quantity > 0).ToList();
        if (validLines.Count == 0)
        {
            return (false, "Lệnh chuyển kho phải có ít nhất một dòng sản phẩm với số lượng lớn hơn 0.", 0);
        }

        // Gộp các dòng trùng sản phẩm để tính đúng tổng số lượng
        var groupedLines = validLines
            .GroupBy(l => l.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        // 5. Kiểm tra sức chứa kho đích (BẮT BUỘC KHI SAVE DRAFT VÀ RELEASE - SECTION 6)
        if (model.ItemCondition == "GOOD")
        {
            var capCheck = await capacityEngine.ValidatePurchaseOrderLinesCapacityAsync(
                model.ToWarehouseId,
                groupedLines.Select(l => (l.ProductId, l.Quantity)));

            if (!capCheck.IsValid)
            {
                return (false, capCheck.ErrorMessage ?? "The destination warehouse does not have enough compatible capacity for all transfer items on the expected arrival date.", 0);
            }
        }
        else // DAMAGED
        {
            // Kiểm tra kho đích có Zone khu vực DEFECTIVE không
            bool hasDefectiveZone = await db.StorageLocations
                .AsNoTracking()
                .AnyAsync(l => l.WarehouseId == model.ToWarehouseId
                    && (l.ZoneType == "DEFECTIVE" || l.ConditionPolicy == "DEFECTIVE_ONLY")
                    && l.Status == "ACTIVE");

            if (!hasDefectiveZone)
            {
                return (false, "No compatible storage zone is configured for this product category, brand, and condition.", 0);
            }
        }

        // 6. Nếu RELEASE ngay: Kiểm tra tồn vật lý khả dụng tại kho nguồn
        if (model.IsRelease)
        {
            foreach (var line in groupedLines)
            {
                // Tồn vật lý trong kho có tình trạng phù hợp
                int physicalCount = await db.SerialItems
                    .AsNoTracking()
                    .Where(s => s.CurrentWarehouseId == model.FromWarehouseId
                        && s.ProductId == line.ProductId
                        && s.ItemCondition == model.ItemCondition
                        && s.StockState == "IN_WAREHOUSE"
                        && !s.IsQuarantined)
                    .CountAsync();

                // Trừ các serial đang bị claim
                int claimedCount = await db.PickingClaims
                    .AsNoTracking()
                    .Where(c => db.SerialItems.Any(s => s.SerialItemId == c.SerialItemId
                        && s.CurrentWarehouseId == model.FromWarehouseId
                        && s.ProductId == line.ProductId
                        && s.ItemCondition == model.ItemCondition))
                    .CountAsync();

                // Trừ các Reservation ACTIVE
                int reservedCount = await db.Reservations
                    .AsNoTracking()
                    .Where(r => r.WarehouseId == model.FromWarehouseId
                        && r.ProductId == line.ProductId
                        && r.ItemCondition == model.ItemCondition
                        && r.SupplyState == "PHYSICAL"
                        && r.Status == "ACTIVE")
                    .SumAsync(r => (int?)(r.ReservedQuantity - r.ConsumedQuantity - r.ReleasedQuantity)) ?? 0;

                int availablePhysical = physicalCount - claimedCount - reservedCount;
                if (availablePhysical < line.Quantity)
                {
                    return (false, "The source warehouse does not have enough unreserved physical stock for this transfer.", 0);
                }
            }
        }

        // 7. Sinh mã lệnh TR-yyyy-XXX
        var currentYear = DateTime.UtcNow.Year;
        var prefix = $"TR-{currentYear}-";
        var lastCode = await db.TransferOrders
            .Where(t => t.TransferCode.StartsWith(prefix))
            .OrderByDescending(t => t.TransferCode)
            .Select(t => t.TransferCode)
            .FirstOrDefaultAsync();

        int nextSeq = 1;
        if (lastCode != null && lastCode.Length > prefix.Length && int.TryParse(lastCode[prefix.Length..], out int parsed))
        {
            nextSeq = parsed + 1;
        }
        string transferCode = $"{prefix}{nextSeq:D3}";

        // 8. Lưu trong transaction nguyên tử
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var transferOrder = new TransferOrder
            {
                TransferCode = transferCode,
                FromWarehouseId = model.FromWarehouseId,
                ToWarehouseId = model.ToWarehouseId,
                ItemCondition = model.ItemCondition,
                Status = model.IsRelease ? "RELEASED" : "DRAFT",
                ExpectedDate = model.ExpectedDate,
                CreatedByUserId = userId,
                AssignedToUserId = null, // Để trống để nhân viên tại kho nguồn/kho đích tự do tiếp nhận
                CreatedAt = DateTime.UtcNow,
                Note = model.Note
            };

            db.TransferOrders.Add(transferOrder);
            await db.SaveChangesAsync();

            var createdLines = new List<TransferOrderLine>();
            foreach (var line in groupedLines)
            {
                var orderLine = new TransferOrderLine
                {
                    TransferOrderId = transferOrder.TransferOrderId,
                    ProductId = line.ProductId,
                    RequestedQuantity = line.Quantity,
                    CancelledQuantity = 0
                };
                db.TransferOrderLines.Add(orderLine);
                createdLines.Add(orderLine);
            }
            await db.SaveChangesAsync();

            // Nếu RELEASE: Tạo Reservation vật lý cho từng dòng (CK_Reservation_DemandContext)
            if (model.IsRelease)
            {
                foreach (var line in createdLines)
                {
                    // Tìm nguồn cung PHYSICAL phù hợp
                    var supplySource = await db.SupplySources
                        .FirstOrDefaultAsync(s => s.ProductId == line.ProductId
                            && s.WarehouseId == model.FromWarehouseId
                            && s.ItemCondition == model.ItemCondition
                            && s.SupplyState == "PHYSICAL");

                    if (supplySource == null)
                    {
                        // Tạo hoặc dùng nguồn cung physical mặc định
                        supplySource = new SupplySource
                        {
                            ProductId = line.ProductId,
                            WarehouseId = model.FromWarehouseId,
                            ItemCondition = model.ItemCondition,
                            SourceType = "INBOUND",
                            SupplyState = "PHYSICAL",
                            EligibleQuantity = line.RequestedQuantity,
                            RemainingQuantity = line.RequestedQuantity,
                            CreatedAt = DateTime.UtcNow
                        };
                        db.SupplySources.Add(supplySource);
                        await db.SaveChangesAsync();
                    }

                    var reservation = new Reservation
                    {
                        SupplySourceId = supplySource.SupplySourceId,
                        TransferOrderId = transferOrder.TransferOrderId,
                        TransferLineId = line.TransferLineId,
                        SolineId = null,
                        SalesOrderId = null,
                        ReservedQuantity = line.RequestedQuantity,
                        ConsumedQuantity = 0,
                        ReleasedQuantity = 0,
                        Status = "ACTIVE",
                        CreatedAt = DateTime.UtcNow,
                        ProductId = line.ProductId,
                        WarehouseId = model.FromWarehouseId,
                        ItemCondition = model.ItemCondition,
                        SupplyState = "PHYSICAL"
                    };
                    db.Reservations.Add(reservation);
                }
                await db.SaveChangesAsync();
            }

            await tx.CommitAsync();
            return (true, null, transferOrder.TransferOrderId);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return (false, $"Lỗi hệ thống khi tạo lệnh chuyển kho: {ex.Message}", 0);
        }
    }

    // =========================================================================
    // 4. CHỨC NĂNG 36: CHỈNH SỬA LỆNH CHUYỂN KHO (MANAGER)
    // =========================================================================
    public async Task<EditTransferOrderViewModel?> GetEditFormAsync(long id, int userId)
    {
        var order = await db.TransferOrders
            .AsNoTracking()
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.TransferOrderLines)
                .ThenInclude(l => l.Product)
            .Include(t => t.TransferDispatches)
            .Include(t => t.TransferDispatchItems)
            .FirstOrDefaultAsync(t => t.TransferOrderId == id);

        if (order == null) return null;

        // Trạng thái đã đóng hoặc hủy: không sửa được
        if (order.Status == "COMPLETED" || order.Status == "CANCELLED" || order.Status == "CLOSED")
            return null;

        bool hasDispatch = await db.TransferDispatches.AnyAsync(d => d.TransferOrderId == id);
        bool hasReceipt = await db.TransferReceipts.AnyAsync(r => r.TransferOrderId == id);
        bool isSourceFrozen = hasDispatch || hasReceipt;

        var warehouses = await db.Warehouses
            .AsNoTracking()
            .Where(w => w.Status == "ACTIVE")
            .Select(w => new LookupItemDto { Id = w.WarehouseId, Name = w.WarehouseName })
            .ToListAsync();

        var products = await db.Products
            .AsNoTracking()
            .Where(p => p.Status == "ACTIVE")
            .Select(p => new ProductLookupDto
            {
                ProductId = p.ProductId,
                ProductCode = p.ProductCode,
                ProductName = p.ProductName,
                Unit = p.Unit,
                CategoryId = p.CategoryId,
                BrandId = p.BrandId
            })
            .ToListAsync();

        var dispatchItems = await db.TransferDispatchItems
            .AsNoTracking()
            .Where(di => di.Dispatch.TransferOrderId == id && di.IsActive)
            .ToListAsync();

        var lines = order.TransferOrderLines.Select(l =>
        {
            int dispatched = dispatchItems.Count(di => di.TransferLineId == l.TransferLineId);
            return new EditTransferOrderLineInput
            {
                TransferLineId = l.TransferLineId,
                ProductId = l.ProductId,
                Quantity = l.RequestedQuantity,
                MinQuantityAllowed = dispatched
            };
        }).ToList();

        return new EditTransferOrderViewModel
        {
            TransferOrderId = order.TransferOrderId,
            TransferCode = order.TransferCode,
            Status = order.Status,
            RowVersion = order.RowVersion,
            RowVersionBase64 = Convert.ToBase64String(order.RowVersion),
            FromWarehouseId = order.FromWarehouseId,
            FromWarehouseName = order.FromWarehouse.WarehouseName,
            ToWarehouseId = order.ToWarehouseId,
            ToWarehouseName = order.ToWarehouse.WarehouseName,
            ItemCondition = order.ItemCondition,
            ExpectedDate = order.ExpectedDate,
            AssignedToUserId = order.AssignedToUserId,
            Note = order.Note,
            IsSourceContextFrozen = isSourceFrozen,
            Lines = lines,
            Warehouses = warehouses,
            Products = products,
            StaffUsers = []
        };
    }

    public async Task<(bool Success, string? Error)> UpdateTransferOrderAsync(
        EditTransferOrderViewModel model,
        int userId)
    {
        var order = await db.TransferOrders
            .Include(t => t.TransferOrderLines)
            .Include(t => t.Reservations)
            .FirstOrDefaultAsync(t => t.TransferOrderId == model.TransferOrderId);

        if (order == null)
            return (false, "Không tìm thấy lệnh chuyển kho.");

        // Kiểm tra terminal states
        if (order.Status == "COMPLETED" || order.Status == "CANCELLED" || order.Status == "CLOSED")
            return (false, $"Lệnh chuyển kho đã ở trạng thái '{order.Status}', không thể chỉnh sửa.");

        // Concurrency Check (Section 7.2)
        var currentVersionBase64 = Convert.ToBase64String(order.RowVersion);
        if (!string.Equals(currentVersionBase64, model.RowVersionBase64, StringComparison.Ordinal))
        {
            return (false, "This transfer order changed while you were editing it. Reload before saving.");
        }

        bool hasDispatch = await db.TransferDispatches.AnyAsync(d => d.TransferOrderId == model.TransferOrderId);
        bool hasReceipt = await db.TransferReceipts.AnyAsync(r => r.TransferOrderId == model.TransferOrderId);
        bool isSourceFrozen = hasDispatch || hasReceipt;

        // Nếu Source Context bị đóng băng (TR_TransferOrder_SourceContextFrozen), không được đổi kho hoặc condition
        if (isSourceFrozen)
        {
            if (order.FromWarehouseId != model.FromWarehouseId || order.ToWarehouseId != model.ToWarehouseId || order.ItemCondition != model.ItemCondition)
            {
                return (false, "Source document context cannot change while dependent source/work records exist.");
            }
        }
        else
        {
            if (model.FromWarehouseId == model.ToWarehouseId)
            {
                return (false, "Select two different valid warehouses.");
            }
            order.FromWarehouseId = model.FromWarehouseId;
            order.ToWarehouseId = model.ToWarehouseId;
            order.ItemCondition = model.ItemCondition;
        }

        // Validate ETA
        if (model.ExpectedDate.HasValue && model.ExpectedDate.Value < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return (false, "Ngày dự kiến chuyển đến (ETA) không được nhỏ hơn ngày hiện tại.");
        }

        // Validate Lines
        var validLines = model.Lines.Where(l => l.ProductId > 0 && l.Quantity > 0).ToList();
        if (validLines.Count == 0)
        {
            return (false, "Lệnh chuyển kho phải có ít nhất một dòng sản phẩm với số lượng lớn hơn 0.");
        }

        var groupedLines = validLines
            .GroupBy(l => l.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        // Kiểm tra không được giảm số lượng thấp hơn số đã Dispatch
        var dispatchItems = await db.TransferDispatchItems
            .AsNoTracking()
            .Where(di => di.Dispatch.TransferOrderId == model.TransferOrderId && di.IsActive)
            .ToListAsync();

        foreach (var l in validLines.Where(l => l.TransferLineId > 0))
        {
            int dispatched = dispatchItems.Count(di => di.TransferLineId == l.TransferLineId);
            if (l.Quantity < dispatched)
            {
                return (false, $"Số lượng không được thấp hơn số lượng đã xuất chuyển ({dispatched}).");
            }
        }

        // Validate capacity đích
        if (order.ItemCondition == "GOOD")
        {
            var capCheck = await capacityEngine.ValidatePurchaseOrderLinesCapacityAsync(
                order.ToWarehouseId,
                groupedLines.Select(l => (l.ProductId, l.Quantity)));

            if (!capCheck.IsValid)
            {
                return (false, capCheck.ErrorMessage ?? "The destination warehouse does not have enough compatible capacity for all transfer items on the expected arrival date.");
            }
        }

        // Nếu lệnh đang RELEASED hoặc người dùng chọn Release ngay bây giờ:
        bool willBeReleased = (order.Status == "RELEASED") || model.IsRelease;

        if (willBeReleased)
        {
            // Recheck source physical stock
            foreach (var line in groupedLines)
            {
                int physicalCount = await db.SerialItems
                    .AsNoTracking()
                    .Where(s => s.CurrentWarehouseId == order.FromWarehouseId
                        && s.ProductId == line.ProductId
                        && s.ItemCondition == order.ItemCondition
                        && s.StockState == "IN_WAREHOUSE"
                        && !s.IsQuarantined)
                    .CountAsync();

                int claimedCount = await db.PickingClaims
                    .AsNoTracking()
                    .Where(c => db.SerialItems.Any(s => s.SerialItemId == c.SerialItemId
                        && s.CurrentWarehouseId == order.FromWarehouseId
                        && s.ProductId == line.ProductId
                        && s.ItemCondition == order.ItemCondition))
                    .CountAsync();

                // Trừ Reservation của các lệnh KHÁC
                int reservedCountOther = await db.Reservations
                    .AsNoTracking()
                    .Where(r => r.WarehouseId == order.FromWarehouseId
                        && r.ProductId == line.ProductId
                        && r.ItemCondition == order.ItemCondition
                        && r.SupplyState == "PHYSICAL"
                        && r.Status == "ACTIVE"
                        && r.TransferOrderId != model.TransferOrderId)
                    .SumAsync(r => (int?)(r.ReservedQuantity - r.ConsumedQuantity - r.ReleasedQuantity)) ?? 0;

                int availablePhysical = physicalCount - claimedCount - reservedCountOther;
                if (availablePhysical < line.Quantity)
                {
                    return (false, "The source warehouse does not have enough unreserved physical stock for this transfer.");
                }
            }
        }

        // Thực hiện cập nhật trong transaction nguyên tử
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            order.ExpectedDate = model.ExpectedDate;
            order.AssignedToUserId = null; // Để trống để nhân viên tại kho nguồn/kho đích tự do tiếp nhận
            order.Note = model.Note;

            if (order.Status == "DRAFT" && model.IsRelease)
            {
                order.Status = "RELEASED";
            }

            // Cập nhật các dòng hàng
            // Xóa các dòng đã bỏ đi
            var lineIdsInModel = validLines.Where(l => l.TransferLineId > 0).Select(l => l.TransferLineId).ToHashSet();
            var linesToRemove = order.TransferOrderLines.Where(l => !lineIdsInModel.Contains(l.TransferLineId)).ToList();
            foreach (var lr in linesToRemove)
            {
                // Xóa reservations liên quan
                var resToRemove = order.Reservations.Where(r => r.TransferLineId == lr.TransferLineId).ToList();
                db.Reservations.RemoveRange(resToRemove);
                db.TransferOrderLines.Remove(lr);
            }

            // Sửa dòng cũ hoặc thêm dòng mới
            foreach (var inputLine in validLines)
            {
                if (inputLine.TransferLineId > 0)
                {
                    var existingLine = order.TransferOrderLines.FirstOrDefault(l => l.TransferLineId == inputLine.TransferLineId);
                    if (existingLine != null)
                    {
                        existingLine.ProductId = inputLine.ProductId;
                        existingLine.RequestedQuantity = inputLine.Quantity;

                        // Cập nhật reservation nếu đã Released
                        if (order.Status == "RELEASED")
                        {
                            var res = order.Reservations.FirstOrDefault(r => r.TransferLineId == existingLine.TransferLineId && r.Status == "ACTIVE");
                            if (res != null)
                            {
                                res.ReservedQuantity = inputLine.Quantity;
                                res.ProductId = inputLine.ProductId;
                            }
                        }
                    }
                }
                else
                {
                    var newLine = new TransferOrderLine
                    {
                        TransferOrderId = order.TransferOrderId,
                        ProductId = inputLine.ProductId,
                        RequestedQuantity = inputLine.Quantity,
                        CancelledQuantity = 0
                    };
                    db.TransferOrderLines.Add(newLine);
                }
            }
            await db.SaveChangesAsync();

            // Nếu lệnh mới chuyển sang RELEASED, tạo Reservation cho các dòng chưa có
            if (order.Status == "RELEASED")
            {
                foreach (var line in order.TransferOrderLines)
                {
                    bool hasRes = await db.Reservations.AnyAsync(r => r.TransferLineId == line.TransferLineId && r.Status == "ACTIVE");
                    if (!hasRes)
                    {
                        var supplySource = await db.SupplySources
                            .FirstOrDefaultAsync(s => s.ProductId == line.ProductId
                                && s.WarehouseId == order.FromWarehouseId
                                && s.ItemCondition == order.ItemCondition
                                && s.SupplyState == "PHYSICAL");

                        if (supplySource == null)
                        {
                            supplySource = new SupplySource
                            {
                                ProductId = line.ProductId,
                                WarehouseId = order.FromWarehouseId,
                                ItemCondition = order.ItemCondition,
                                SourceType = "INBOUND",
                                SupplyState = "PHYSICAL",
                                EligibleQuantity = line.RequestedQuantity,
                                RemainingQuantity = line.RequestedQuantity,
                                CreatedAt = DateTime.UtcNow
                            };
                            db.SupplySources.Add(supplySource);
                            await db.SaveChangesAsync();
                        }

                        var reservation = new Reservation
                        {
                            SupplySourceId = supplySource.SupplySourceId,
                            TransferOrderId = order.TransferOrderId,
                            TransferLineId = line.TransferLineId,
                            SolineId = null,
                            SalesOrderId = null,
                            ReservedQuantity = line.RequestedQuantity,
                            ConsumedQuantity = 0,
                            ReleasedQuantity = 0,
                            Status = "ACTIVE",
                            CreatedAt = DateTime.UtcNow,
                            ProductId = line.ProductId,
                            WarehouseId = order.FromWarehouseId,
                            ItemCondition = order.ItemCondition,
                            SupplyState = "PHYSICAL"
                        };
                        db.Reservations.Add(reservation);
                    }
                }
                await db.SaveChangesAsync();
            }

            await tx.CommitAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return (false, $"Lỗi hệ thống khi cập nhật: {ex.Message}");
        }
    }
}
