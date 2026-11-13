using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Warehouse;
using PRN222_EWMS_Web.ViewModels.Outbound;

namespace PRN222_EWMS_Web.Services.Warehouse;

public class OutboundService(IOutboundRepository repo, IOutboundSupplyService supply, ILogger<OutboundService> logger) : IOutboundService
{
    private static readonly string[] OpenStates = ["DRAFT", "PICKING", "READY"];
    private static readonly string[] States = ["DRAFT", "PICKING", "READY", "DONE", "CANCELLED"];

    public async Task<OutboundListViewModel> GetListAsync(int userId, string role, string? search, string? status,
        int? warehouseId, DateOnly? fromDate, DateOnly? toDate, int page = 1, int pageSize = 10)
    {
        var actor = await ReaderAsync(userId, role);
        if (actor is null) return new OutboundListViewModel();
        var isStaff = role == "WAREHOUSE_STAFF";
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        status = States.Contains(status ?? "") ? status : null;
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = repo.Outbounds.AsNoTracking();
        if (isStaff) query = query.Where(o => o.SalesOrder.WarehouseId == actor.WarehouseId);
        else if (warehouseId > 0) query = query.Where(o => o.SalesOrder.WarehouseId == warehouseId);
        if (search is not null) query = query.Where(o => o.IssueCode.Contains(search) || o.SalesOrder.Socode.Contains(search)
            || (o.CustomerNameSnapshot ?? o.SalesOrder.Customer.CustomerName).Contains(search));
        if (status is not null) query = query.Where(o => o.Status == status);
        if (fromDate.HasValue && fromDate.Value > DateOnly.MinValue)
        {
            var from = fromDate.Value.ToDateTime(TimeOnly.MinValue).AddHours(-7);
            query = query.Where(o => o.CreatedAt >= from);
        }
        if (toDate.HasValue && toDate.Value < DateOnly.MaxValue)
        {
            var until = toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7);
            query = query.Where(o => o.CreatedAt < until);
        }
        var count = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(count / (double)pageSize)));
        var items = await query.OrderByDescending(o => o.OutboundId).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new OutboundListItemDto
            {
                OutboundId = o.OutboundId, IssueCode = o.IssueCode, SalesOrderId = o.SalesOrderId,
                Socode = o.SalesOrder.Socode, CustomerName = o.CustomerNameSnapshot ?? o.SalesOrder.Customer.CustomerName,
                WarehouseId = o.SalesOrder.WarehouseId, WarehouseName = o.SalesOrder.Warehouse.WarehouseName,
                Status = o.Status, IsActivePicking = o.IsActivePicking, CreatedAt = o.CreatedAt,
                StartedAt = o.StartedAt, CompletedAt = o.CompletedAt, CreatedByName = o.CreatedByUser.FullName,
                AssignedToName = o.AssignedToUser == null ? null : o.AssignedToUser.FullName,
                ItemCount = o.OutboundItemOutbounds.Count(i => i.IsActive),
                CanWork = isStaff && OpenStates.Contains(o.Status)
            }).ToListAsync();
        return new OutboundListViewModel
        {
            SearchTerm = search, Status = status, WarehouseId = warehouseId, FromDate = fromDate, ToDate = toDate,
            PageIndex = page, PageSize = pageSize, TotalItems = count, Items = items, CanCreate = isStaff,
            Warehouses = await repo.Warehouses.AsNoTracking().Where(w => !isStaff || w.WarehouseId == actor.WarehouseId)
                .OrderBy(w => w.WarehouseName).Select(w => new LookupItemDto { Id = w.WarehouseId, Name = w.WarehouseName }).ToListAsync()
        };
    }

    public async Task<OutboundDetailsViewModel?> GetDetailsAsync(long id, int userId, string role)
    {
        var actor = await ReaderAsync(userId, role);
        if (actor is null) return null;
        var outbound = await DocumentQuery().SingleOrDefaultAsync(o => o.OutboundId == id);
        if (outbound is null || (role == "WAREHOUSE_STAFF" && outbound.SalesOrder.WarehouseId != actor.WarehouseId)) return null;
        var items = await repo.GetItemsAsync(id, false);
        var snapshot = await supply.GetAvailabilityAsync(outbound.SalesOrderId, OpenStates.Contains(outbound.Status) ? id : null);
        foreach (var line in snapshot.Lines) line.InThisReceiptCount = items.Count(i => i.IsActive && i.SolineId == line.SolineId);
        return new OutboundDetailsViewModel
        {
            OutboundId = id, IssueCode = outbound.IssueCode, Status = outbound.Status, IsActivePicking = outbound.IsActivePicking,
            Version = Convert.ToBase64String(outbound.RowVersion), CanWork = role == "WAREHOUSE_STAFF" && OpenStates.Contains(outbound.Status),
            IssueDate = outbound.IssueDate, CreatedAt = outbound.CreatedAt, StartedAt = outbound.StartedAt,
            CompletedAt = outbound.CompletedAt, RecipientName = outbound.RecipientName, Note = outbound.Note,
            WarehouseId = outbound.SalesOrder.WarehouseId, WarehouseName = outbound.SalesOrder.Warehouse.WarehouseName,
            WarehouseAddress = outbound.SalesOrder.Warehouse.Address, CreatedByUserId = outbound.CreatedByUserId,
            CreatedByName = outbound.CreatedByUser.FullName, AssignedToUserId = outbound.AssignedToUserId,
            AssignedToName = outbound.AssignedToUser?.FullName,
            CustomerName = outbound.CustomerNameSnapshot ?? outbound.SalesOrder.Customer.CustomerName,
            CustomerPhone = outbound.CustomerPhoneSnapshot ?? outbound.SalesOrder.Customer.Phone,
            CustomerAddress = outbound.CustomerAddressSnapshot ?? outbound.SalesOrder.Customer.Address,
            SalesOrderId = outbound.SalesOrderId, Socode = outbound.SalesOrder.Socode, SoStatus = outbound.SalesOrder.Status,
            SoOrderDate = outbound.SalesOrder.OrderDate, SoRequestedDate = outbound.SalesOrder.RequestedDate,
            SoHasShortage = snapshot.Lines.Any(line => line.ShortageQuantity > 0), Lines = snapshot.Lines,
            Serials = await MapSerialsAsync(outbound, items)
        };
    }

    public async Task<List<EligibleSalesOrderDto>> GetEligibleSalesOrdersAsync(int userId)
    {
        var actor = await StaffAsync(userId);
        if (actor?.WarehouseId is null) return [];
        var orders = await repo.Orders.AsNoTracking().Include(o => o.Customer)
            .Where(o => o.WarehouseId == actor.WarehouseId && (o.Status == "SUBMITTED" || o.Status == "PARTIALLY_FULFILLED"))
            .OrderBy(o => o.RequestedDate).ThenBy(o => o.SubmittedAt).ThenBy(o => o.SalesOrderId).ToListAsync();
        var ids = orders.Select(o => o.SalesOrderId).ToArray();
        var open = await repo.Outbounds.AsNoTracking().Where(o => ids.Contains(o.SalesOrderId) && OpenStates.Contains(o.Status))
            .Include(o => o.AssignedToUser).OrderByDescending(o => o.IsActivePicking).ThenBy(o => o.OutboundId).ToListAsync();
        var result = new List<EligibleSalesOrderDto>();
        foreach (var order in orders)
        {
            var current = open.FirstOrDefault(o => o.SalesOrderId == order.SalesOrderId);
            var snapshot = await supply.GetAvailabilityAsync(order.SalesOrderId, current?.OutboundId);
            if (snapshot.Lines.Sum(l => l.RemainingQuantity) <= 0) continue;
            result.Add(new EligibleSalesOrderDto
            {
                SalesOrderId = order.SalesOrderId, Socode = order.Socode,
                CustomerName = order.CustomerNameSnapshot ?? order.Customer.CustomerName,
                CustomerPhone = order.CustomerPhoneSnapshot ?? order.Customer.Phone,
                ShippingAddress = order.CustomerAddressSnapshot ?? order.Customer.Address,
                RequestedDate = order.RequestedDate, SubmittedAt = order.SubmittedAt ?? order.CreatedAt,
                HasShortage = snapshot.Lines.Any(l => l.ShortageQuantity > 0),
                TotalRequested = snapshot.Lines.Sum(l => l.RequestedQuantity), TotalDone = snapshot.Lines.Sum(l => l.DoneQuantity),
                TotalRemaining = snapshot.Lines.Sum(l => l.RemainingQuantity), TotalReserved = snapshot.Lines.Sum(l => l.ReservedBalance),
                IsPickingActive = current is not null, ActiveOutboundId = current?.OutboundId,
                ActiveIssueCode = current?.IssueCode, ActivePickerName = current?.AssignedToUser?.FullName, Lines = snapshot.Lines
            });
        }
        return result;
    }

    public async Task<(bool Success, string? Error, long OutboundId)> StartPickingSessionAsync(long salesOrderId, int userId, string? requestToken = null)
    {
        var actor = await StaffAsync(userId);
        if (actor?.WarehouseId is null) return (false, "Bạn phải là Warehouse Staff đang hoạt động và thuộc kho xuất.", 0);
        if (requestToken is not null && !Guid.TryParseExact(requestToken, "N", out _)) return (false, "Yêu cầu tạo phiếu không hợp lệ. Vui lòng tải lại trang.", 0);
        var code = "OUT-" + (requestToken ?? Guid.NewGuid().ToString("N")).ToLowerInvariant();
        try
        {
            return await repo.InTransactionAsync(userId, async () =>
            {
                await supply.LockWarehouseAsync(actor.WarehouseId.Value);
                await EnsureStaffAsync(userId, actor.WarehouseId.Value);
                var order = await repo.OrderForUpdateAsync(salesOrderId);
                if (order is null || order.WarehouseId != actor.WarehouseId) throw new OutboundRuleException("Đơn bán hàng không thuộc kho của bạn.");
                var retry = await repo.Outbounds.SingleOrDefaultAsync(o => o.IssueCode == code);
                if (retry is not null)
                {
                    if (retry.SalesOrderId != salesOrderId) throw new OutboundRuleException("Yêu cầu tạo đã được dùng cho đơn bán khác. Vui lòng tải lại trang.");
                    return (true, (string?)null, retry.OutboundId);
                }
                RequireOpenOrder(order);
                var existing = await repo.Outbounds.Where(o => o.SalesOrderId == salesOrderId && OpenStates.Contains(o.Status))
                    .OrderByDescending(o => o.IsActivePicking).ThenBy(o => o.OutboundId).ToListAsync();
                if (existing.Count > 1) throw new OutboundRuleException("SO có nhiều phiếu đang mở. Cần kiểm tra các phiếu cũ trước khi tạo đợt mới.");
                if (existing.Count == 1) return (true, (string?)null, existing[0].OutboundId);
                await supply.ReconcileWarehouseAsync(order.WarehouseId);
                var snapshot = await supply.GetAvailabilityAsync(salesOrderId);
                if (snapshot.Lines.Sum(l => l.RemainingQuantity) <= 0) throw new OutboundRuleException("SO đã hết số lượng còn phải xuất.");
                if (snapshot.Lines.Sum(l => l.MaxIssueNow) <= 0) throw new OutboundRuleException("Chưa có hàng GOOD hợp lệ để xuất. Hàng sắp về chưa được tính là tồn có thể xuất.");
                var now = DateTime.UtcNow;
                var outbound = new Outbound
                {
                    SalesOrderId = salesOrderId, IssueCode = code, CreatedByUserId = userId, AssignedToUserId = userId,
                    Status = "PICKING", IsActivePicking = true, StartedAt = now, CreatedAt = now,
                    IssueDate = DateOnly.FromDateTime(now.AddHours(7)),
                    CustomerNameSnapshot = order.CustomerNameSnapshot ?? order.Customer.CustomerName,
                    CustomerAddressSnapshot = order.CustomerAddressSnapshot ?? order.Customer.Address,
                    CustomerPhoneSnapshot = order.CustomerPhoneSnapshot ?? order.Customer.Phone
                };
                repo.Add(outbound);
                await repo.SaveAsync();
                return (true, (string?)null, outbound.OutboundId);
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return (false, Failure(ex), 0); }
    }

    public async Task<PickingWorkbenchViewModel?> GetPickingWorkbenchAsync(long outboundId, int userId)
    {
        var actor = await StaffAsync(userId);
        if (actor?.WarehouseId is null) return null;
        var outbound = await DocumentQuery().SingleOrDefaultAsync(o => o.OutboundId == outboundId);
        if (outbound is null || outbound.SalesOrder.WarehouseId != actor.WarehouseId) return null;
        var snapshot = await supply.GetAvailabilityAsync(outbound.SalesOrderId, OpenStates.Contains(outbound.Status) ? outboundId : null);
        var items = await repo.GetItemsAsync(outboundId, false);
        foreach (var line in snapshot.Lines) line.InThisReceiptCount = items.Count(i => i.IsActive && i.SolineId == line.SolineId);
        return new PickingWorkbenchViewModel
        {
            OutboundId = outboundId, IssueCode = outbound.IssueCode, Status = outbound.Status, IsActivePicking = outbound.IsActivePicking,
            Version = Convert.ToBase64String(outbound.RowVersion), StartedAt = outbound.StartedAt,
            SalesOrderId = outbound.SalesOrderId, Socode = outbound.SalesOrder.Socode,
            CustomerName = outbound.CustomerNameSnapshot ?? outbound.SalesOrder.Customer.CustomerName,
            CustomerPhone = outbound.CustomerPhoneSnapshot ?? outbound.SalesOrder.Customer.Phone,
            CustomerAddress = outbound.CustomerAddressSnapshot ?? outbound.SalesOrder.Customer.Address,
            WarehouseId = outbound.SalesOrder.WarehouseId, WarehouseName = outbound.SalesOrder.Warehouse.WarehouseName,
            AssignedToUserId = outbound.AssignedToUserId ?? outbound.CreatedByUserId,
            AssignedToName = outbound.AssignedToUser?.FullName ?? outbound.CreatedByUser.FullName,
            Lines = snapshot.Lines, ScannedSerials = (await MapSerialsAsync(outbound, items)).Where(i => i.IsActive).ToList(),
            SuggestedSerials = snapshot.Candidates
        };
    }

    public Task<(bool Success, string? Error)> ScanSerialAsync(long outboundId, string serialNumber, int userId, string version) =>
        ScanSerialsAsync(outboundId, [serialNumber], userId, version);

    public Task<(bool Success, string? Error)> ScanSerialsAsync(long outboundId, IReadOnlyCollection<string> serialNumbers, int userId, string version) =>
        ChangeAsync(outboundId, userId, version, async (outbound, order) =>
        {
            RequireOpenOrder(order);
            if (serialNumbers.Count is < 1 or > 1000)
                throw new OutboundRuleException("Chọn từ 1 đến 1.000 serial mỗi lượt.");
            var numbers = serialNumbers.Select(number => number?.Trim() ?? "").ToArray();
            if (numbers.Any(number => number.Length is < 1 or > 100 || number.Any(char.IsControl)))
                throw new OutboundRuleException("Serial phải dài 1–100 ký tự và không chứa ký tự điều khiển.");
            if (numbers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != numbers.Length)
                throw new OutboundRuleException("Danh sách có serial trùng. Hãy kiểm tra lại lựa chọn.");

            var snapshot = await supply.GetAvailabilityAsync(order.SalesOrderId, outboundId);
            var candidates = snapshot.Candidates.ToDictionary(candidate => candidate.SerialItemId);
            var chosen = new List<(SerialItem Serial, SuggestedSerialPickDto Candidate, OutboundLineProgressDto Line)>();
            foreach (var number in numbers.Order(StringComparer.OrdinalIgnoreCase))
            {
                var serial = await repo.SerialForUpdateAsync(number)
                    ?? throw new OutboundRuleException($"Không tìm thấy serial {number}.");
                if (!candidates.TryGetValue(serial.SerialItemId, out var candidate) || !candidate.CanSelect
                    || serial.StockState != "IN_WAREHOUSE" || serial.ItemCondition != "GOOD"
                    || serial.CurrentWarehouseId != order.WarehouseId || serial.CurrentSupplyState != "PHYSICAL"
                    || serial.CurrentSupplySourceId != candidate.SupplySourceId)
                    throw new OutboundRuleException($"Serial {number} không còn đủ điều kiện tại nguồn và vị trí đã chọn. Hãy cập nhật danh sách.");
                var line = snapshot.Lines.SingleOrDefault(line => line.ProductId == serial.ProductId)
                    ?? throw new OutboundRuleException("Sản phẩm của serial không thuộc SO.");
                chosen.Add((serial, candidate, line));
            }
            if (chosen.Select(item => item.Serial.SerialItemId).Distinct().Count() != chosen.Count)
                throw new OutboundRuleException("Danh sách đang trỏ đến cùng một serial nhiều lần.");
            foreach (var group in chosen.GroupBy(item => item.Line.SolineId))
                if (group.Count() > group.First().Line.CanPickMoreQuantity)
                    throw new OutboundRuleException($"{group.First().Line.ProductName}: số lượng đã tích vượt hạn mức có thể chọn thêm.");

            var ids = chosen.Select(item => item.Serial.SerialItemId).ToArray();
            if (await repo.Claims.AnyAsync(claim => ids.Contains(claim.SerialItemId)))
                throw new OutboundRuleException("Có serial đã được phiếu này hoặc phiên khác giữ. Chưa thêm danh sách; hãy cập nhật lại.");

            var previousItems = await repo.Items.Where(item => item.OutboundId == outboundId && ids.Contains(item.SerialItemId))
                .OrderByDescending(item => item.OutboundItemId).ToListAsync();
            var previousBySerial = previousItems.GroupBy(item => item.SerialItemId).ToDictionary(group => group.Key, group => group.First());
            if (previousBySerial.Values.Any(item => item.IsActive))
                throw new OutboundRuleException("Có serial đã nằm trong phiếu xuất.");

            foreach (var group in chosen.GroupBy(item => (item.Line.SolineId, item.Candidate.SupplySourceId)))
            {
                var selected = await repo.Items.CountAsync(item => item.OutboundId == outboundId
                    && item.SolineId == group.Key.SolineId && item.SourceAtIssueId == group.Key.SupplySourceId && item.IsActive);
                var ready = group.First().Line.Sources.SingleOrDefault(source =>
                    source.SupplySourceId == group.Key.SupplySourceId && source.SupplyState == "PHYSICAL")?.ReadyQuantity ?? 0;
                if ((long)selected + group.Count() > ready)
                    throw new OutboundRuleException($"{group.First().Line.ProductName}: số lượng đã tích vượt phần nguồn dành cho SO.");
            }

            await ActivateDraftAsync(outbound, order, userId, snapshot);
            foreach (var group in chosen.GroupBy(item => (item.Line.SolineId, item.Candidate.SupplySourceId)))
            {
                var selected = await repo.Items.CountAsync(item => item.OutboundId == outboundId
                    && item.SolineId == group.Key.SolineId && item.SourceAtIssueId == group.Key.SupplySourceId && item.IsActive);
                await supply.EnsureSourceReservationAsync(order.SalesOrderId, group.Key.SolineId,
                    group.Key.SupplySourceId, selected + group.Count());
            }

            var now = DateTime.UtcNow;
            foreach (var (serial, candidate, line) in chosen)
            {
                var previous = previousBySerial.GetValueOrDefault(serial.SerialItemId);
                var item = previous ?? new OutboundItem();
                item.OutboundId = outboundId; item.SalesOrderId = order.SalesOrderId; item.SolineId = line.SolineId;
                item.SerialItemId = serial.SerialItemId; item.ProductId = serial.ProductId; item.WarehouseId = order.WarehouseId;
                item.SourceAtIssueId = candidate.SupplySourceId; item.ItemCondition = "GOOD"; item.SupplyState = "PHYSICAL";
                item.IsActive = true; item.ScannedAt = now;
                if (previous is null) repo.Add(item);
                repo.Add(new PickingClaim { SerialItemId = serial.SerialItemId, OutboundId = outboundId, ClaimedByUserId = userId, ClaimedAt = now });
            }
            outbound.Status = "PICKING"; outbound.IsActivePicking = true; outbound.AssignedToUserId = userId;
            repo.Touch(outbound);
            await repo.SaveAsync();
            await supply.RefreshShortageAsync(order.SalesOrderId);
        });

    public Task<(bool Success, string? Error)> RemoveSerialAsync(long outboundId, long serialItemId, int userId, string version) =>
        ChangeAsync(outboundId, userId, version, async (outbound, order) =>
        {
            var item = await repo.Items.SingleOrDefaultAsync(i => i.OutboundId == outboundId && i.SerialItemId == serialItemId && i.IsActive);
            if (item is null) throw new OutboundRuleException("Serial không còn nằm trong danh sách đang chọn. Vui lòng tải lại trang.");
            item.IsActive = false;
            var claim = await repo.Claims.SingleOrDefaultAsync(c => c.OutboundId == outboundId && c.SerialItemId == serialItemId);
            if (claim is not null) repo.Remove(claim);
            if (outbound.Status == "READY") outbound.Status = "PICKING";
            outbound.AssignedToUserId = userId;
            repo.Touch(outbound);
            await repo.SaveAsync();
            await supply.ReconcileWarehouseAsync(order.WarehouseId);
            await supply.RefreshShortageAsync(order.SalesOrderId);
        });

    public Task<(bool Success, string? Error)> CompleteAsync(long outboundId, int userId, string version) =>
        ChangeAsync(outboundId, userId, version, async (outbound, order) =>
        {
            RequireOpenOrder(order);
            var items = (await repo.GetItemsAsync(outboundId, true)).Where(i => i.IsActive).ToList();
            if (items.Count == 0) throw new OutboundRuleException("Hãy chọn ít nhất một serial trước khi hoàn tất xuất kho.");
            if (items.Select(i => i.SerialItemId).Distinct().Count() != items.Count) throw new OutboundRuleException("Danh sách xuất có serial trùng, chưa thể hoàn tất.");
            var snapshot = await supply.GetAvailabilityAsync(order.SalesOrderId, outboundId);
            await ActivateDraftAsync(outbound, order, userId, snapshot);
            var candidates = snapshot.Candidates.Select(c => c.SerialItemId).ToHashSet();
            foreach (var item in items)
            {
                var serial = await repo.SerialForUpdateAsync(item.SerialItem.SerialNumber)
                    ?? throw new OutboundRuleException("Serial trong phiếu không còn tồn tại.");
                if (!candidates.Contains(serial.SerialItemId) || serial.StockState != "IN_WAREHOUSE" || serial.ItemCondition != "GOOD"
                    || serial.CurrentSupplyState != "PHYSICAL" || serial.CurrentWarehouseId != order.WarehouseId
                    || serial.CurrentSupplySourceId != item.SourceAtIssueId || serial.ProductId != item.ProductId
                    || item.WarehouseId != order.WarehouseId || item.SalesOrderId != order.SalesOrderId
                    || item.ItemCondition != "GOOD" || item.SupplyState != "PHYSICAL")
                    throw new OutboundRuleException($"Serial {serial.SerialNumber} không còn đủ điều kiện xuất. Kiểm tra nguồn, khóa kiểm kê và tình trạng hàng.");
            }
            var claims = await repo.Claims.Where(c => c.OutboundId == outboundId).ToListAsync();
            if (!claims.Select(c => c.SerialItemId).ToHashSet().SetEquals(items.Select(i => i.SerialItemId)))
                throw new OutboundRuleException("Danh sách serial giữ cho phiếu không khớp danh sách xuất. Hãy tải lại và kiểm tra.");
            foreach (var group in items.GroupBy(i => i.SolineId))
            {
                var line = snapshot.Lines.SingleOrDefault(l => l.SolineId == group.Key)
                    ?? throw new OutboundRuleException("Dòng hàng không còn thuộc SO.");
                if (group.Count() > line.RemainingQuantity || group.Count() > line.MaxIssueNow)
                    throw new OutboundRuleException($"Số lượng {line.ProductCode} vượt nhu cầu hoặc hạn mức thực tế có thể xuất.");
                if (group.Any(i => i.ProductId != line.ProductId)) throw new OutboundRuleException("Serial không khớp sản phẩm của dòng SO.");
            }
            foreach (var group in items.GroupBy(i => new { i.SolineId, i.SourceAtIssueId }))
                await supply.EnsureSourceReservationAsync(order.SalesOrderId, group.Key.SolineId, group.Key.SourceAtIssueId, group.Count());
            await repo.SaveAsync();
            var reservations = await repo.ReservationsForUpdateAsync(order.SalesOrderId);
            var now = DateTime.UtcNow;
            foreach (var sourceGroup in items.GroupBy(i => i.SourceAtIssueId))
            {
                var source = await repo.SourceForUpdateAsync(sourceGroup.Key) ?? throw new OutboundRuleException("Nguồn xuất không còn tồn tại.");
                var quantity = sourceGroup.Count();
                if (source.WarehouseId != order.WarehouseId || source.ItemCondition != "GOOD" || source.SupplyState != "PHYSICAL"
                    || source.RemainingQuantity < quantity || source.EligibleQuantity < quantity)
                    throw new OutboundRuleException("Nguồn hàng thực tế không còn đủ số lượng hợp lệ để xuất.");
                var allocation = new DemandAllocation
                {
                    OutboundId = outboundId, SalesOrderId = order.SalesOrderId, SupplySourceId = source.SupplySourceId,
                    Quantity = quantity, AllocatedAt = now
                };
                foreach (var lineGroup in sourceGroup.GroupBy(i => i.SolineId))
                {
                    var needed = lineGroup.Count();
                    foreach (var reservation in reservations.Where(r => r.SolineId == lineGroup.Key && r.SupplySourceId == source.SupplySourceId))
                    {
                        var balance = reservation.ReservedQuantity - reservation.ConsumedQuantity - reservation.ReleasedQuantity;
                        if (balance <= 0) continue;
                        var take = Math.Min(balance, needed);
                        allocation.ReservationConsumptionAllocations.Add(new ReservationConsumption
                        {
                            ReservationId = reservation.ReservationId, SupplySourceId = source.SupplySourceId,
                            SalesOrderId = order.SalesOrderId, ConsumedQuantity = take
                        });
                        reservation.ConsumedQuantity += take;
                        if (take == balance) { reservation.Status = "CLOSED"; reservation.ClosedAt = now; }
                        needed -= take;
                        if (needed == 0) break;
                    }
                    if (needed > 0) throw new OutboundRuleException("Giữ chỗ thực tế của phiếu không đủ tại nguồn đã chọn. Chưa ghi nhận xuất kho.");
                }
                repo.Add(allocation);
                source.RemainingQuantity -= quantity;
                source.EligibleQuantity -= quantity;
            }
            foreach (var item in items)
            {
                var serial = item.SerialItem;
                repo.Add(new SerialMovement
                {
                    SerialItemId = serial.SerialItemId, OutboundId = outboundId, PerformedByUserId = userId,
                    EventType = "SALES_ISSUE", OccurredAt = now, FromState = serial.StockState, ToState = "SOLD",
                    FromCondition = serial.ItemCondition, ToCondition = serial.ItemCondition,
                    FromWarehouseId = serial.CurrentWarehouseId, FromLocationId = serial.CurrentLocationId,
                    FromQuarantined = serial.IsQuarantined, ToQuarantined = false
                });
                serial.StockState = "SOLD"; serial.CurrentWarehouseId = null; serial.CurrentLocationId = null;
                serial.CurrentSupplySourceId = null; serial.CurrentSupplyState = null;
                serial.IsQuarantined = false; serial.QuarantineReason = null;
            }
            foreach (var claim in claims) repo.Remove(claim);
            // Inventory and evidence are flushed before the receipt becomes immutable.
            await repo.SaveAsync();
            outbound.Status = "DONE"; outbound.IsActivePicking = false; outbound.CompletedAt = now;
            outbound.IssueDate = DateOnly.FromDateTime(now.AddHours(7)); outbound.AssignedToUserId = userId;
            outbound.CustomerNameSnapshot ??= order.CustomerNameSnapshot ?? order.Customer.CustomerName;
            await repo.SaveAsync();
            var issued = await repo.Items.Where(i => i.SalesOrderId == order.SalesOrderId && i.IsActive && i.Outbound.Status == "DONE").CountAsync();
            var expected = order.SalesOrderLines.Sum(l => (long)l.RequestedQuantity - l.CancelledQuantity);
            order.Status = issued >= expected ? "FULFILLED" : "PARTIALLY_FULFILLED";
            await repo.SaveAsync();
            await supply.RefreshShortageAsync(order.SalesOrderId);
        }, allowCompleted: true);

    public Task<(bool Success, string? Error)> CancelPickingAsync(long outboundId, int userId, string version) =>
        ChangeAsync(outboundId, userId, version, async (outbound, order) =>
        {
            var claims = await repo.Claims.Where(c => c.OutboundId == outboundId).ToListAsync();
            foreach (var claim in claims) repo.Remove(claim);
            var items = await repo.Items.Where(i => i.OutboundId == outboundId && i.IsActive).ToListAsync();
            foreach (var item in items) item.IsActive = false;
            outbound.Status = "CANCELLED"; outbound.IsActivePicking = false;
            repo.Touch(outbound);
            await repo.SaveAsync();
            await supply.ReconcileWarehouseAsync(order.WarehouseId);
            await supply.RefreshShortageAsync(order.SalesOrderId);
        }, allowCancelled: true);

    private async Task<(bool Success, string? Error)> ChangeAsync(long id, int userId, string version,
        Func<Outbound, SalesOrder, Task> change, bool allowCompleted = false, bool allowCancelled = false)
    {
        var actor = await StaffAsync(userId);
        if (actor?.WarehouseId is null) return (false, "Bạn phải là Warehouse Staff đang hoạt động và thuộc kho xuất.");
        try
        {
            return await repo.InTransactionAsync(userId, async () =>
            {
                await supply.LockWarehouseAsync(actor.WarehouseId.Value);
                await EnsureStaffAsync(userId, actor.WarehouseId.Value);
                var outbound = await repo.OutboundForUpdateAsync(id) ?? throw new OutboundRuleException("Không tìm thấy phiếu xuất.");
                if (outbound.SalesOrder.WarehouseId != actor.WarehouseId) throw new OutboundRuleException("Bạn chỉ được xử lý phiếu tại kho của mình.");
                if (allowCompleted && outbound.Status == "DONE") return (true, (string?)null);
                if (allowCancelled && outbound.Status == "CANCELLED") return (true, (string?)null);
                if (!OpenStates.Contains(outbound.Status)) throw new OutboundRuleException("Phiếu đã khóa, không thể thay đổi.");
                CheckVersion(outbound.RowVersion, version);
                var order = await repo.OrderForUpdateAsync(outbound.SalesOrderId) ?? throw new OutboundRuleException("Không tìm thấy SO của phiếu.");
                await supply.ReconcileWarehouseAsync(order.WarehouseId);
                await change(outbound, order);
                await repo.SaveAsync();
                return (true, (string?)null);
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return (false, Failure(ex)); }
    }

    private async Task ActivateDraftAsync(Outbound outbound, SalesOrder order, int userId, OutboundSupplySnapshot snapshot)
    {
        if (outbound.Status != "DRAFT") return;
        if (await repo.Outbounds.AnyAsync(o => o.SalesOrderId == order.SalesOrderId && o.OutboundId != outbound.OutboundId && o.IsActivePicking))
            throw new OutboundRuleException("SO đang có một đợt lấy hàng khác hoạt động.");
        var eligible = snapshot.Candidates.Select(c => c.SerialItemId).ToHashSet();
        var items = await repo.Items.Where(i => i.OutboundId == outbound.OutboundId && i.IsActive).ToListAsync();
        foreach (var item in items)
        {
            if (!eligible.Contains(item.SerialItemId)) throw new OutboundRuleException("Phiếu nháp cũ có serial không còn hợp lệ. Hãy bỏ serial đó trước khi tiếp tục.");
            var claim = await repo.Claims.SingleOrDefaultAsync(c => c.SerialItemId == item.SerialItemId);
            if (claim is not null && claim.OutboundId != outbound.OutboundId) throw new OutboundRuleException("Serial trong phiếu nháp đang được phiên khác giữ.");
            if (claim is null) repo.Add(new PickingClaim { SerialItemId = item.SerialItemId, OutboundId = outbound.OutboundId, ClaimedByUserId = userId, ClaimedAt = DateTime.UtcNow });
        }
        outbound.Status = "PICKING"; outbound.IsActivePicking = true; outbound.StartedAt ??= DateTime.UtcNow; outbound.AssignedToUserId = userId;
        await repo.SaveAsync();
    }

    private IQueryable<Outbound> DocumentQuery() => repo.Outbounds.AsNoTracking()
        .Include(o => o.SalesOrder).ThenInclude(o => o.Customer)
        .Include(o => o.SalesOrder).ThenInclude(o => o.Warehouse)
        .Include(o => o.CreatedByUser).Include(o => o.AssignedToUser).AsSplitQuery();

    private Task<User?> StaffAsync(int id) => repo.Users.AsNoTracking().Include(u => u.Role)
        .SingleOrDefaultAsync(u => u.UserId == id && u.Status == "ACTIVE" && u.Role.RoleName == "WAREHOUSE_STAFF" && u.WarehouseId != null);

    private async Task<User?> ReaderAsync(int id, string role)
    {
        if (role is not ("WAREHOUSE_STAFF" or "MANAGER")) return null;
        return await repo.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == id && u.Status == "ACTIVE" && u.Role.RoleName == role
            && (role == "MANAGER" || u.WarehouseId != null));
    }

    private async Task EnsureStaffAsync(int userId, int warehouseId)
    {
        if (!await repo.Users.AnyAsync(u => u.UserId == userId && u.Status == "ACTIVE" && u.Role.RoleName == "WAREHOUSE_STAFF" && u.WarehouseId == warehouseId))
            throw new OutboundRuleException("Tài khoản đã đổi kho hoặc không còn quyền thực hiện. Vui lòng tải lại trang.");
    }

    private static void RequireOpenOrder(SalesOrder order)
    {
        if (order.Status is not ("SUBMITTED" or "PARTIALLY_FULFILLED")) throw new OutboundRuleException("SO không còn ở trạng thái được phép xuất hàng.");
    }

    private static void CheckVersion(byte[] current, string version)
    {
        byte[] supplied;
        try { supplied = Convert.FromBase64String(version ?? ""); }
        catch (FormatException) { throw new OutboundRuleException("Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại phiếu."); }
        if (supplied.Length != 8 || !supplied.SequenceEqual(current)) throw new OutboundRuleException("Phiếu vừa được cập nhật. Vui lòng tải lại trước khi tiếp tục.");
    }

    private async Task<List<OutboundSerialItemDto>> MapSerialsAsync(Outbound outbound, List<OutboundItem> items)
    {
        var claims = await repo.Claims.AsNoTracking().Where(c => c.OutboundId == outbound.OutboundId)
            .Include(c => c.ClaimedByUser).ToDictionaryAsync(c => c.SerialItemId);
        var movements = await repo.Movements.AsNoTracking().Where(m => m.OutboundId == outbound.OutboundId && m.EventType == "SALES_ISSUE")
            .Select(m => new { m.SerialItemId, m.FromLocationId }).ToDictionaryAsync(m => m.SerialItemId, m => m.FromLocationId);
        var locations = await repo.Locations.AsNoTracking().Where(l => l.WarehouseId == outbound.SalesOrder.WarehouseId)
            .Select(l => new { l.LocationId, l.ParentLocationId, l.LocationName }).ToListAsync();
        var map = locations.ToDictionary(l => l.LocationId);
        string Path(int? locationId)
        {
            if (!locationId.HasValue) return "Hàng chờ xếp kho";
            var names = new List<string>(); var visited = new HashSet<int>(); var cursor = locationId;
            while (cursor.HasValue && visited.Add(cursor.Value) && map.TryGetValue(cursor.Value, out var location))
            { names.Add(location.LocationName); cursor = location.ParentLocationId; }
            names.Reverse();
            return names.Count > 0 ? string.Join(" → ", names) : $"Vị trí #{locationId}";
        }
        return items.Select(item =>
        {
            claims.TryGetValue(item.SerialItemId, out var claim);
            var issued = outbound.Status == "DONE";
            var locationId = issued ? movements.GetValueOrDefault(item.SerialItemId) : item.SerialItem.CurrentLocationId;
            return new OutboundSerialItemDto
            {
                OutboundItemId = item.OutboundItemId, SerialItemId = item.SerialItemId, SerialNumber = item.SerialItem.SerialNumber,
                ProductId = item.ProductId, ProductCode = item.SerialItem.Product.ProductCode, ProductName = item.SerialItem.Product.ProductName,
                ItemCondition = item.ItemCondition, IsActive = item.IsActive, IsDone = issued && item.IsActive,
                ScannedAt = item.ScannedAt, IsClaimed = claim is not null, ClaimedByName = claim?.ClaimedByUser.FullName,
                ClaimedAt = claim?.ClaimedAt, SourceAtIssueId = item.SourceAtIssueId, SourceCode = SourceCode(item.SourceAtIssue),
                LocationPath = issued && !movements.ContainsKey(item.SerialItemId) ? "Chưa có lịch sử vị trí xuất" : Path(locationId)
            };
        }).ToList();
    }

    private static string SourceCode(SupplySource source) => source.SourceType switch
    {
        "INBOUND" => source.Inbound?.ReceiptCode ?? $"Nhập #{source.InboundId}",
        "RETURN" => source.ReturnReceipt?.ReceiptCode ?? $"Trả #{source.ReturnReceiptId}",
        "TRANSFER" or "TRANSFER_RECEIPT" => source.TransferReceipt?.ReceiptCode ?? $"Nhận chuyển #{source.TransferReceiptId}",
        "ADJUSTMENT" => source.Adjustment?.AdjustmentCode ?? $"Điều chỉnh #{source.AdjustmentId}",
        _ => $"Nguồn #{source.SupplySourceId}"
    };

    private string Failure(Exception error)
    {
        if (error is OutboundRuleException) return error.Message;
        if (error is DbUpdateConcurrencyException) return "Dữ liệu vừa thay đổi. Vui lòng tải lại phiếu trước khi tiếp tục.";
        var sql = error as SqlException ?? (error as DbUpdateException)?.InnerException as SqlException;
        if (sql?.Number is 1205 or 1222) return "Dữ liệu đang được xử lý đồng thời. Vui lòng tải lại và thực hiện lại.";
        if (sql?.Number is 2601 or 2627) return "Serial hoặc phiên lấy hàng vừa được sử dụng. Vui lòng tải lại phiếu.";
        if (error is ArgumentException || (error is InvalidOperationException && !error.Message.Contains("LINQ", StringComparison.OrdinalIgnoreCase))) return error.Message;
        logger.LogError(error, "Outbound operation failed and was rolled back");
        return "Chưa lưu được thao tác xuất kho. Hệ thống chưa ghi nhận thay đổi một phần; vui lòng tải lại phiếu.";
    }

    private sealed class OutboundRuleException(string message) : Exception(message);
}
