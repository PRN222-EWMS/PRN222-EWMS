using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Outbound;

namespace PRN222_EWMS_Web.Services.Warehouse;

public class OutboundService(EwmsDbContext db) : IOutboundService
{
    // =========================================================================
    // 1. DANH SÁCH PHIẾU XUẤT KHO (OUTBOUND LIST)
    // =========================================================================
    public async Task<OutboundListViewModel> GetListAsync(
        int userId,
        string role,
        string? search,
        string? status,
        int? warehouseId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page = 1,
        int pageSize = 10)
    {
        // 1. Kiểm tra phạm vi kho của nhân viên
        int? staffWarehouseId = null;
        if (role == "WAREHOUSE_STAFF")
        {
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            staffWarehouseId = user?.WarehouseId;
        }

        var query = db.Outbounds
            .AsNoTracking()
            .Include(o => o.SalesOrder)
                .ThenInclude(so => so.Warehouse)
            .Include(o => o.SalesOrder)
                .ThenInclude(so => so.Customer)
            .Include(o => o.CreatedByUser)
            .Include(o => o.AssignedToUser)
            .Include(o => o.OutboundItemOutbounds)
            .AsQueryable();

        // 2. Phân quyền theo kho: Warehouse Staff chỉ xem phiếu thuộc kho mình phụ trách
        if (role == "WAREHOUSE_STAFF")
        {
            if (!staffWarehouseId.HasValue)
            {
                return new OutboundListViewModel();
            }
            query = query.Where(o => o.SalesOrder.WarehouseId == staffWarehouseId.Value);
        }
        else if (warehouseId.HasValue && warehouseId.Value > 0)
        {
            query = query.Where(o => o.SalesOrder.WarehouseId == warehouseId.Value);
        }

        // 3. Bộ lọc
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o => o.IssueCode.Contains(term)
                                  || o.SalesOrder.Socode.Contains(term)
                                  || (o.CustomerNameSnapshot != null && o.CustomerNameSnapshot.Contains(term))
                                  || o.SalesOrder.Customer.CustomerName.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            query = query.Where(o => o.Status == st);
        }

        if (fromDate.HasValue)
        {
            var fromDateTime = fromDate.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(o => o.CreatedAt >= fromDateTime);
        }

        if (toDate.HasValue)
        {
            var toDateTime = toDate.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(o => o.CreatedAt <= toDateTime);
        }

        int totalCount = await query.CountAsync();

        var pagedItems = await query
            .OrderByDescending(o => o.OutboundId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OutboundListItemDto
            {
                OutboundId = o.OutboundId,
                IssueCode = o.IssueCode,
                SalesOrderId = o.SalesOrderId,
                Socode = o.SalesOrder.Socode,
                CustomerName = o.CustomerNameSnapshot ?? o.SalesOrder.Customer.CustomerName,
                WarehouseId = o.SalesOrder.WarehouseId,
                WarehouseName = o.SalesOrder.Warehouse.WarehouseName,
                Status = o.Status,
                IsActivePicking = o.IsActivePicking,
                CreatedAt = o.CreatedAt,
                StartedAt = o.StartedAt,
                CompletedAt = o.CompletedAt,
                CreatedByName = o.CreatedByUser.FullName,
                AssignedToName = o.AssignedToUser != null ? o.AssignedToUser.FullName : null,
                ItemCount = o.OutboundItemOutbounds.Count(i => i.IsActive)
            })
            .ToListAsync();

        var warehouses = await db.Warehouses
            .AsNoTracking()
            .Where(w => w.Status == "ACTIVE")
            .OrderBy(w => w.WarehouseName)
            .Select(w => new LookupItemDto { Id = w.WarehouseId, Name = w.WarehouseName })
            .ToListAsync();

        return new OutboundListViewModel
        {
            SearchTerm = search,
            Status = status,
            WarehouseId = warehouseId,
            FromDate = fromDate,
            ToDate = toDate,
            PageIndex = page,
            PageSize = pageSize,
            TotalItems = totalCount,
            Items = pagedItems,
            Warehouses = warehouses,
            CanCreate = (role == "WAREHOUSE_STAFF")
        };
    }

    // =========================================================================
    // 2. CHI TIẾT PHIẾU XUẤT KHO (CHỨC NĂNG 27 - VIEW OUTBOUND RECEIPT DETAILS)
    // =========================================================================
    public async Task<OutboundDetailsViewModel?> GetDetailsAsync(long id, int userId, string role)
    {
        // 1. Kiểm tra phạm vi kho của nhân viên nếu là WAREHOUSE_STAFF
        int? staffWarehouseId = null;
        if (role == "WAREHOUSE_STAFF")
        {
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            staffWarehouseId = user?.WarehouseId;
            if (!staffWarehouseId.HasValue) return null;
        }

        // 2. Nạp phiếu xuất cùng toàn bộ quan hệ
        var outbound = await db.Outbounds
            .AsNoTracking()
            .Include(o => o.SalesOrder)
                .ThenInclude(so => so.Warehouse)
            .Include(o => o.SalesOrder)
                .ThenInclude(so => so.Customer)
            .Include(o => o.SalesOrder)
                .ThenInclude(so => so.SalesOrderLines)
                    .ThenInclude(sol => sol.Product)
            .Include(o => o.CreatedByUser)
            .Include(o => o.AssignedToUser)
            .FirstOrDefaultAsync(o => o.OutboundId == id);

        if (outbound == null) return null;

        // 3. Cô lập theo kho (Warehouse Isolation): Staff chỉ được xem phiếu của kho mình
        if (role == "WAREHOUSE_STAFF" && (!staffWarehouseId.HasValue || outbound.SalesOrder.WarehouseId != staffWarehouseId.Value))
        {
            return null;
        }

        // 4. Lấy tiến độ các dòng từ View vw_SalesOrderProgress
        var progressList = await db.VwSalesOrderProgresses
            .AsNoTracking()
            .Where(p => p.SalesOrderId == outbound.SalesOrderId)
            .ToListAsync();

        var progressDict = progressList.ToDictionary(p => p.SolineId);

        // 5. Lấy danh sách item đã quét trong phiếu Outbound này
        var outboundItems = await db.OutboundItems
            .AsNoTracking()
            .Where(oi => oi.OutboundId == id)
            .Include(oi => oi.SerialItem)
            .Include(oi => oi.SerialItem.Product)
            .ToListAsync();

        // 6. Lấy danh sách PickingClaim hiện tại cho phiếu này (để phân biệt serial đang claim/picking)
        var claims = await db.PickingClaims
            .AsNoTracking()
            .Where(pc => pc.OutboundId == id)
            .Include(pc => pc.ClaimedByUser)
            .ToDictionaryAsync(pc => pc.SerialItemId);

        // Đếm số lượng serial trong phiếu này theo từng dòng SolineId
        var inThisReceiptByLine = outboundItems
            .Where(oi => oi.IsActive)
            .GroupBy(oi => oi.SolineId)
            .ToDictionary(g => g.Key, g => g.Count());

        // 7. Map các dòng sản phẩm của Sales Order
        var lines = outbound.SalesOrder.SalesOrderLines.Select(line =>
        {
            progressDict.TryGetValue(line.SolineId, out var prog);
            inThisReceiptByLine.TryGetValue(line.SolineId, out int thisCount);

            return new OutboundLineProgressDto
            {
                SolineId = line.SolineId,
                ProductId = line.ProductId,
                ProductCode = line.Product.ProductCode,
                ProductName = line.Product.ProductName,
                Model = line.Product.Model,
                Unit = line.Product.Unit,
                RequestedQuantity = line.RequestedQuantity,
                CancelledQuantity = line.CancelledQuantity,
                DoneQuantity = prog?.IssuedQuantity ?? 0,
                RemainingQuantity = prog?.RemainingQuantity ?? (line.RequestedQuantity - line.CancelledQuantity),
                ReservedBalance = prog?.ReservedBalance ?? 0,
                InThisReceiptCount = thisCount
            };
        }).ToList();

        // 8. Map danh sách serials thuộc phiếu
        bool isOutboundDone = (outbound.Status == "DONE");
        var serials = outboundItems.Select(item =>
        {
            claims.TryGetValue(item.SerialItemId, out var claim);
            return new OutboundSerialItemDto
            {
                OutboundItemId = item.OutboundItemId,
                SerialItemId = item.SerialItemId,
                SerialNumber = item.SerialItem.SerialNumber,
                ProductId = item.ProductId,
                ProductCode = item.SerialItem.Product.ProductCode,
                ProductName = item.SerialItem.Product.ProductName,
                ItemCondition = item.ItemCondition,
                ScannedAt = item.ScannedAt,
                IsActive = item.IsActive,
                IsDone = isOutboundDone,
                IsClaimed = claim != null,
                ClaimedByName = claim?.ClaimedByUser?.FullName,
                ClaimedAt = claim?.ClaimedAt
            };
        }).OrderByDescending(s => s.ScannedAt).ToList();

        // 9. Lắp ráp ViewModel hoàn chỉnh (chỉ đọc, không gây side effect)
        var model = new OutboundDetailsViewModel
        {
            OutboundId = outbound.OutboundId,
            IssueCode = outbound.IssueCode,
            Status = outbound.Status,
            IsActivePicking = outbound.IsActivePicking,
            IssueDate = outbound.IssueDate,
            CreatedAt = outbound.CreatedAt,
            StartedAt = outbound.StartedAt,
            CompletedAt = outbound.CompletedAt,
            RecipientName = outbound.RecipientName,
            Note = outbound.Note,

            WarehouseId = outbound.SalesOrder.WarehouseId,
            WarehouseName = outbound.SalesOrder.Warehouse.WarehouseName,
            WarehouseAddress = outbound.SalesOrder.Warehouse.Address,

            CreatedByUserId = outbound.CreatedByUserId,
            CreatedByName = outbound.CreatedByUser.FullName,
            AssignedToUserId = outbound.AssignedToUserId,
            AssignedToName = outbound.AssignedToUser?.FullName,

            CustomerName = outbound.CustomerNameSnapshot ?? outbound.SalesOrder.Customer.CustomerName,
            CustomerPhone = outbound.CustomerPhoneSnapshot ?? outbound.SalesOrder.Customer.Phone,
            CustomerAddress = outbound.CustomerAddressSnapshot ?? outbound.SalesOrder.Customer.Address,

            SalesOrderId = outbound.SalesOrderId,
            Socode = outbound.SalesOrder.Socode,
            SoStatus = outbound.SalesOrder.Status,
            SoOrderDate = outbound.SalesOrder.OrderDate,
            SoRequestedDate = outbound.SalesOrder.RequestedDate,
            SoHasShortage = outbound.SalesOrder.HasShortage,

            Lines = lines,
            Serials = serials
        };

        return model;
    }

    // =========================================================================
    // 3. CHỨC NĂNG 70: TẠO PHIẾU XUẤT KHO & PHIÊN LẤY HÀNG (PICKING WORKBENCH)
    // =========================================================================

    public async Task<List<EligibleSalesOrderDto>> GetEligibleSalesOrdersAsync(int userId)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        if (user?.WarehouseId == null) return [];

        int warehouseId = user.WarehouseId.Value;

        // Lấy danh sách các phiên picking active để hiển thị trạng thái và liên kết tới phiên nhặt dở
        var activeOutbounds = await db.Outbounds
            .AsNoTracking()
            .Where(o => o.IsActivePicking)
            .Select(o => new
            {
                o.SalesOrderId,
                o.OutboundId,
                o.IssueCode,
                PickerName = o.AssignedToUser != null ? o.AssignedToUser.FullName : o.CreatedByUser.FullName
            })
            .ToListAsync();

        var activePickingDict = activeOutbounds
            .GroupBy(o => o.SalesOrderId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.OutboundId).First());

        var orders = await db.SalesOrders
            .AsNoTracking()
            .Include(so => so.Customer)
            .Include(so => so.SalesOrderLines)
                .ThenInclude(sol => sol.Product)
            .Where(so => so.WarehouseId == warehouseId
                && (so.Status == "SUBMITTED" || so.Status == "PARTIALLY_FULFILLED"))
            .OrderByDescending(so => so.SubmittedAt)
            .ToListAsync();

        var result = new List<EligibleSalesOrderDto>();

        foreach (var so in orders)
        {
            var progressList = await db.VwSalesOrderProgresses
                .AsNoTracking()
                .Where(p => p.SalesOrderId == so.SalesOrderId)
                .ToListAsync();

            var progressDict = progressList.ToDictionary(p => p.SolineId);

            var lines = new List<OutboundLineProgressDto>();
            foreach (var sol in so.SalesOrderLines)
            {
                progressDict.TryGetValue(sol.SolineId, out var prog);

                lines.Add(new OutboundLineProgressDto
                {
                    SolineId = sol.SolineId,
                    ProductId = sol.ProductId,
                    ProductCode = sol.Product.ProductCode,
                    ProductName = sol.Product.ProductName,
                    Model = sol.Product.Model,
                    Unit = sol.Product.Unit,
                    RequestedQuantity = sol.RequestedQuantity,
                    CancelledQuantity = sol.CancelledQuantity,
                    DoneQuantity = prog?.IssuedQuantity ?? 0,
                    RemainingQuantity = prog?.RemainingQuantity ?? (sol.RequestedQuantity - sol.CancelledQuantity),
                    ReservedBalance = prog?.ReservedBalance ?? 0,
                    InThisReceiptCount = 0
                });
            }

            long totalRemaining = lines.Sum(l => l.RemainingQuantity);
            if (totalRemaining <= 0) continue; // Đã fulfill xong, bỏ qua

            activePickingDict.TryGetValue(so.SalesOrderId, out var activeOutbound);

            result.Add(new EligibleSalesOrderDto
            {
                SalesOrderId = so.SalesOrderId,
                Socode = so.Socode,
                CustomerName = so.CustomerNameSnapshot ?? so.Customer.CustomerName,
                CustomerPhone = so.Customer.Phone,
                ShippingAddress = so.CustomerAddressSnapshot ?? so.Customer.Address,
                RequestedDate = so.RequestedDate,
                SubmittedAt = so.SubmittedAt ?? so.CreatedAt,
                TotalRequested = lines.Sum(l => l.RequestedQuantity),
                TotalDone = lines.Sum(l => l.DoneQuantity),
                TotalRemaining = totalRemaining,
                TotalReserved = lines.Sum(l => l.ReservedBalance),
                HasShortage = so.HasShortage,
                IsPickingActive = (activeOutbound != null),
                ActiveOutboundId = activeOutbound?.OutboundId,
                ActiveIssueCode = activeOutbound?.IssueCode,
                ActivePickerName = activeOutbound?.PickerName,
                Lines = lines
            });
        }

        return result;
    }

    public async Task<(bool Success, string? Error, long OutboundId)> StartPickingSessionAsync(long salesOrderId, int userId)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        if (user?.WarehouseId == null)
            return (false, "Tài khoản của bạn chưa được liên kết với kho hàng cụ thể.", 0);

        int warehouseId = user.WarehouseId.Value;

        var so = await db.SalesOrders
            .Include(s => s.Customer)
            .Include(s => s.SalesOrderLines)
            .FirstOrDefaultAsync(s => s.SalesOrderId == salesOrderId);

        if (so == null)
            return (false, "Không tìm thấy đơn bán hàng.", 0);

        if (so.WarehouseId != warehouseId)
            return (false, "Bạn chỉ có thể tạo phiếu xuất cho các đơn bán hàng thuộc kho của bạn.", 0);

        if (so.Status != "SUBMITTED" && so.Status != "PARTIALLY_FULFILLED")
            return (false, $"Đơn bán hàng đang ở trạng thái '{so.Status}', không thể tạo phiếu xuất.", 0);

        // Kiểm tra độc quyền phiên picking active
        bool hasActivePicking = await db.Outbounds.AnyAsync(o => o.SalesOrderId == salesOrderId && o.IsActivePicking);
        if (hasActivePicking)
            return (false, "Đơn bán hàng này đang có một phiên lấy hàng đang hoạt động bởi nhân viên khác.", 0);

        // Sinh mã phiếu xuất OUT-yyyy-XXX
        var currentYear = DateTime.UtcNow.Year;
        var prefix = $"OUT-{currentYear}-";
        var lastCode = await db.Outbounds
            .Where(o => o.IssueCode.StartsWith(prefix))
            .OrderByDescending(o => o.IssueCode)
            .Select(o => o.IssueCode)
            .FirstOrDefaultAsync();

        int nextSeq = 1;
        if (lastCode != null && lastCode.Length > prefix.Length && int.TryParse(lastCode[prefix.Length..], out int parsed))
        {
            nextSeq = parsed + 1;
        }
        string issueCode = $"{prefix}{nextSeq:D3}";

        var outbound = new Outbound
        {
            IssueCode = issueCode,
            SalesOrderId = salesOrderId,
            CreatedByUserId = userId,
            AssignedToUserId = userId,
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = "PICKING",
            IsActivePicking = true,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CustomerNameSnapshot = string.IsNullOrWhiteSpace(so.CustomerNameSnapshot) ? so.Customer.CustomerName : so.CustomerNameSnapshot,
            CustomerAddressSnapshot = string.IsNullOrWhiteSpace(so.CustomerAddressSnapshot) ? so.Customer.Address : so.CustomerAddressSnapshot,
            CustomerPhoneSnapshot = so.Customer.Phone
        };

        db.Outbounds.Add(outbound);
        await db.SaveChangesAsync();

        return (true, null, outbound.OutboundId);
    }

    public async Task<PickingWorkbenchViewModel?> GetPickingWorkbenchAsync(long outboundId, int userId)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        if (user?.WarehouseId == null) return null;

        var outbound = await db.Outbounds
            .AsNoTracking()
            .Include(o => o.SalesOrder)
                .ThenInclude(so => so.Customer)
            .Include(o => o.SalesOrder)
                .ThenInclude(so => so.Warehouse)
            .Include(o => o.SalesOrder)
                .ThenInclude(so => so.SalesOrderLines)
                    .ThenInclude(sol => sol.Product)
            .Include(o => o.AssignedToUser)
            .FirstOrDefaultAsync(o => o.OutboundId == outboundId);

        if (outbound == null) return null;

        if (outbound.SalesOrder.WarehouseId != user.WarehouseId.Value)
            return null; // Isolation

        // Lấy tiến độ các dòng từ vw_SalesOrderProgress
        var progressList = await db.VwSalesOrderProgresses
            .AsNoTracking()
            .Where(p => p.SalesOrderId == outbound.SalesOrderId)
            .ToListAsync();
        var progressDict = progressList.ToDictionary(p => p.SolineId);

        // Lấy các serial đã quét trong phiếu này
        var scannedItems = await db.OutboundItems
            .AsNoTracking()
            .Where(oi => oi.OutboundId == outboundId && oi.IsActive)
            .Include(oi => oi.SerialItem)
                .ThenInclude(s => s.Product)
            .ToListAsync();

        var claims = await db.PickingClaims
            .AsNoTracking()
            .Where(c => c.OutboundId == outboundId)
            .Include(c => c.ClaimedByUser)
            .ToDictionaryAsync(c => c.SerialItemId);

        var serialDtos = new List<OutboundSerialItemDto>();
        foreach (var oi in scannedItems)
        {
            claims.TryGetValue(oi.SerialItemId, out var claim);
            serialDtos.Add(new OutboundSerialItemDto
            {
                OutboundItemId = oi.OutboundItemId,
                SerialItemId = oi.SerialItemId,
                SerialNumber = oi.SerialItem.SerialNumber,
                ProductId = oi.ProductId,
                ProductCode = oi.SerialItem.Product.ProductCode,
                ProductName = oi.SerialItem.Product.ProductName,
                ItemCondition = oi.ItemCondition,
                ScannedAt = oi.ScannedAt,
                IsActive = oi.IsActive,
                IsDone = (outbound.Status == "DONE"),
                IsClaimed = (claim != null),
                ClaimedByName = claim?.ClaimedByUser.FullName,
                ClaimedAt = claim?.ClaimedAt
            });
        }

        var lines = new List<OutboundLineProgressDto>();
        foreach (var sol in outbound.SalesOrder.SalesOrderLines)
        {
            progressDict.TryGetValue(sol.SolineId, out var prog);
            int inThisReceipt = scannedItems.Count(i => i.SolineId == sol.SolineId);

            lines.Add(new OutboundLineProgressDto
            {
                SolineId = sol.SolineId,
                ProductId = sol.ProductId,
                ProductCode = sol.Product.ProductCode,
                ProductName = sol.Product.ProductName,
                Model = sol.Product.Model,
                Unit = sol.Product.Unit,
                RequestedQuantity = sol.RequestedQuantity,
                CancelledQuantity = sol.CancelledQuantity,
                DoneQuantity = prog?.IssuedQuantity ?? 0,
                RemainingQuantity = prog?.RemainingQuantity ?? (sol.RequestedQuantity - sol.CancelledQuantity),
                ReservedBalance = prog?.ReservedBalance ?? 0,
                InThisReceiptCount = inThisReceipt
            });
        }

        // Tải vị trí kho để dựng đường dẫn trực quan (Zone > Kệ > Tầng / Ô sàn)
        var allLocations = await db.StorageLocations
            .AsNoTracking()
            .Where(l => l.WarehouseId == outbound.SalesOrder.WarehouseId)
            .ToListAsync();

        var locationDict = allLocations.ToDictionary(l => l.LocationId);

        string BuildLocationPath(int? locId)
        {
            if (!locId.HasValue || !locationDict.TryGetValue(locId.Value, out var loc))
                return "Chưa gán vị trí (Chờ xếp chỗ)";

            var path = new List<string> { loc.LocationName };
            var curr = loc;
            int depth = 0;
            while (curr.ParentLocationId.HasValue && depth < 5)
            {
                if (locationDict.TryGetValue(curr.ParentLocationId.Value, out var parent))
                {
                    path.Insert(0, parent.LocationName);
                    curr = parent;
                    depth++;
                }
                else break;
            }
            return string.Join(" > ", path);
        }

        // Truy vấn gợi ý thiết bị theo nguyên tắc FIFO (Ưu tiên năm sản xuất cũ nhất)
        var productIds = outbound.SalesOrder.SalesOrderLines.Select(l => l.ProductId).Distinct().ToList();

        var availableSerials = await db.SerialItems
            .AsNoTracking()
            .Include(s => s.Product)
            .Where(s => s.CurrentWarehouseId == outbound.SalesOrder.WarehouseId
                     && productIds.Contains(s.ProductId)
                     && s.StockState == "IN_WAREHOUSE"
                     && s.ItemCondition == "GOOD"
                     && !s.IsQuarantined)
            .ToListAsync();

        var activeOtherClaims = (await db.PickingClaims
            .AsNoTracking()
            .Where(c => c.OutboundId != outboundId)
            .Select(c => c.SerialItemId)
            .ToListAsync()).ToHashSet();

        var scannedSerialIds = scannedItems.Select(oi => oi.SerialItemId).ToHashSet();

        // Sắp xếp ưu tiên FIFO: Năm sản xuất cũ nhất -> Tháng cũ nhất -> Ngày nhập kho cũ nhất
        var candidateSerials = availableSerials
            .Where(s => !activeOtherClaims.Contains(s.SerialItemId))
            .OrderBy(s => s.ManufacturingYear ?? 9999)
            .ThenBy(s => s.ManufacturingMonth ?? 99)
            .ThenBy(s => s.RegisteredAt)
            .ToList();

        var earliestSerialIds = candidateSerials
            .Where(s => !scannedSerialIds.Contains(s.SerialItemId))
            .GroupBy(s => s.ProductId)
            .Select(g => g.First().SerialItemId)
            .ToHashSet();

        var suggestedSerials = candidateSerials.Select(s => new SuggestedSerialPickDto
        {
            SerialItemId = s.SerialItemId,
            SerialNumber = s.SerialNumber,
            ProductId = s.ProductId,
            ProductCode = s.Product.ProductCode,
            ProductName = s.Product.ProductName,
            ManufacturingMonth = s.ManufacturingMonth,
            ManufacturingYear = s.ManufacturingYear,
            RegisteredAt = s.RegisteredAt,
            ItemCondition = s.ItemCondition,
            LocationId = s.CurrentLocationId,
            LocationPath = BuildLocationPath(s.CurrentLocationId),
            IsPriority = earliestSerialIds.Contains(s.SerialItemId),
            IsAlreadyPickedInSession = scannedSerialIds.Contains(s.SerialItemId)
        }).ToList();

        return new PickingWorkbenchViewModel
        {
            OutboundId = outbound.OutboundId,
            IssueCode = outbound.IssueCode,
            Status = outbound.Status,
            IsActivePicking = outbound.IsActivePicking,
            StartedAt = outbound.StartedAt,
            SalesOrderId = outbound.SalesOrderId,
            Socode = outbound.SalesOrder.Socode,
            CustomerName = outbound.CustomerNameSnapshot ?? outbound.SalesOrder.Customer.CustomerName,
            CustomerPhone = outbound.CustomerPhoneSnapshot ?? outbound.SalesOrder.Customer.Phone,
            CustomerAddress = outbound.CustomerAddressSnapshot ?? outbound.SalesOrder.Customer.Address,
            WarehouseId = outbound.SalesOrder.WarehouseId,
            WarehouseName = outbound.SalesOrder.Warehouse.WarehouseName,
            AssignedToUserId = outbound.AssignedToUserId ?? userId,
            AssignedToName = outbound.AssignedToUser?.FullName ?? "Tôi",
            Lines = lines,
            ScannedSerials = serialDtos,
            SuggestedSerials = suggestedSerials
        };
    }

    public async Task<(bool Success, string? Error)> ScanSerialAsync(long outboundId, string serialNumber, int userId)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return (false, "Vui lòng nhập hoặc quét mã serial.");

        var trimmedSerial = serialNumber.Trim();

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        if (user?.WarehouseId == null)
            return (false, "Tài khoản chưa được gán kho.");

        var outbound = await db.Outbounds
            .Include(o => o.SalesOrder)
                .ThenInclude(so => so.SalesOrderLines)
            .FirstOrDefaultAsync(o => o.OutboundId == outboundId);

        if (outbound == null)
            return (false, "Không tìm thấy phiếu xuất kho.");

        if (outbound.SalesOrder.WarehouseId != user.WarehouseId.Value)
            return (false, "Bạn không có quyền thao tác trên phiếu xuất của kho khác.");

        if (outbound.Status != "PICKING" && outbound.Status != "DRAFT")
            return (false, $"Phiếu xuất đang ở trạng thái '{outbound.Status}', không thể quét thêm serial.");

        if (!outbound.IsActivePicking)
            return (false, "Phiên lấy hàng này đã bị đóng hoặc tạm dừng.");

        // 1. Tìm SerialItem
        var serial = await db.SerialItems
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.SerialNumber.ToLower() == trimmedSerial.ToLower());

        if (serial == null)
            return (false, $"Không tìm thấy thiết bị có mã serial '{trimmedSerial}' trong hệ thống.");

        // 2. Validate Condition
        if (serial.ItemCondition != "GOOD")
            return (false, $"Thiết bị có tình trạng '{serial.ItemCondition}'. Đơn bán hàng chỉ được xuất hàng GOOD.");

        // 3. Validate StockState
        if (serial.StockState != "IN_WAREHOUSE")
            return (false, $"Thiết bị không ở trạng thái trong kho (Trạng thái hiện tại: {serial.StockState}).");

        // 4. Validate Quarantine
        if (serial.IsQuarantined)
            return (false, "Thiết bị đang bị cách ly kiểm định, không được phép xuất kho.");

        // 5. Validate Warehouse (thuộc đúng kho của SO)
        if (serial.CurrentWarehouseId != outbound.SalesOrder.WarehouseId)
            return (false, $"Thiết bị này không thuộc kho xuất của đơn bán hàng.");

        // 6. Validate Claim độc quyền
        var existingClaim = await db.PickingClaims.FirstOrDefaultAsync(c => c.SerialItemId == serial.SerialItemId);
        if (existingClaim != null)
        {
            if (existingClaim.OutboundId == outboundId)
                return (false, $"Thiết bị '{trimmedSerial}' đã được quét trong phiếu xuất này rồi.");
            return (false, $"Thiết bị '{trimmedSerial}' đang được giữ bởi một phiên xử lý khác.");
        }

        // 7. Validate đã quét trong OutboundItem chưa
        bool alreadyScanned = await db.OutboundItems.AnyAsync(oi => oi.OutboundId == outboundId && oi.SerialItemId == serial.SerialItemId && oi.IsActive);
        if (alreadyScanned)
            return (false, $"Thiết bị '{trimmedSerial}' đã được quét trong phiếu xuất này.");

        // 8. Validate Serial thuộc dòng sản phẩm nào trong SO
        var matchingLine = outbound.SalesOrder.SalesOrderLines.FirstOrDefault(l => l.ProductId == serial.ProductId);
        if (matchingLine == null)
            return (false, $"Sản phẩm '{serial.Product.ProductName}' không có trong đơn bán hàng {outbound.SalesOrder.Socode}.");

        // 9. Validate số lượng còn lại cần xuất cho dòng sản phẩm này
        var progress = await db.VwSalesOrderProgresses
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.SolineId == matchingLine.SolineId);

        long remainingNeeded = progress?.RemainingQuantity ?? (matchingLine.RequestedQuantity - matchingLine.CancelledQuantity);
        int alreadyPickedInSession = await db.OutboundItems
            .CountAsync(oi => oi.OutboundId == outboundId && oi.SolineId == matchingLine.SolineId && oi.IsActive);

        if (alreadyPickedInSession >= remainingNeeded)
            return (false, $"Sản phẩm '{serial.Product.ProductCode}' đã được nhặt đủ số lượng cần xuất ({remainingNeeded}).");

        // 10. Xác định SourceAtIssueId
        long sourceId = serial.CurrentSupplySourceId ?? 0;
        if (sourceId == 0)
        {
            var fallbackSource = await db.SupplySources
                .AsNoTracking()
                .FirstOrDefaultAsync(ss => ss.ProductId == serial.ProductId
                    && ss.WarehouseId == outbound.SalesOrder.WarehouseId
                    && ss.ItemCondition == "GOOD"
                    && ss.SupplyState == "PHYSICAL");
            if (fallbackSource != null)
            {
                sourceId = fallbackSource.SupplySourceId;
            }
        }

        if (sourceId == 0)
            return (false, "Không tìm thấy nguồn cung vật lý (Physical Supply Source) hợp lệ cho thiết bị này.");

        // 11. Ghi nhận Claim & OutboundItem trong transaction
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var claim = new PickingClaim
            {
                SerialItemId = serial.SerialItemId,
                OutboundId = outbound.OutboundId,
                ClaimedByUserId = userId,
                ClaimedAt = DateTime.UtcNow
            };
            db.PickingClaims.Add(claim);

            var outboundItem = new OutboundItem
            {
                OutboundId = outbound.OutboundId,
                SolineId = matchingLine.SolineId,
                SerialItemId = serial.SerialItemId,
                SourceAtIssueId = sourceId,
                ScannedAt = DateTime.UtcNow,
                IsActive = true,
                SalesOrderId = outbound.SalesOrderId,
                ProductId = serial.ProductId,
                WarehouseId = outbound.SalesOrder.WarehouseId,
                ItemCondition = "GOOD",
                SupplyState = "PHYSICAL"
            };
            db.OutboundItems.Add(outboundItem);

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return (false, $"Lỗi hệ thống khi lưu serial: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> RemoveSerialAsync(long outboundId, long serialItemId, int userId)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        if (user?.WarehouseId == null)
            return (false, "Tài khoản chưa được gán kho.");

        var outbound = await db.Outbounds
            .Include(o => o.SalesOrder)
            .FirstOrDefaultAsync(o => o.OutboundId == outboundId);

        if (outbound == null)
            return (false, "Không tìm thấy phiếu xuất kho.");

        if (outbound.SalesOrder.WarehouseId != user.WarehouseId.Value)
            return (false, "Bạn không có quyền thao tác trên phiếu xuất này.");

        if (outbound.Status == "DONE")
            return (false, "Phiếu xuất đã hoàn tất, không thể gỡ bỏ thiết bị.");

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var claim = await db.PickingClaims
                .FirstOrDefaultAsync(c => c.OutboundId == outboundId && c.SerialItemId == serialItemId);
            if (claim != null)
            {
                db.PickingClaims.Remove(claim);
            }

            var item = await db.OutboundItems
                .FirstOrDefaultAsync(oi => oi.OutboundId == outboundId && oi.SerialItemId == serialItemId);
            if (item != null)
            {
                db.OutboundItems.Remove(item);
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return (false, $"Lỗi khi gỡ thiết bị: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> MarkReadyAsync(long outboundId, int userId)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        if (user?.WarehouseId == null)
            return (false, "Tài khoản chưa được gán kho.");

        var outbound = await db.Outbounds
            .Include(o => o.SalesOrder)
            .FirstOrDefaultAsync(o => o.OutboundId == outboundId);

        if (outbound == null)
            return (false, "Không tìm thấy phiếu xuất kho.");

        if (outbound.SalesOrder.WarehouseId != user.WarehouseId.Value)
            return (false, "Bạn không có quyền thao tác trên phiếu xuất này.");

        int activeItemsCount = await db.OutboundItems
            .CountAsync(oi => oi.OutboundId == outboundId && oi.IsActive);

        if (activeItemsCount == 0)
            return (false, "Phiếu xuất chưa có thiết bị nào được quét. Vui lòng quét ít nhất một thiết bị trước khi đánh dấu Sẵn sàng.");

        outbound.Status = "READY";
        outbound.IsActivePicking = true; // CK_Outbound_01 requires IsActivePicking = 1 for READY
        await db.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CancelPickingAsync(long outboundId, int userId)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        if (user?.WarehouseId == null)
            return (false, "Tài khoản chưa được gán kho.");

        var outbound = await db.Outbounds
            .Include(o => o.SalesOrder)
            .FirstOrDefaultAsync(o => o.OutboundId == outboundId);

        if (outbound == null)
            return (false, "Không tìm thấy phiếu xuất kho.");

        if (outbound.SalesOrder.WarehouseId != user.WarehouseId.Value)
            return (false, "Bạn không có quyền thao tác trên phiếu xuất này.");

        if (outbound.Status == "DONE")
            return (false, "Phiếu xuất đã hoàn tất, không thể hủy.");

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Giải phóng toàn bộ claims
            var claims = await db.PickingClaims
                .Where(c => c.OutboundId == outboundId)
                .ToListAsync();
            db.PickingClaims.RemoveRange(claims);

            // Xóa các OutboundItems nháp
            var items = await db.OutboundItems
                .Where(oi => oi.OutboundId == outboundId)
                .ToListAsync();
            db.OutboundItems.RemoveRange(items);

            outbound.Status = "CANCELLED";
            outbound.IsActivePicking = false;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return (false, $"Lỗi khi hủy phiên lấy hàng: {ex.Message}");
        }
    }
}
