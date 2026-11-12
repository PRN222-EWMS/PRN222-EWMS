using System.Data;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Services.Warehouse;
using PRN222_EWMS_Web.ViewModels.Sales;

namespace PRN222_EWMS_Web.Services.Sales;

public class SalesOrderSourceException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}

/// <summary>Draft editing and publication are atomic; published order content is immutable.</summary>
public class SalesOrderSourceService(EwmsDbContext db, ISourceReservationService sources, TimeProvider clock,
    ILogger<SalesOrderSourceService> logger)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private DateOnly Today => DateOnly.FromDateTime(Now.AddHours(7));

    public async Task<long> SaveAsync(long? id, SalesOrderFormViewModel form, string action, int actorId, CancellationToken ct = default)
    {
        if (action is not ("draft" or "submit" or "save")) throw new InvalidOperationException("Thao tác lưu SO không hợp lệ.");
        bool submit = action == "submit";
        ValidateLines(form, submit);
        var oldWarehouse = id.HasValue ? await db.SalesOrders.AsNoTracking().Where(o => o.SalesOrderId == id.Value)
            .Select(o => (int?)o.WarehouseId).SingleOrDefaultAsync(ct) : null;
        if (id.HasValue && oldWarehouse == null) throw new InvalidOperationException("Không tìm thấy SO.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await SetAuditAsync(actorId, "Save sales order source plan", id.HasValue ? null : form.RequestToken, ct);
            await RequireSalesAsync(actorId, ct);
            if (!id.HasValue)
            {
                if (!Guid.TryParseExact(form.RequestToken, "N", out _)) throw new InvalidOperationException("Yêu cầu tạo không hợp lệ. Vui lòng tải lại form.");
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    DECLARE @result int;
                    EXEC @result=sys.sp_getapplock @Resource={"EWMS:SalesCreate:" + form.RequestToken},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
                    IF @result<0 THROW 51261,N'Yêu cầu tạo SO đang được xử lý. Vui lòng thử lại.',1;", ct);
                var previous = await db.AuditLogs.AsNoTracking().Where(a => a.TableName == "SalesOrder" && a.ActionType == "INSERT"
                        && a.ActorUserId == actorId && a.RequestId == form.RequestToken)
                    .OrderBy(a => a.AuditId).Select(a => a.EntityKey).FirstOrDefaultAsync(ct);
                if (long.TryParse(previous, out var previousId))
                {
                    var existing = await db.SalesOrders.AsNoTracking().SingleOrDefaultAsync(o => o.SalesOrderId == previousId && o.CreatedByUserId == actorId, ct);
                    if (existing == null || existing.Status != (submit ? "SUBMITTED" : "DRAFT"))
                        throw new InvalidOperationException("Yêu cầu tạo này đã được xử lý. Vui lòng mở SO đã lưu để tiếp tục.");
                    await transaction.CommitAsync(ct);
                    return previousId;
                }
            }
            foreach (var warehouseId in new[] { oldWarehouse ?? form.WarehouseId!.Value, form.WarehouseId!.Value }.Distinct().Order())
                await sources.LockWarehouseAsync(warehouseId, ct);
            var warehouse = await db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.WarehouseId == form.WarehouseId && w.Status == "ACTIVE", ct)
                ?? throw new SalesOrderSourceException(nameof(form.WarehouseId), "Kho xuất không còn hoạt động.");
            var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.CustomerId == form.CustomerId, ct)
                ?? throw new SalesOrderSourceException(nameof(form.CustomerId), "Khách hàng không còn tồn tại.");
            SalesOrder order;
            if (id.HasValue)
            {
                var current = await db.SalesOrders.FromSqlInterpolated($"SELECT * FROM dbo.SalesOrder WITH (UPDLOCK,HOLDLOCK) WHERE SalesOrderID={id.Value}")
                    .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new InvalidOperationException("Không tìm thấy SO.");
                if (current.CreatedByUserId != actorId) throw new UnauthorizedAccessException("Chỉ người tạo SO được sửa Draft.");
                if (current.Status != "DRAFT") throw new InvalidOperationException("SO đã phát hành, không được sửa nội dung kể cả chưa có phiếu xuất.");
                if (current.WarehouseId != oldWarehouse) throw new DbUpdateConcurrencyException("Kho của SO đã thay đổi. Vui lòng tải lại form.");
                CheckVersion(current.RowVersion, form.ConcurrencyToken);
                if (await db.Outbounds.AnyAsync(o => o.SalesOrderId == id.Value, ct)
                    || await db.Reservations.AnyAsync(r => r.SalesOrderId == id.Value, ct))
                    throw new InvalidOperationException("Draft có dữ liệu thực hiện/giữ chỗ; cần đối soát trước khi sửa.");
                var oldPlans = await db.SalesOrderSourcePlans.Where(p => p.SalesOrderLine.SalesOrderId == id.Value).ToListAsync(ct);
                db.SalesOrderSourcePlans.RemoveRange(oldPlans);
                await db.SaveChangesAsync(ct);
                // Warehouse is part of an EF alternate key. Change only an unused Draft context in SQL.
                if (current.WarehouseId != warehouse.WarehouseId)
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.SalesOrder SET WarehouseID={warehouse.WarehouseId} WHERE SalesOrderID={id.Value} AND Status='DRAFT'", ct);
                order = await db.SalesOrders.Include(o => o.SalesOrderLines).SingleAsync(o => o.SalesOrderId == id.Value, ct);
                if (form.Lines.Any(l => l.SolineId != 0 && order.SalesOrderLines.All(old => old.SolineId != l.SolineId)))
                    throw new InvalidOperationException("Dòng sản phẩm không thuộc SO đang sửa.");
                db.SalesOrderLines.RemoveRange(order.SalesOrderLines);
                await db.SaveChangesAsync(ct);
                order.SalesOrderLines.Clear();
            }
            else
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    DECLARE @result int;
                    EXEC @result=sys.sp_getapplock @Resource={"EWMS:SalesCode:" + Today.Year},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
                    IF @result<0 THROW 51262,N'Không lấy được mã SO. Vui lòng thử lại.',1;", ct);
                var prefix = $"SO-{Today.Year}-";
                var codes = await db.SalesOrders.AsNoTracking().Where(o => o.Socode.StartsWith(prefix)).Select(o => o.Socode).ToListAsync(ct);
                var max = codes.Select(code => long.TryParse(code[prefix.Length..], out var value) ? value : 0L).DefaultIfEmpty().Max();
                order = new SalesOrder { Socode = prefix + checked(max + 1).ToString("D3"), CreatedByUserId = actorId, CreatedAt = Now,
                    OrderDate = Today, WarehouseId = warehouse.WarehouseId, Status = "DRAFT" };
                db.SalesOrders.Add(order);
            }
            if (form.RequestedDate.HasValue && form.RequestedDate < order.OrderDate)
                throw new SalesOrderSourceException(nameof(form.RequestedDate), "Ngày phải xuất không được trước ngày đặt SO.");
            if (submit && form.RequestedDate < Today)
                throw new SalesOrderSourceException(nameof(form.RequestedDate), "Không thể phát hành SO với ngày phải xuất đã qua.");
            if (form.RequestedDate.HasValue && warehouse.WarehouseType == "RENTED"
                && (warehouse.RentalStartDate is not DateOnly start || warehouse.RentalEndDate is not DateOnly end
                    || form.RequestedDate < start || form.RequestedDate > end))
                throw new SalesOrderSourceException(nameof(form.RequestedDate), "Ngày phải xuất nằm ngoài thời hạn thuê kho.");
            var productIds = form.Lines.Select(l => l.ProductId!.Value).ToArray();
            if (await db.Products.CountAsync(p => productIds.Contains(p.ProductId) && p.Status == "ACTIVE", ct) != productIds.Length)
                throw new InvalidOperationException("Có sản phẩm không tồn tại hoặc đã ngừng hoạt động. Vui lòng chọn lại.");
            order.CustomerId = customer.CustomerId;
            order.RequestedDate = form.RequestedDate;
            order.Note = string.IsNullOrWhiteSpace(form.Note) ? null : form.Note.Trim();
            order.CustomerNameSnapshot = customer.CustomerName;
            order.CustomerAddressSnapshot = customer.Address;
            order.CustomerPhoneSnapshot = customer.Phone;
            // A line/source-only edit must also advance the header concurrency token.
            db.Entry(order).Property(o => o.Note).IsModified = id.HasValue;
            foreach (var input in form.Lines)
                order.SalesOrderLines.Add(new SalesOrderLine { ProductId = input.ProductId!.Value, RequestedQuantity = input.RequestedQuantity!.Value });
            await db.SaveChangesAsync(ct);
            var savedLines = order.SalesOrderLines.ToDictionary(l => l.ProductId);
            var resolved = new List<(int Index, SalesOrderLine Line, long SourceId, int Quantity)>();
            for (int i = 0; i < form.Lines.Count; i++)
            {
                var input = form.Lines[i];
                var used = new HashSet<long>();
                foreach (var choice in input.Sources)
                {
                    var sourceId = await sources.ResolveSourceAsync(warehouse.WarehouseId, choice, ct);
                    if (!used.Add(sourceId)) throw new SalesOrderSourceException($"Lines[{i}].Sources", "Một nguồn chỉ xuất hiện một lần trong dòng sản phẩm.");
                    resolved.Add((i, savedLines[input.ProductId!.Value], sourceId, choice.Quantity));
                }
            }
            var catalog = (await sources.GetOptionsAsync(warehouse.WarehouseId, order.RequestedDate, order.SalesOrderId, ct))
                .Where(s => s.SupplySourceId > 0).ToDictionary(s => s.SupplySourceId);
            foreach (var allocation in resolved)
            {
                if (!catalog.TryGetValue(allocation.SourceId, out var option) || option.ProductId != allocation.Line.ProductId
                    || !option.CanSelect || allocation.Quantity > option.AvailableQuantity)
                    throw new SalesOrderSourceException($"Lines[{allocation.Index}].Sources", "Nguồn đã hết, không đủ số lượng hoặc không kịp ngày. Kiểm tra lại phân bổ.");
                db.SalesOrderSourcePlans.Add(new SalesOrderSourcePlan { SalesOrderLine = allocation.Line, SupplySourceId = allocation.SourceId,
                    Quantity = allocation.Quantity, CreatedAt = Now });
                option.AvailableQuantity -= allocation.Quantity;
            }
            await db.SaveChangesAsync(ct);
            if (submit)
            {
                await sources.ReservePlansAsync(order, ct);
                order.Status = "SUBMITTED";
                order.SubmittedAt = Now;
                order.HasShortage = false;
                await db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return order.SalesOrderId;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            throw;
        }
        finally { await ClearAuditAsync(); }
    }

    private static void ValidateLines(SalesOrderFormViewModel form, bool submit)
    {
        if (form.WarehouseId is not > 0 || form.CustomerId is not > 0) throw new InvalidOperationException("Chọn khách hàng và kho xuất hợp lệ.");
        if (submit && !form.RequestedDate.HasValue) throw new SalesOrderSourceException(nameof(form.RequestedDate), "Chọn ngày phải xuất khi gửi SO.");
        if (form.Note?.Length > 500) throw new SalesOrderSourceException(nameof(form.Note), "Ghi chú tối đa 500 ký tự.");
        if (form.Lines is null || form.Lines.Count is < 1 or > 100) throw new InvalidOperationException("SO cần từ 1 đến 100 dòng sản phẩm.");
        if (form.Lines.Any(l => l.ProductId is not > 0 || l.RequestedQuantity is not > 0)) throw new InvalidOperationException("Sản phẩm và số lượng nguyên dương là bắt buộc.");
        if (form.Lines.Select(l => l.ProductId).Distinct().Count() != form.Lines.Count) throw new InvalidOperationException("Mỗi sản phẩm chỉ xuất hiện một lần trên SO.");
        for (int i = 0; i < form.Lines.Count; i++)
        {
            var line = form.Lines[i];
            if (line.Sources is null || line.Sources.Count is < 1 or > 100 || line.Sources.Any(s => s.Quantity <= 0)
                || line.Sources.Sum(s => (long)s.Quantity) != line.RequestedQuantity)
                throw new SalesOrderSourceException($"Lines[{i}].Sources", "Chọn nguồn và phân bổ đủ số lượng; Draft chỉ kiểm tra, chưa giữ hàng.");
        }
    }

    public async Task<(bool Success, string Message)> CancelAsync(long id, int actorId, string actorRole, string? reason, CancellationToken ct = default)
    {
        if (reason?.Trim().Length > 1000) return (false, "Lý do hủy tối đa 1.000 ký tự.");
        var warehouseId = await db.SalesOrders.AsNoTracking().Where(o => o.SalesOrderId == id).Select(o => (int?)o.WarehouseId).SingleOrDefaultAsync(ct);
        if (warehouseId == null) return (false, "Không tìm thấy SO.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var actor = await db.Users.AsNoTracking().Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == actorId && u.Status == "ACTIVE", ct);
            if (actor?.Role.RoleName != actorRole || actorRole is not ("MANAGER" or "SALES_STAFF"))
                return (false, "Bạn không có quyền hủy SO.");
            await sources.LockWarehouseAsync(warehouseId.Value, ct);
            var order = await db.SalesOrders.FromSqlInterpolated($"SELECT * FROM dbo.SalesOrder WITH (UPDLOCK,HOLDLOCK) WHERE SalesOrderID={id}")
                .SingleOrDefaultAsync(ct);
            if (order == null || order.WarehouseId != warehouseId) return (false, "SO đã thay đổi; vui lòng tải lại.");
            if (actorRole != "MANAGER" && order.CreatedByUserId != actorId) return (false, "Chỉ người tạo SO được hủy đơn của mình.");
            if (order.Status is not ("DRAFT" or "SUBMITTED") || await db.Outbounds.AnyAsync(o => o.SalesOrderId == id, ct))
                return (false, "SO đã có phiếu xuất hoặc không còn ở trạng thái được hủy.");
            await SetAuditAsync(actorId, "Cancel sales order; release unused commitments", null, ct);
            foreach (var hold in await db.Reservations.Where(r => r.SalesOrderId == id && r.Status == "ACTIVE").ToListAsync(ct))
            {
                hold.ReleasedQuantity += checked((int)SourceReservationService.Balance(hold));
                hold.Status = "CLOSED"; hold.ClosedAt = Now;
            }
            order.Status = "CANCELLED"; order.ClosedAt = Now; order.HasShortage = false;
            order.CloseReason = string.IsNullOrWhiteSpace(reason) ? "Hủy SO chưa thực hiện" : reason.Trim();
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (true, "Đã hủy SO và giải phóng phần nguồn chưa sử dụng.");
        }
        catch
        {
            await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); throw;
        }
        finally { await ClearAuditAsync(); }
    }

    public async Task<Dictionary<long, List<SalesSourceSummary>>> GetSummaryAsync(long id, CancellationToken ct = default)
    {
        var plans = await db.SalesOrderSourcePlans.AsNoTracking().Where(p => p.SalesOrderLine.SalesOrderId == id).ToListAsync(ct);
        var holds = await db.Reservations.AsNoTracking().Where(r => r.SalesOrderId == id && r.SolineId != null).ToListAsync(ct);
        var ids = plans.Select(p => p.SupplySourceId).Concat(holds.Select(r => r.SupplySourceId)).Distinct().ToArray();
        var origins = await db.SupplySources.AsNoTracking().Where(s => ids.Contains(s.SupplySourceId))
            .Include(s => s.Poline).ThenInclude(l => l!.PurchaseOrder).ThenInclude(p => p.Supplier)
            .Include(s => s.Inbound).Include(s => s.ReturnReceipt).Include(s => s.TransferReceipt).Include(s => s.Adjustment)
            .Include(s => s.IncomingTransferLine).ThenInclude(l => l!.TransferOrder).ThenInclude(o => o.FromWarehouse)
            .AsSplitQuery().ToDictionaryAsync(s => s.SupplySourceId, ct);
        var result = new Dictionary<long, List<SalesSourceSummary>>();
        foreach (var lineId in plans.Select(p => p.SolineId).Concat(holds.Select(r => r.SolineId!.Value)).Distinct())
        {
            result[lineId] = plans.Where(p => p.SolineId == lineId).Select(p => p.SupplySourceId)
                .Concat(holds.Where(r => r.SolineId == lineId).Select(r => r.SupplySourceId)).Distinct().Select(sourceId =>
                {
                    var source = origins[sourceId];
                    var reservations = holds.Where(r => r.SolineId == lineId && r.SupplySourceId == sourceId).ToList();
                    return new SalesSourceSummary { Label = SourceReservationService.Label(source), SupplyState = source.SupplyState,
                        OriginalQuantity = plans.Where(p => p.SolineId == lineId && p.SupplySourceId == sourceId).Sum(p => p.Quantity),
                        HeldQuantity = reservations.Where(r => r.Status == "ACTIVE").Sum(SourceReservationService.Balance),
                        ConsumedQuantity = reservations.Sum(r => (long)r.ConsumedQuantity), ReleasedQuantity = reservations.Sum(r => (long)r.ReleasedQuantity) };
                }).ToList();
        }
        return result;
    }

    public async Task<SalesOrderFormViewModel?> GetReplacementFormAsync(long id, int actorId, CancellationToken ct = default)
    {
        var order = await db.SalesOrders.AsNoTracking().Include(o => o.Warehouse).Include(o => o.Customer)
            .Include(o => o.SalesOrderLines).ThenInclude(l => l.Product)
            .SingleOrDefaultAsync(o => o.SalesOrderId == id && o.CreatedByUserId == actorId, ct);
        if (order == null || order.Status is not ("SUBMITTED" or "PARTIALLY_FULFILLED")
            || !await db.SalesOrderSourcePlans.AnyAsync(p => p.SalesOrderLine.SalesOrderId == id, ct)) return null;
        var missing = await MissingAsync(order, ct);
        var catalog = await sources.GetOptionsAsync(order.WarehouseId, order.RequestedDate, ct: ct);
        return new SalesOrderFormViewModel { Id = id, Code = order.Socode, Status = order.Status, CustomerId = order.CustomerId,
            WarehouseId = order.WarehouseId, RequestedDate = order.RequestedDate, OrderDate = order.OrderDate,
            ConcurrencyToken = Convert.ToBase64String(order.RowVersion), CanCancel = false,
            Warehouses = [new() { Value = order.WarehouseId.ToString(), Text = order.Warehouse.WarehouseName }],
            Lines = order.SalesOrderLines.Where(l => missing[l.SolineId] > 0).Select(l => new SalesOrderLineFormDto {
                SolineId = l.SolineId, ProductId = l.ProductId, ProductCode = l.Product.ProductCode, ProductName = l.Product.ProductName,
                RequestedQuantity = checked((int)missing[l.SolineId]), AvailableQuantity = catalog.Where(s => s.ProductId == l.ProductId && s.CanSelect).Sum(s => s.AvailableQuantity)
            }).ToList() };
    }

    // Only fill an uncovered balance. Original order content and all valid/picked/consumed commitments stay intact.
    public async Task AddReplacementAsync(long id, SalesOrderFormViewModel form, int actorId, CancellationToken ct = default)
    {
        if (form.Lines is null || form.Lines.Count is < 1 or > 100 || form.Lines.Select(l => l.SolineId).Distinct().Count() != form.Lines.Count)
            throw new InvalidOperationException("Danh sách bổ sung nguồn không hợp lệ.");
        var warehouseId = await db.SalesOrders.AsNoTracking().Where(o => o.SalesOrderId == id).Select(o => (int?)o.WarehouseId).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Không tìm thấy SO.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await RequireSalesAsync(actorId, ct);
            await SetAuditAsync(actorId, "Sales confirmed replacement for uncovered source quantity", null, ct);
            await sources.LockWarehouseAsync(warehouseId, ct);
            var order = await db.SalesOrders.FromSqlInterpolated($"SELECT * FROM dbo.SalesOrder WITH (UPDLOCK,HOLDLOCK) WHERE SalesOrderID={id}")
                .Include(o => o.SalesOrderLines).SingleAsync(ct);
            if (order.CreatedByUserId != actorId || order.WarehouseId != warehouseId) throw new UnauthorizedAccessException("Chỉ Sales tạo SO được xác nhận nguồn thay thế.");
            if (order.Status is not ("SUBMITTED" or "PARTIALLY_FULFILLED")) throw new InvalidOperationException("SO không còn mở để bổ sung nguồn.");
            CheckVersion(order.RowVersion, form.ConcurrencyToken);
            if (!await db.SalesOrderSourcePlans.AnyAsync(p => p.SalesOrderLine.SalesOrderId == id, ct))
                throw new InvalidOperationException("SO cũ chưa có kế hoạch nguồn; giữ quy trình xuất hiện có.");
            await sources.ReconcileWarehouseAsync(warehouseId, "Đối soát trước khi xác nhận nguồn thay thế.", ct);
            var missing = await MissingAsync(order, ct);
            var resolved = new List<(int Index, SalesOrderLine Line, long SourceId, int Quantity)>();
            for (int i = 0; i < form.Lines.Count; i++)
            {
                var input = form.Lines[i];
                var line = order.SalesOrderLines.SingleOrDefault(l => l.SolineId == input.SolineId)
                    ?? throw new InvalidOperationException("Dòng bổ sung nguồn không thuộc SO.");
                if (input.Sources is null || input.Sources.Count > 100 || input.Sources.Any(s => s.Quantity <= 0)
                    || input.Sources.Sum(s => (long)s.Quantity) > missing[line.SolineId])
                    throw new SalesOrderSourceException($"Lines[{i}].Sources", "Chỉ được bổ sung tối đa phần đang thiếu; số lượng phải nguyên dương.");
                var used = new HashSet<long>();
                foreach (var choice in input.Sources)
                {
                    var sourceId = await sources.ResolveSourceAsync(warehouseId, choice, ct);
                    if (!used.Add(sourceId)) throw new SalesOrderSourceException($"Lines[{i}].Sources", "Không chọn trùng nguồn trong một sản phẩm.");
                    resolved.Add((i, line, sourceId, choice.Quantity));
                }
            }
            if (resolved.Count == 0) throw new InvalidOperationException("Chọn ít nhất một nguồn thay thế.");
            var catalog = (await sources.GetOptionsAsync(warehouseId, order.RequestedDate, ct: ct))
                .Where(s => s.SupplySourceId > 0).ToDictionary(s => s.SupplySourceId);
            foreach (var allocation in resolved)
            {
                if (!catalog.TryGetValue(allocation.SourceId, out var option) || !option.CanSelect
                    || option.ProductId != allocation.Line.ProductId || allocation.Quantity > option.AvailableQuantity)
                    throw new SalesOrderSourceException($"Lines[{allocation.Index}].Sources", "Nguồn thay thế không còn đủ hoặc không kịp ngày phải xuất.");
                db.Reservations.Add(new Reservation { SalesOrderId = id, SolineId = allocation.Line.SolineId, SupplySourceId = option.SupplySourceId,
                    ProductId = allocation.Line.ProductId, WarehouseId = warehouseId, ItemCondition = "GOOD", SupplyState = option.SupplyState,
                    ReservedQuantity = allocation.Quantity, Status = "ACTIVE", CreatedAt = Now });
                option.AvailableQuantity -= allocation.Quantity;
            }
            await db.SaveChangesAsync(ct);
            await sources.ReconcileWarehouseAsync(warehouseId, "Sales đã xác nhận bổ sung nguồn thay thế.", ct);
            db.Entry(order).Property(o => o.Note).IsModified = true;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch { await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); throw; }
        finally { await ClearAuditAsync(); }
    }

    private async Task<Dictionary<long, long>> MissingAsync(SalesOrder order, CancellationToken ct)
    {
        var issued = await db.OutboundItems.AsNoTracking().Where(i => i.SalesOrderId == order.SalesOrderId && i.IsActive && i.Outbound.Status == "DONE")
            .GroupBy(i => i.SolineId).Select(g => new { Id = g.Key, Quantity = g.LongCount() }).ToDictionaryAsync(x => x.Id, x => x.Quantity, ct);
        var holds = await db.Reservations.AsNoTracking().Where(r => r.SalesOrderId == order.SalesOrderId && r.Status == "ACTIVE" && r.SolineId != null)
            .ToListAsync(ct);
        return order.SalesOrderLines.ToDictionary(l => l.SolineId, l => Math.Max(0, (long)l.RequestedQuantity-l.CancelledQuantity
            -issued.GetValueOrDefault(l.SolineId)-holds.Where(r => r.SolineId == l.SolineId).Sum(SourceReservationService.Balance)));
    }

    private async Task RequireSalesAsync(int actorId, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.UserId == actorId && u.Status == "ACTIVE" && u.Role.RoleName == "SALES_STAFF", ct))
            throw new UnauthorizedAccessException("Tài khoản không còn quyền Sales Staff.");
    }

    private static void CheckVersion(byte[] expected, string? token)
    {
        byte[] supplied;
        try { supplied = Convert.FromBase64String(token ?? ""); }
        catch (FormatException) { throw new DbUpdateConcurrencyException("Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại."); }
        if (supplied.Length != expected.Length || !expected.SequenceEqual(supplied))
            throw new DbUpdateConcurrencyException("SO đã được cập nhật. Vui lòng tải lại form.");
    }

    private Task SetAuditAsync(int actor, string reason, string? request, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($@"
        EXEC sys.sp_set_session_context @key=N'ActorUserID',@value={actor};
        EXEC sys.sp_set_session_context @key=N'AuditReason',@value={reason};
        EXEC sys.sp_set_session_context @key=N'RequestID',@value={request};", ct);

    private async Task ClearAuditAsync()
    {
        try { await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'ActorUserID',@value=NULL; EXEC sys.sp_set_session_context @key=N'AuditReason',@value=NULL; EXEC sys.sp_set_session_context @key=N'RequestID',@value=NULL;"); }
        catch (Exception ex) when (ex is System.Data.Common.DbException or InvalidOperationException)
        { logger.LogDebug(ex, "Cannot clear sales source audit context after transaction completion."); }
    }
}
