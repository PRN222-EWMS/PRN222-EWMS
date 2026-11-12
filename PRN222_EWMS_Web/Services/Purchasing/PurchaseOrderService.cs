using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Services.Warehouse;
using PRN222_EWMS_Web.Repositories.Purchasing;
using PRN222_EWMS_Web.ViewModels.Purchasing;

namespace PRN222_EWMS_Web.Services.Purchasing;

public class PurchaseOrderService(
    IPurchaseOrderRepository repo, 
    EwmsDbContext db, 
    IWarehouseCapacityEngine capacityEngine, ISourceReservationService sources,
    ILogger<PurchaseOrderService> logger) : IPurchaseOrderService
{
    // =========================================================================
    // 1. DANH SÁCH ĐƠN MUA HÀNG (PO LIST)
    // =========================================================================
    public async Task<PoListViewModel> GetOrdersAsync(PoListViewModel filter, int currentUserId, string role)
    {
        // Truy vấn dữ liệu tiến độ tổng hợp từ View CSDL vw_PurchaseOrderProgress
        var progressQuery = repo.Progress;

        // Bắt đầu lọc
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            progressQuery = progressQuery.Where(p => p.Pocode.Contains(term));
        }

        if (filter.WarehouseId.HasValue && filter.WarehouseId > 0)
        {
            progressQuery = progressQuery.Where(p => p.WarehouseId == filter.WarehouseId.Value);
        }

        if (filter.SupplierId.HasValue && filter.SupplierId > 0)
        {
            progressQuery = progressQuery.Where(p => p.SupplierId == filter.SupplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            progressQuery = progressQuery.Where(p => p.Status == filter.Status);
        }

        if (filter.FromDate.HasValue)
        {
            progressQuery = progressQuery.Where(p => p.ExpectedArrivalDate >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            progressQuery = progressQuery.Where(p => p.ExpectedArrivalDate <= filter.ToDate.Value);
        }

        // Lấy danh sách PO ID sau khi gom nhóm từ tiến độ
        var groupedSummary = progressQuery
            .GroupBy(v => new
            {
                v.PurchaseOrderId,
                v.Pocode,
                v.SupplierId,
                v.WarehouseId,
                v.Status,
                v.ExpectedArrivalDate
            })
            .Select(g => new
            {
                g.Key.PurchaseOrderId,
                g.Key.Pocode,
                g.Key.SupplierId,
                g.Key.WarehouseId,
                g.Key.Status,
                g.Key.ExpectedArrivalDate,
                TotalOrdered = g.Sum(x => x.OrderedQuantity),
                TotalClosed = g.Sum(x => x.ClosedQuantity),
                TotalReceived = g.Sum(x => x.ReceivedQuantity),
                TotalRemaining = g.Sum(x => x.RemainingQuantity ?? 0)
            });

        // Nối với bảng PurchaseOrder chính để lấy CreatedByUser, OrderDate, ConfirmedAt
        var mainQuery = from g in groupedSummary
                        join p in repo.Orders on g.PurchaseOrderId equals p.PurchaseOrderId
                        join s in repo.Suppliers on g.SupplierId equals s.SupplierId
                        join w in repo.Warehouses on g.WarehouseId equals w.WarehouseId
                        select new
                        {
                            g.PurchaseOrderId,
                            g.Pocode,
                            SupplierName = s.SupplierName,
                            WarehouseName = w.WarehouseName,
                            CreatedByName = p.CreatedByUser.FullName,
                            p.CreatedByUserId,
                            p.OrderDate,
                            g.ExpectedArrivalDate,
                            g.Status,
                            p.ConfirmedAt,
                            g.TotalOrdered,
                            g.TotalClosed,
                            g.TotalReceived,
                            g.TotalRemaining
                        };

        if (filter.OnlyMyOrders)
        {
            mainQuery = mainQuery.Where(x => x.CreatedByUserId == currentUserId);
        }

        filter.TotalItems = await mainQuery.CountAsync();

        var pagedData = await mainQuery
            .OrderByDescending(x => x.PurchaseOrderId)
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        // Lấy danh sách ID các PO đã có Inbound để xác định cờ CanEdit
        var poIds = pagedData.Select(x => x.PurchaseOrderId).ToList();
        var lockedPoIds = await db.Inbounds
            .AsNoTracking()
            .Where(i => poIds.Contains(i.PurchaseOrderId))
            .Select(i => i.PurchaseOrderId)
            .Distinct()
            .ToListAsync();

        filter.Items = pagedData.Select(x =>
        {
            bool hasInbound = lockedPoIds.Contains(x.PurchaseOrderId);
            bool hasPermission = (role == "MANAGER" || x.CreatedByUserId == currentUserId);
            bool isValidStatus = (x.Status == "DRAFT" || x.Status == "CONFIRMED");
            bool canCancel = hasPermission && isValidStatus && !hasInbound;

            string? cannotCancelReason = null;
            if (!canCancel)
            {
                if (!hasPermission)
                    cannotCancelReason = "Bạn không có quyền hủy đơn hàng này.";
                else if (x.Status == "CANCELLED")
                    cannotCancelReason = "Đơn mua hàng đã bị hủy.";
                else if (x.Status == "CLOSED")
                    cannotCancelReason = "Đơn mua hàng đã đóng.";
                else if (hasInbound || x.Status == "PARTIALLY_RECEIVED" || x.Status == "RECEIVED")
                    cannotCancelReason = "Đơn đã phát sinh phiếu nhận hàng (Inbound), không thể hủy.";
                else
                    cannotCancelReason = $"Đơn hàng ở trạng thái {x.Status} không thể hủy.";
            }

            return new PoListItemDto
            {
                PurchaseOrderId = x.PurchaseOrderId,
                Pocode = x.Pocode,
                SupplierName = x.SupplierName,
                WarehouseName = x.WarehouseName,
                CreatedByName = x.CreatedByName,
                CreatedByUserId = x.CreatedByUserId,
                OrderDate = x.OrderDate,
                ExpectedArrivalDate = x.ExpectedArrivalDate,
                Status = x.Status,
                ConfirmedAt = x.ConfirmedAt,
                TotalOrderedQuantity = x.TotalOrdered,
                TotalClosedQuantity = x.TotalClosed,
                TotalReceivedQuantity = x.TotalReceived,
                TotalRemainingQuantity = x.TotalRemaining,
                CanEdit = role == "PURCHASING_STAFF" && x.CreatedByUserId == currentUserId && x.Status == "DRAFT" && !hasInbound,
                CanCancel = canCancel,
                CannotCancelReason = cannotCancelReason
            };
        }).ToList();

        // Nạp dropdown
        filter.Warehouses = await repo.Warehouses
            .Where(w => w.Status == "ACTIVE")
            .Select(w => new LookupItemDto { Id = w.WarehouseId, Name = w.WarehouseName })
            .ToListAsync();

        filter.Suppliers = await repo.Suppliers
            .Where(s => s.Status == "ACTIVE")
            .Select(s => new LookupItemDto { Id = s.SupplierId, Name = s.SupplierName })
            .ToListAsync();

        filter.CanCreateNew = (role == "PURCHASING_STAFF");

        return filter;
    }

    // =========================================================================
    // 2. CHI TIẾT ĐƠN MUA HÀNG (PO DETAILS)
    // =========================================================================
    public async Task<PoDetailsViewModel?> GetOrderDetailsAsync(long id, int currentUserId, string role)
    {
        var po = await repo.GetDetailsAsync(id);
        if (po == null) return null;

        // Lấy tiến độ từ view cho từng dòng POLine
        var progressDict = await repo.Progress
            .Where(v => v.PurchaseOrderId == id)
            .ToDictionaryAsync(v => v.PolineId, v => new
            {
                Received = v.ReceivedQuantity,
                Remaining = v.RemainingQuantity ?? 0
            });

        bool hasInbound = await repo.HasAnyInboundAsync(id);

        var model = new PoDetailsViewModel
        {
            PurchaseOrderId = po.PurchaseOrderId,
            Pocode = po.Pocode,
            Status = po.Status,
            OrderDate = po.OrderDate,
            ExpectedArrivalDate = po.ExpectedArrivalDate,
            ConfirmedAt = po.ConfirmedAt,
            ClosedAt = po.ClosedAt,
            CloseReason = po.CloseReason,
            Note = po.Note,
            CreatedAt = po.CreatedAt,
            RowVersion = po.RowVersion,

            SupplierId = po.SupplierId,
            SupplierName = po.Supplier.SupplierName,
            SupplierPhone = po.Supplier.Phone,
            SupplierEmail = po.Supplier.Email,
            SupplierAddress = po.Supplier.Address,

            WarehouseId = po.WarehouseId,
            WarehouseName = po.Warehouse.WarehouseName,
            WarehouseType = po.Warehouse.WarehouseType,
            WarehouseAddress = po.Warehouse.Address,

            CreatedByUserId = po.CreatedByUserId,
            CreatedByName = po.CreatedByUser.FullName,

            Lines = po.PurchaseOrderLines.Select(l =>
            {
                progressDict.TryGetValue(l.PolineId, out var prog);
                return new PoLineDetailDto
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
                    ClosedQuantity = l.ClosedQuantity,
                    ReceivedQuantity = prog?.Received ?? 0,
                    RemainingQuantity = prog?.Remaining ?? (l.OrderedQuantity - l.ClosedQuantity)
                };
            }).ToList(),

            Inbounds = po.Inbounds.Select(i => new PoInboundReceiptDto
            {
                InboundId = i.InboundId,
                ReceiptCode = i.ReceiptCode,
                ReceiptDate = i.ReceiptDate,
                Status = i.Status,
                PerformedByName = i.PerformedByUser?.FullName ?? "Chưa chỉ định",
                CompletedAt = i.CompletedAt,
                ItemCount = i.InboundItemInbounds.Count(item => item.IsActive),
                Note = i.Note
            }).OrderByDescending(i => i.InboundId).ToList(),

            // Quyền thao tác
            CanEdit = role == "PURCHASING_STAFF" && po.CreatedByUserId == currentUserId
                      && po.Status == "DRAFT"
                      && !hasInbound,

            CanConfirm = (role == "PURCHASING_STAFF" && po.CreatedByUserId == currentUserId)
                         && po.Status == "DRAFT",

            CanCloseRemainder = (role == "PURCHASING_STAFF" || role == "MANAGER")
                                && (po.Status == "CONFIRMED" || po.Status == "PARTIALLY_RECEIVED")
                                && po.PurchaseOrderLines.Any(l => (l.OrderedQuantity - l.ClosedQuantity) > 0),

            CanCancelDraft = (role == "PURCHASING_STAFF" && po.CreatedByUserId == currentUserId)
                             && po.Status == "DRAFT" && !hasInbound,

            CanCancel = (role == "MANAGER" || po.CreatedByUserId == currentUserId)
                        && (po.Status == "DRAFT" || po.Status == "CONFIRMED")
                        && !hasInbound
        };

        return model;
    }

    // =========================================================================
    // 3. TẠO MỚI ĐƠN MUA HÀNG (CREATE PO)
    // =========================================================================
    public async Task<PoFormViewModel> GetCreateFormAsync(int currentUserId)
    {
        var model = new PoFormViewModel
        {
            ExpectedArrivalDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)),
            Warehouses = await repo.Warehouses
                .Where(w => w.Status == "ACTIVE")
                .Select(w => new LookupItemDto { Id = w.WarehouseId, Name = w.WarehouseName })
                .ToListAsync(),
            Suppliers = await repo.Suppliers
                .Where(s => s.Status == "ACTIVE")
                .Select(s => new LookupItemDto { Id = s.SupplierId, Name = s.SupplierName })
                .ToListAsync()
        };

        return model;
    }

    public async Task<(bool Success, string Message, long PoId)> CreateOrderAsync(PoFormViewModel model, bool confirmNow, int actorId)
    {
        // 1. Validation cơ bản
        if (model.SupplierId <= 0) return (false, "Vui lòng chọn Nhà cung cấp hợp lệ.", 0);
        if (model.WarehouseId <= 0) return (false, "Vui lòng chọn Kho tiếp nhận hợp lệ.", 0);
        if (model.ExpectedArrivalDate < DateOnly.FromDateTime(DateTime.Today))
            return (false, "Ngày dự kiến đến không được nhỏ hơn ngày hiện tại.", 0);

        if (model.Lines == null || model.Lines.Count == 0)
            return (false, "Đơn mua hàng phải có ít nhất một dòng sản phẩm.", 0);

        // Kiểm tra trùng sản phẩm trên các dòng
        var duplicateProduct = model.Lines.GroupBy(l => l.ProductId).FirstOrDefault(g => g.Count() > 1);
        if (duplicateProduct != null)
            return (false, "Mỗi sản phẩm chỉ được xuất hiện một lần trên đơn mua hàng.", 0);

        // Kiểm tra danh mục NCC có cung ứng sản phẩm này không (SupplierProduct check)
        var productIds = model.Lines.Select(l => l.ProductId).ToList();
        var validSupplierProductCount = await repo.SupplierProducts
            .CountAsync(sp => sp.SupplierId == model.SupplierId && productIds.Contains(sp.ProductId));

        if (validSupplierProductCount != productIds.Count)
            return (false, "Một số sản phẩm không nằm trong danh mục cung cấp của Nhà cung cấp này.", 0);

        foreach (var line in model.Lines)
        {
            if (line.OrderedQuantity <= 0)
                return (false, "Số lượng đặt hàng của mỗi sản phẩm phải là số nguyên dương lớn hơn 0.", 0);
        }

        // Capacity is validated once inside the warehouse transaction below.

        // Kiểm tra mã đơn mua (POCode) nếu người dùng nhập
        string poCode;
        if (!string.IsNullOrWhiteSpace(model.Pocode))
        {
            poCode = model.Pocode.Trim();
            if (poCode.Length > 40)
                return (false, "Mã đơn mua không được vượt quá 40 ký tự.", 0);

            // Kiểm tra trùng mã đơn mua: Nếu trùng phải thông báo lỗi, tuyệt đối không tự tạo mới mã đơn!
            var isDuplicate = await repo.Orders.AnyAsync(p => p.Pocode == poCode);
            if (isDuplicate)
                return (false, $"Mã đơn mua '{poCode}' đã tồn tại trong hệ thống. Vui lòng nhập mã khác.", 0);
        }
        else
        {
            poCode = await repo.GenerateNextPoCodeAsync();
            var isDuplicateGenerated = await repo.Orders.AnyAsync(p => p.Pocode == poCode);
            if (isDuplicateGenerated)
                return (false, $"Mã đơn mua tự sinh '{poCode}' đã tồn tại. Vui lòng thử lại.", 0);
        }

        // Bắt đầu Transaction
        using var transaction = await repo.BeginTransactionAsync();
        try
        {
            if (!await IsActorAsync(actorId, "PURCHASING_STAFF")) return (false, "Bạn không có quyền tạo PO.", 0);
            await sources.LockWarehouseAsync(model.WarehouseId);
            var validationError = await ValidateFormAsync(model, null);
            if (validationError != null) return (false, validationError, 0);
            var now = DateTime.UtcNow;

            var po = new PurchaseOrder
            {
                SupplierId = model.SupplierId,
                WarehouseId = model.WarehouseId,
                CreatedByUserId = actorId,
                Pocode = poCode,
                OrderDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)),
                ExpectedArrivalDate = model.ExpectedArrivalDate,
                ConfirmedAt = confirmNow ? now : null,
                Status = confirmNow ? "CONFIRMED" : "DRAFT",
                Note = model.Note?.Trim(),
                CreatedAt = now
            };

            foreach (var line in model.Lines)
            {
                po.PurchaseOrderLines.Add(new PurchaseOrderLine
                {
                    ProductId = line.ProductId,
                    OrderedQuantity = line.OrderedQuantity,
                    ClosedQuantity = 0
                });
            }

            await repo.AddAsync(po, actorId);

            // Nếu Confirm ngay: Sinh nguồn hàng dự kiến PO_INCOMING cho từng dòng POLine
            if (confirmNow)
            {
                foreach (var line in po.PurchaseOrderLines)
                {
                    var source = new SupplySource
                    {
                        ProductId = line.ProductId,
                        WarehouseId = po.WarehouseId,
                        ItemCondition = "GOOD",
                        SourceType = "PO_INCOMING",
                        SupplyState = "INCOMING",
                        PolineId = line.PolineId,
                        EligibleQuantity = line.OrderedQuantity,
                        RemainingQuantity = line.OrderedQuantity,
                        AvailableFrom = po.ExpectedArrivalDate,
                        CreatedAt = now
                    };
                    db.SupplySources.Add(source);
                }

                await repo.SaveChangesWithActorAsync(actorId);
            }

            await transaction.CommitAsync();
            return (true, confirmNow ? "Tạo và xác nhận đơn mua hàng thành công." : "Lưu nháp đơn mua hàng thành công.", po.PurchaseOrderId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(); db.ChangeTracker.Clear();
            logger.LogError(ex, "Cannot create purchase order");
            if (ex.InnerException?.Message.Contains("UX_PurchaseOrder_01") == true || ex.Message.Contains("UX_PurchaseOrder_01"))
            {
                return (false, $"Mã đơn mua '{poCode}' đã tồn tại trong hệ thống. Vui lòng nhập mã khác.", 0);
            }
            return (false, "Không thể lưu PO. Vui lòng tải lại số liệu và thử lại.", 0);
        }
    }

    // =========================================================================
    // 4. CHỈNH SỬA ĐƠN MUA HÀNG (EDIT PO)
    // =========================================================================
    public async Task<PoFormViewModel?> GetEditFormAsync(long id, int currentUserId, string role)
    {
        var po = await repo.FindByIdAsync(id, asNoTracking: true);
        if (po == null) return null;

        // Kiểm tra quyền sửa
        if (role != "PURCHASING_STAFF" || po.CreatedByUserId != currentUserId || po.Status != "DRAFT") return null;

        // Khóa cứng: Chưa từng có Inbound nào
        if (await repo.HasAnyInboundAsync(id)) return null;

        var productList = await (from sp in repo.SupplierProducts
                                 join p in repo.Products on sp.ProductId equals p.ProductId
                                 where sp.SupplierId == po.SupplierId && p.Status == "ACTIVE"
                                 select new ProductLookupDto
                                 {
                                     ProductId = p.ProductId,
                                     ProductCode = p.ProductCode,
                                     ProductName = p.ProductName,
                                     Model = p.Model,
                                     Unit = p.Unit,
                                     SupplierId = sp.SupplierId
                                 }).ToListAsync();

        var model = new PoFormViewModel
        {
            PurchaseOrderId = po.PurchaseOrderId,
            Pocode = po.Pocode,
            SupplierId = po.SupplierId,
            WarehouseId = po.WarehouseId,
            ExpectedArrivalDate = po.ExpectedArrivalDate,
            Note = po.Note,
            Status = po.Status,
            RowVersion = po.RowVersion,
            Lines = po.PurchaseOrderLines.Select(l => new PoFormLineDto
            {
                PolineId = l.PolineId,
                ProductId = l.ProductId,
                OrderedQuantity = l.OrderedQuantity
            }).ToList(),
            Warehouses = await repo.Warehouses.Where(w => w.Status == "ACTIVE")
                .Select(w => new LookupItemDto { Id = w.WarehouseId, Name = w.WarehouseName }).ToListAsync(),
            Suppliers = await repo.Suppliers.Where(s => s.Status == "ACTIVE")
                .Select(s => new LookupItemDto { Id = s.SupplierId, Name = s.SupplierName }).ToListAsync(),
            AvailableProducts = productList,
            CanCancel = (role == "MANAGER" || po.CreatedByUserId == currentUserId)
                        && (po.Status == "DRAFT" || po.Status == "CONFIRMED")
        };

        return model;
    }

    public async Task<(bool Success, string Message)> UpdateOrderAsync(long id, PoFormViewModel model, int actorId, string role)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            if (role != "PURCHASING_STAFF" || !await IsActorAsync(actorId, role)) return (false, "Bạn không có quyền sửa PO.");
            var oldWarehouse = await db.PurchaseOrders.AsNoTracking().Where(o => o.PurchaseOrderId == id).Select(o => (int?)o.WarehouseId).SingleOrDefaultAsync();
            if (oldWarehouse == null) return (false, "Không tìm thấy PO.");
            foreach (var wh in new[] { oldWarehouse.Value, model.WarehouseId }.Distinct().Order()) await sources.LockWarehouseAsync(wh);
            var po = await LockOrderAsync(id);
            if (po == null || po.WarehouseId != oldWarehouse) return (false, "PO đã thay đổi; vui lòng tải lại.");
            if (po.CreatedByUserId != actorId || po.Status != "DRAFT") return (false, "Chỉ người tạo được sửa PO Draft. PO đã Confirm khóa toàn bộ nội dung.");
            if (model.RowVersion == null || !po.RowVersion.SequenceEqual(model.RowVersion)) return (false, "PO đã thay đổi; vui lòng tải lại trang.");
            if (await repo.HasAnyInboundAsync(id) || await db.SupplySources.AnyAsync(s => s.Poline != null && s.Poline.PurchaseOrderId == id))
                return (false, "PO đã có dữ liệu nhận hàng hoặc nguồn cung, không thể sửa Draft.");
            if (model.Lines.Any(l => l.PolineId is > 0 && po.PurchaseOrderLines.All(old => old.PolineId != l.PolineId)))
                return (false, "Dòng sản phẩm không thuộc PO đang sửa.");
            var error = await ValidateFormAsync(model, id);
            if (error != null) return (false, error);
            // Remove unused Draft lines first to avoid duplicate product keys on an update.
            db.PurchaseOrderLines.RemoveRange(po.PurchaseOrderLines);
            await repo.SaveChangesWithActorAsync(actorId);
            po.PurchaseOrderLines.Clear();
            po.SupplierId = model.SupplierId; po.WarehouseId = model.WarehouseId;
            po.ExpectedArrivalDate = model.ExpectedArrivalDate; po.Note = model.Note?.Trim();
            db.Entry(po).Property(o => o.Note).IsModified = true;
            foreach (var line in model.Lines) po.PurchaseOrderLines.Add(new PurchaseOrderLine { ProductId = line.ProductId, OrderedQuantity = line.OrderedQuantity });
            await repo.SaveChangesWithActorAsync(actorId);
            await transaction.CommitAsync();
            return (true, "Đã cập nhật PO Draft.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(); db.ChangeTracker.Clear();
            logger.LogError(ex, "Cannot update purchase order {Id}", id);
            return (false, "Không thể cập nhật PO. Dữ liệu có thể vừa thay đổi; vui lòng tải lại và thử lại.");
        }
    }

    public async Task<(bool Success, string Message)> ConfirmOrderAsync(long id, int actorId, string role)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            if (role != "PURCHASING_STAFF" || !await IsActorAsync(actorId, role)) return (false, "Bạn không có quyền Confirm PO.");
            var warehouseId = await db.PurchaseOrders.AsNoTracking().Where(o => o.PurchaseOrderId == id).Select(o => (int?)o.WarehouseId).SingleOrDefaultAsync();
            if (warehouseId == null) return (false, "Không tìm thấy PO.");
            await sources.LockWarehouseAsync(warehouseId.Value);
            var po = await LockOrderAsync(id);
            if (po == null || po.WarehouseId != warehouseId) return (false, "PO đã thay đổi; vui lòng tải lại.");
            if (po.CreatedByUserId != actorId || po.Status != "DRAFT") return (false, "Chỉ người tạo được Confirm PO Draft.");
            if (await repo.HasAnyInboundAsync(id) || await db.SupplySources.AnyAsync(s => s.Poline != null && s.Poline.PurchaseOrderId == id))
                return (false, "Draft đã có dữ liệu thực hiện; cần đối soát trước khi Confirm.");
            var form = new PoFormViewModel { SupplierId = po.SupplierId, WarehouseId = po.WarehouseId,
                ExpectedArrivalDate = po.ExpectedArrivalDate, Note = po.Note,
                Lines = po.PurchaseOrderLines.Select(l => new PoFormLineDto { ProductId = l.ProductId, OrderedQuantity = l.OrderedQuantity }).ToList() };
            var error = await ValidateFormAsync(form, id);
            if (error != null) return (false, error);
            po.Status = "CONFIRMED"; po.ConfirmedAt = DateTime.UtcNow;
            foreach (var line in po.PurchaseOrderLines) db.SupplySources.Add(new SupplySource {
                ProductId = line.ProductId, WarehouseId = po.WarehouseId, ItemCondition = "GOOD", SourceType = "PO_INCOMING", SupplyState = "INCOMING",
                PolineId = line.PolineId, EligibleQuantity = line.OrderedQuantity, RemainingQuantity = line.OrderedQuantity,
                AvailableFrom = po.ExpectedArrivalDate, CreatedAt = DateTime.UtcNow });
            await repo.SaveChangesWithActorAsync(actorId);
            await transaction.CommitAsync();
            return (true, "Đã Confirm PO. Nguồn incoming được mở và nội dung PO được khóa.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(); db.ChangeTracker.Clear();
            logger.LogError(ex, "Cannot confirm purchase order {Id}", id);
            return (false, "Không thể Confirm PO. Vui lòng tải lại số liệu nguồn/sức chứa và thử lại.");
        }
    }

    private Task<bool> IsActorAsync(int actorId, string role) => db.Users.AsNoTracking()
        .AnyAsync(u => u.UserId == actorId && u.Status == "ACTIVE" && u.Role.RoleName == role);

    private Task<PurchaseOrder?> LockOrderAsync(long id) => db.PurchaseOrders
        .FromSqlInterpolated($"SELECT * FROM dbo.PurchaseOrder WITH (UPDLOCK,HOLDLOCK) WHERE PurchaseOrderID={id}")
        .Include(o => o.PurchaseOrderLines).SingleOrDefaultAsync();

    private async Task<string?> ValidateFormAsync(PoFormViewModel form, long? id)
    {
        if (form.Lines is null || form.Lines.Count is < 1 or > 100 || form.Lines.Any(l => l.ProductId <= 0 || l.OrderedQuantity is < 1 or > 1000000)
            || form.Lines.Select(l => l.ProductId).Distinct().Count() != form.Lines.Count)
            return "PO cần từ 1 đến 100 sản phẩm khác nhau, với số lượng nguyên dương tối đa 1.000.000 mỗi dòng.";
        if (form.Note?.Length > 1000) return "Ghi chú tối đa 1.000 ký tự.";
        if (form.ExpectedArrivalDate < DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))) return "Ngày dự kiến đến không được trong quá khứ.";
        if (!await db.Suppliers.AnyAsync(s => s.SupplierId == form.SupplierId && s.Status == "ACTIVE")) return "Nhà cung cấp không còn hoạt động.";
        var warehouse = await db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.WarehouseId == form.WarehouseId && w.Status == "ACTIVE");
        if (warehouse == null) return "Kho tiếp nhận không còn hoạt động.";
        if (warehouse.WarehouseType == "RENTED" && (warehouse.RentalStartDate is not DateOnly start || warehouse.RentalEndDate is not DateOnly end
            || form.ExpectedArrivalDate < start || form.ExpectedArrivalDate > end)) return "Ngày dự kiến đến nằm ngoài thời hạn thuê kho.";
        var ids = form.Lines.Select(l => l.ProductId).ToArray();
        if (await db.SupplierProducts.CountAsync(sp => sp.SupplierId == form.SupplierId && ids.Contains(sp.ProductId) && sp.Product.Status == "ACTIVE") != ids.Length)
            return "Có sản phẩm đã ngừng hoạt động hoặc không thuộc danh mục của nhà cung cấp.";
        var capacity = await capacityEngine.EvaluateAsync(new()
        {
            WarehouseId = form.WarehouseId, ExpectedArrivalDate = form.ExpectedArrivalDate, ExcludePoId = id,
            Demands = form.Lines.Select(l => new CapacityDemand(l.ProductId, l.OrderedQuantity)).ToList()
        });
        return capacity.IsValid ? null : string.Join(" ", capacity.Errors);
    }

    private async Task ReconcileAsActorAsync(int actorId, int warehouseId, string reason)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'ActorUserID',@value={actorId}");
        try { await sources.ReconcileWarehouseAsync(warehouseId, reason); }
        finally { await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'ActorUserID',@value=NULL;"); }
    }

    // =========================================================================
    // 6. ĐÓNG SỐ LƯỢNG CÒN LẠI (CLOSE REMAINDER)
    // =========================================================================
    public async Task<(bool Success, string Message)> CloseRemainderAsync(long id, string reason, int actorId, string role)
    {
        if (reason?.Trim().Length > 1000) return (false, "Lý do tối đa 1.000 ký tự.");
        if (role is not ("PURCHASING_STAFF" or "MANAGER") || !await IsActorAsync(actorId, role)) return (false, "Bạn không có quyền xử lý PO.");
        await using var transaction = await repo.BeginTransactionAsync();
        try
        {
            var warehouseId = await db.PurchaseOrders.AsNoTracking().Where(o => o.PurchaseOrderId == id).Select(o => (int?)o.WarehouseId).SingleOrDefaultAsync();
            if (warehouseId == null) return (false, "Không tìm thấy PO.");
            await sources.LockWarehouseAsync(warehouseId.Value);
            var po = await LockOrderAsync(id);
            if (po != null && po.WarehouseId != warehouseId) return (false, "PO đã thay đổi; vui lòng tải lại.");

            if (string.IsNullOrWhiteSpace(reason)) return (false, "Nhập lý do đóng phần còn lại.");
            if (await db.Inbounds.AnyAsync(i => i.PurchaseOrderId == id && i.Status != "DONE" && i.Status != "CANCELLED"))
                return (false, "Cần hoàn tất hoặc hủy các phiếu nhập đang xử lý trước khi đóng PO.");
            if (po == null) return (false, "Không tìm thấy đơn mua hàng.");
            if (po.Status != "CONFIRMED" && po.Status != "PARTIALLY_RECEIVED")
                return (false, "Chỉ được đóng phần còn lại của đơn CONFIRMED hoặc PARTIALLY_RECEIVED.");

            var now = DateTime.UtcNow;
            po.ClosedAt = now;
            po.CloseReason = reason.Trim();
            po.Status = "CLOSED";

            // Đóng lượng còn lại trên các dòng và giảm SupplySource Incoming tương ứng
            foreach (var line in po.PurchaseOrderLines)
            {
                var prog = await repo.Progress.FirstOrDefaultAsync(p => p.PolineId == line.PolineId);
                long remaining = prog?.RemainingQuantity ?? (line.OrderedQuantity - line.ClosedQuantity);

                if (remaining > 0)
                {
                    line.ClosedQuantity += (int)remaining;

                    var source = await db.SupplySources.FirstOrDefaultAsync(s => s.PolineId == line.PolineId);
                    if (source != null)
                    {
                        source.RemainingQuantity = 0;
                        source.EligibleQuantity = 0;

                        // Theo MSG-027 và Phân tích SO Mục 9 & Mục 13 (SO21):
                        // Khi đóng phần chưa nhận của PO, lượng incoming bị xóa bỏ hoàn toàn.
                        // Các Sales Order đang có Active Reservation dựa vào nguồn này bị mất nguồn cung -> Bật HasShortage!
                        var affectedReservations = await db.Reservations
                            .Include(r => r.SalesOrder)
                            .Where(r => r.SupplySourceId == source.SupplySourceId && r.Status == "ACTIVE")
                            .ToListAsync();

                        foreach (var res in affectedReservations)
                        {
                            if (res.SalesOrder != null)
                            {
                                res.SalesOrder.HasShortage = true;
                            }
                        }
                    }
                }
            }

            await repo.SaveChangesWithActorAsync(actorId);
            await ReconcileAsActorAsync(actorId, po.WarehouseId, "PO đã hủy/đóng phần chưa nhận; nguồn incoming không còn khả dụng.");
            await transaction.CommitAsync();
            return (true, "Đã đóng số lượng còn lại của đơn mua hàng.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(); db.ChangeTracker.Clear();
            logger.LogError(ex, "Cannot close/cancel purchase order {Id}", id);
            return (false, "Không thể đóng PO. Dữ liệu có thể vừa thay đổi; vui lòng tải lại.");
        }
    }

    // =========================================================================
    // 6.1. HỦY ĐƠN MUA HÀNG (CANCEL PO)
    // =========================================================================
    public async Task<(bool Success, string Message)> CancelOrderAsync(long id, int actorId, string role, string? reason = null)
    {
        if (reason?.Trim().Length > 1000) return (false, "Lý do tối đa 1.000 ký tự.");
        if (role is not ("PURCHASING_STAFF" or "MANAGER") || !await IsActorAsync(actorId, role)) return (false, "Bạn không có quyền xử lý PO.");
        await using var transaction = await repo.BeginTransactionAsync();
        try
        {
            var warehouseId = await db.PurchaseOrders.AsNoTracking().Where(o => o.PurchaseOrderId == id).Select(o => (int?)o.WarehouseId).SingleOrDefaultAsync();
            if (warehouseId == null) return (false, "Không tìm thấy PO.");
            await sources.LockWarehouseAsync(warehouseId.Value);
            var po = await LockOrderAsync(id);
            if (po != null && po.WarehouseId != warehouseId) return (false, "PO đã thay đổi; vui lòng tải lại.");

            if (po == null) return (false, "Không tìm thấy đơn mua hàng.");

            // Quyền: Quản lý (MANAGER) hoặc chính người tạo đơn
            if (role != "MANAGER" && po.CreatedByUserId != actorId)
                return (false, "Bạn không có quyền hủy đơn mua hàng này.");

            // Chỉ được hủy khi đơn ở trạng thái DRAFT hoặc CONFIRMED
            if (po.Status != "DRAFT" && po.Status != "CONFIRMED")
                return (false, "Chỉ được phép hủy đơn hàng ở trạng thái Bản nháp (DRAFT) hoặc Đã xác nhận (CONFIRMED).");

            // Không cho phép hủy nếu đơn đã từng phát sinh Inbound
            if (await repo.HasAnyInboundAsync(id))
                return (false, "Đơn mua hàng đã phát sinh phiếu nhận hàng (Inbound), không thể hủy đơn.");

            var now = DateTime.UtcNow;
            string oldStatus = po.Status;

            po.Status = "CANCELLED";
            po.ClosedAt = now;
            po.CloseReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

            // Đóng toàn bộ các dòng sản phẩm
            foreach (var line in po.PurchaseOrderLines)
            {
                line.ClosedQuantity = line.OrderedQuantity;
            }

            // Nếu đơn đã CONFIRMED, đã có SupplySource PO_INCOMING -> cần thu hồi nguồn cung và cảnh báo thiếu hàng cho các Sales Order liên kết
            if (oldStatus == "CONFIRMED")
            {
                foreach (var line in po.PurchaseOrderLines)
                {
                    var source = await db.SupplySources.FirstOrDefaultAsync(s => s.PolineId == line.PolineId);
                    if (source != null)
                    {
                        source.RemainingQuantity = 0;
                        source.EligibleQuantity = 0;

                        // Tìm các Active Reservation của các Sales Order đang dựa vào nguồn PO_INCOMING này
                        var affectedReservations = await db.Reservations
                            .Include(r => r.SalesOrder)
                            .Where(r => r.SupplySourceId == source.SupplySourceId && r.Status == "ACTIVE")
                            .ToListAsync();

                        foreach (var res in affectedReservations)
                        {
                            if (res.SalesOrder != null)
                            {
                                res.SalesOrder.HasShortage = true;
                            }
                        }
                    }
                }
            }

            await repo.SaveChangesWithActorAsync(actorId);
            await ReconcileAsActorAsync(actorId, po.WarehouseId, "PO đã hủy/đóng phần chưa nhận; nguồn incoming không còn khả dụng.");
            await transaction.CommitAsync();
            return (true, "Đã hủy đơn mua hàng thành công.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(); db.ChangeTracker.Clear();
            logger.LogError(ex, "Cannot close/cancel purchase order {Id}", id);
            return (false, "Không thể hủy PO. Dữ liệu có thể vừa thay đổi; vui lòng tải lại.");
        }
    }

    // =========================================================================
    // 7. LẤY SẢN PHẨM THEO NHÀ CUNG CẤP (AJAX HELPER)
    // =========================================================================
    public async Task<List<ProductLookupDto>> GetProductsBySupplierAsync(int supplierId)
    {
        return await (from sp in repo.SupplierProducts
                      join p in repo.Products on sp.ProductId equals p.ProductId
                      where sp.SupplierId == supplierId && p.Status == "ACTIVE"
                      select new ProductLookupDto
                      {
                          ProductId = p.ProductId,
                          ProductCode = p.ProductCode,
                          ProductName = p.ProductName,
                          Model = p.Model,
                          Unit = p.Unit,
                          SupplierId = sp.SupplierId
                      }).ToListAsync();
    }

    public async Task<bool> IsPoCodeDuplicateAsync(string poCode)
    {
        if (string.IsNullOrWhiteSpace(poCode)) return false;
        var trimmed = poCode.Trim();
        return await repo.Orders.AnyAsync(p => p.Pocode == trimmed);
    }
}
