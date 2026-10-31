using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.WarehouseStaff;

namespace PRN222_EWMS_Web.Repositories.WarehouseStaff;

public class WarehouseStaffRepository(EwmsDbContext db) : IWarehouseStaffRepository
{
    public Task<PRN222_EWMS_Web.Models.User?> GetUserWithWarehouseAsync(int userId)
    {
        return db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.Warehouse)
            .SingleOrDefaultAsync(u => u.UserId == userId);
    }

    public Task<PRN222_EWMS_Web.Models.Warehouse?> GetWarehouseByIdAsync(int warehouseId)
    {
        return db.Warehouses
            .AsNoTracking()
            .SingleOrDefaultAsync(w => w.WarehouseId == warehouseId);
    }

    public async Task<WarehouseDashboardViewModel> GetDashboardMetricsAsync(int warehouseId, int userId)
    {
        var wh = await db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.WarehouseId == warehouseId);
        var vm = new WarehouseDashboardViewModel
        {
            WarehouseId = warehouseId,
            WarehouseName = wh?.WarehouseName ?? "Không xác định",
            WarehouseType = wh?.WarehouseType ?? "",
            Address = wh?.Address ?? "",
            RentalStartDate = wh?.RentalStartDate,
            RentalEndDate = wh?.RentalEndDate
        };

        // Kiểm tra hạn thuê kho
        if (wh != null && string.Equals(wh.WarehouseType, "RENTED", StringComparison.OrdinalIgnoreCase) && wh.RentalEndDate.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var remainingDays = wh.RentalEndDate.Value.DayNumber - today.DayNumber;
            vm.RentalDaysRemaining = remainingDays;
            if (remainingDays <= 30)
            {
                vm.IsRentalExpiringSoon = true;
                vm.LeaseAlertMessage = remainingDays < 0
                    ? $"Hợp đồng thuê kho đã hết hạn vào ngày {wh.RentalEndDate.Value:dd/MM/yyyy}!"
                    : $"Hợp đồng thuê kho sắp hết hạn vào ngày {wh.RentalEndDate.Value:dd/MM/yyyy} (còn {remainingDays} ngày).";
            }
        }

        // 1. Hàng đợi Nhận hàng mua (Inbound Receiving Queue)
        var receivablePoGrouped = await db.VwPurchaseOrderProgresses
            .AsNoTracking()
            .Where(p => p.WarehouseId == warehouseId
                     && (p.Status == "CONFIRMED" || p.Status == "PARTIALLY_RECEIVED"))
            .GroupBy(p => new
            {
                p.PurchaseOrderId,
                p.Pocode,
                p.SupplierId,
                p.Status,
                p.ExpectedArrivalDate
            })
            .Select(g => new
            {
                g.Key.PurchaseOrderId,
                g.Key.Pocode,
                g.Key.SupplierId,
                g.Key.Status,
                g.Key.ExpectedArrivalDate,
                TotalOrdered = g.Sum(x => x.OrderedQuantity),
                TotalReceived = g.Sum(x => x.ReceivedQuantity),
                TotalRemaining = g.Sum(x => x.RemainingQuantity ?? 0)
            })
            .Where(x => x.TotalRemaining > 0)
            .ToListAsync();

        vm.ReceivablePoCount = receivablePoGrouped.Count;
        vm.DraftInboundCount = await db.Inbounds
            .AsNoTracking()
            .CountAsync(i => i.PurchaseOrder.WarehouseId == warehouseId && i.Status == "DRAFT");

        // Top 5 đơn cần nhận sớm nhất
        var top5 = receivablePoGrouped.OrderBy(x => x.ExpectedArrivalDate).Take(5).ToList();
        if (top5.Count > 0)
        {
            var supplierIds = top5.Select(x => x.SupplierId).Distinct().ToList();
            var suppliers = await db.Suppliers
                .AsNoTracking()
                .Where(s => supplierIds.Contains(s.SupplierId))
                .ToDictionaryAsync(s => s.SupplierId, s => s.SupplierName);

            vm.RecentReceivableOrders = top5.Select(x => new WarehousePoListItemDto
            {
                PurchaseOrderId = x.PurchaseOrderId,
                Pocode = x.Pocode,
                SupplierName = suppliers.GetValueOrDefault(x.SupplierId, "Nhà cung cấp"),
                ExpectedArrivalDate = x.ExpectedArrivalDate,
                TotalOrdered = x.TotalOrdered,
                TotalReceived = x.TotalReceived,
                TotalRemaining = x.TotalRemaining,
                Status = x.Status
            }).ToList();
        }

        // 2. Hàng đợi Lưu kho / Xếp kệ (Storage / Putaway Queue)
        vm.AwaitingPutawayCount = await db.SerialItems
            .AsNoTracking()
            .CountAsync(s => s.CurrentWarehouseId == warehouseId && s.StockState == "IN_WAREHOUSE" && s.CurrentLocationId == null);

        // 3. Hàng đợi Xuất hàng (Outbound / Picking Queue)
        vm.PendingPickingOrderCount = await db.SalesOrders
            .AsNoTracking()
            .CountAsync(s => s.WarehouseId == warehouseId && s.Status == "SUBMITTED");

        vm.MyActivePickingCount = await db.Outbounds
            .AsNoTracking()
            .CountAsync(o => o.SalesOrder.WarehouseId == warehouseId && o.IsActivePicking && (o.AssignedToUserId == userId || o.AssignedToUserId == null));

        // 4. Hàng đợi Chuyển kho (Transfer Queue)
        vm.TransferDispatchCount = await db.TransferOrders
            .AsNoTracking()
            .CountAsync(t => t.FromWarehouseId == warehouseId && (t.Status == "APPROVED" || t.Status == "DRAFT"));

        vm.TransferInTransitCount = await db.TransferOrders
            .AsNoTracking()
            .CountAsync(t => t.ToWarehouseId == warehouseId && t.Status == "IN_TRANSIT");

        // 5. Cảnh báo Kiểm kê (Stocktake Alerts)
        var activeStocktakes = await db.Stocktakes
            .AsNoTracking()
            .Where(s => s.WarehouseId == warehouseId && (s.Status == "IN_PROGRESS" || s.Status == "PLANNED"))
            .ToListAsync();
        vm.ActiveStocktakeCount = activeStocktakes.Count;
        vm.ActiveStocktakeCodes = activeStocktakes.Select(s => s.StocktakeCode).ToList();

        return vm;
    }

    public async Task<WarehousePoListViewModel> GetReceivableOrdersAsync(
        int warehouseId,
        string? search,
        DateOnly? fromDate,
        DateOnly? toDate,
        int pageIndex,
        int pageSize)
    {
        var wh = await db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.WarehouseId == warehouseId);
        var vm = new WarehousePoListViewModel
        {
            WarehouseId = warehouseId,
            WarehouseName = wh?.WarehouseName ?? "Kho hàng",
            SearchTerm = search,
            FromDate = fromDate,
            ToDate = toDate,
            PageIndex = Math.Max(1, pageIndex),
            PageSize = pageSize > 0 ? pageSize : 10
        };

        var progressQuery = db.VwPurchaseOrderProgresses
            .AsNoTracking()
            .Where(p => p.WarehouseId == warehouseId
                     && (p.Status == "CONFIRMED" || p.Status == "PARTIALLY_RECEIVED"));

        if (fromDate.HasValue) progressQuery = progressQuery.Where(p => p.ExpectedArrivalDate >= fromDate.Value);
        if (toDate.HasValue) progressQuery = progressQuery.Where(p => p.ExpectedArrivalDate <= toDate.Value);

        var grouped = progressQuery
            .GroupBy(p => new
            {
                p.PurchaseOrderId,
                p.Pocode,
                p.SupplierId,
                p.Status,
                p.ExpectedArrivalDate
            })
            .Select(g => new
            {
                g.Key.PurchaseOrderId,
                g.Key.Pocode,
                g.Key.SupplierId,
                g.Key.Status,
                g.Key.ExpectedArrivalDate,
                TotalOrdered = g.Sum(x => x.OrderedQuantity),
                TotalReceived = g.Sum(x => x.ReceivedQuantity),
                TotalRemaining = g.Sum(x => x.RemainingQuantity ?? 0)
            })
            .Where(x => x.TotalRemaining > 0);

        var joined = from g in grouped
                     join s in db.Suppliers.AsNoTracking() on g.SupplierId equals s.SupplierId
                     select new
                     {
                         g.PurchaseOrderId,
                         g.Pocode,
                         SupplierName = s.SupplierName,
                         g.ExpectedArrivalDate,
                         g.TotalOrdered,
                         g.TotalReceived,
                         g.TotalRemaining,
                         g.Status
                     };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            joined = joined.Where(x => x.Pocode.Contains(s) || x.SupplierName.Contains(s));
        }

        vm.TotalItems = await joined.CountAsync();

        var paged = await joined
            .OrderBy(x => x.ExpectedArrivalDate)
            .Skip((vm.PageIndex - 1) * vm.PageSize)
            .Take(vm.PageSize)
            .ToListAsync();

        vm.Items = paged.Select(x => new WarehousePoListItemDto
        {
            PurchaseOrderId = x.PurchaseOrderId,
            Pocode = x.Pocode,
            SupplierName = x.SupplierName,
            ExpectedArrivalDate = x.ExpectedArrivalDate,
            TotalOrdered = x.TotalOrdered,
            TotalReceived = x.TotalReceived,
            TotalRemaining = x.TotalRemaining,
            Status = x.Status
        }).ToList();

        return vm;
    }

    public async Task<WarehousePoDetailsViewModel?> GetReceivableOrderDetailsAsync(long purchaseOrderId, int warehouseId)
    {
        var po = await db.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.PurchaseOrderLines)
                .ThenInclude(l => l.Product)
                    .ThenInclude(prod => prod.Category)
            .Include(p => p.PurchaseOrderLines)
                .ThenInclude(l => l.Product)
                    .ThenInclude(prod => prod.Brand)
            .Include(p => p.Inbounds)
                .ThenInclude(i => i.PerformedByUser)
            .Include(p => p.Inbounds)
                .ThenInclude(i => i.InboundItemInbounds)
            .SingleOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId);

        // NGUYÊN TẮC CỐT LÕI: CÔ LẬP THEO KHO
        // Nếu đơn hàng không tồn tại hoặc không thuộc kho của thủ kho hiện tại, lập tức từ chối
        if (po == null || po.WarehouseId != warehouseId)
        {
            return null;
        }

        // Lấy tiến độ chi tiết từng dòng từ view
        var progresses = await db.VwPurchaseOrderProgresses
            .AsNoTracking()
            .Where(p => p.PurchaseOrderId == purchaseOrderId)
            .ToDictionaryAsync(p => p.PolineId, p => new
            {
                Received = p.ReceivedQuantity,
                Remaining = p.RemainingQuantity ?? 0
            });

        var lines = po.PurchaseOrderLines.Select(l =>
        {
            progresses.TryGetValue(l.PolineId, out var prog);
            var received = prog?.Received ?? 0;
            var remaining = prog?.Remaining ?? Math.Max(0, l.OrderedQuantity - l.ClosedQuantity - received);

            return new WarehousePoLineDto
            {
                PolineId = l.PolineId,
                ProductId = l.ProductId,
                ProductCode = l.Product.ProductCode,
                ProductName = l.Product.ProductName,
                Model = l.Product.Model,
                Unit = l.Product.Unit,
                CategoryName = l.Product.Category.CategoryName,
                BrandName = l.Product.Brand.BrandName,
                GrossWeightKg = l.Product.GrossWeightKg,
                LengthCm = l.Product.LengthCm,
                WidthCm = l.Product.WidthCm,
                HeightCm = l.Product.HeightCm,
                OrderedQuantity = l.OrderedQuantity,
                ReceivedQuantity = received,
                RemainingQuantity = remaining
            };
        }).ToList();

        var receipts = po.Inbounds
            .OrderByDescending(i => i.ReceiptDate)
            .Select(i => new WarehousePoReceiptDto
            {
                InboundId = i.InboundId,
                ReceiptCode = i.ReceiptCode,
                ReceiptDate = i.ReceiptDate,
                Status = i.Status,
                PerformedByName = i.PerformedByUser?.FullName ?? "Chưa ghi nhận",
                CompletedAt = i.CompletedAt,
                ItemCount = i.InboundItemInbounds.Count(item => item.IsActive),
                Note = i.Note
            }).ToList();

        var totalOrdered = lines.Sum(l => l.OrderedQuantity);
        var totalReceived = lines.Sum(l => l.ReceivedQuantity);
        var totalRemaining = lines.Sum(l => l.RemainingQuantity);

        return new WarehousePoDetailsViewModel
        {
            PurchaseOrderId = po.PurchaseOrderId,
            Pocode = po.Pocode,
            Status = po.Status,
            ExpectedArrivalDate = po.ExpectedArrivalDate,
            OrderDate = po.OrderDate,
            ConfirmedAt = po.ConfirmedAt,
            Note = po.Note,

            SupplierId = po.SupplierId,
            SupplierName = po.Supplier.SupplierName,
            SupplierPhone = po.Supplier.Phone,
            SupplierEmail = po.Supplier.Email,
            SupplierAddress = po.Supplier.Address,

            WarehouseId = po.WarehouseId,
            WarehouseName = po.Warehouse.WarehouseName,
            WarehouseAddress = po.Warehouse.Address,
            WarehouseType = po.Warehouse.WarehouseType,

            TotalOrdered = totalOrdered,
            TotalReceived = totalReceived,
            TotalRemaining = totalRemaining,

            Lines = lines,
            PreviousReceipts = receipts
        };
    }
}
