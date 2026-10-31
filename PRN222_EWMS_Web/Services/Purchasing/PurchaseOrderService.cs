using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Purchasing;
using PRN222_EWMS_Web.ViewModels.Purchasing;

namespace PRN222_EWMS_Web.Services.Purchasing;

public class PurchaseOrderService(IPurchaseOrderRepository repo, EwmsDbContext db) : IPurchaseOrderService
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

        filter.Items = pagedData.Select(x => new PoListItemDto
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
            CanEdit = (role == "MANAGER" || x.CreatedByUserId == currentUserId)
                      && (x.Status == "DRAFT" || x.Status == "CONFIRMED")
                      && !lockedPoIds.Contains(x.PurchaseOrderId)
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
            CanEdit = (role == "MANAGER" || po.CreatedByUserId == currentUserId)
                      && (po.Status == "DRAFT" || po.Status == "CONFIRMED")
                      && !hasInbound,

            CanConfirm = (role == "PURCHASING_STAFF" && po.CreatedByUserId == currentUserId)
                         && po.Status == "DRAFT",

            CanCloseRemainder = (role == "PURCHASING_STAFF" || role == "MANAGER")
                                && (po.Status == "CONFIRMED" || po.Status == "PARTIALLY_RECEIVED")
                                && po.PurchaseOrderLines.Any(l => (l.OrderedQuantity - l.ClosedQuantity) > 0),

            CanCancelDraft = (role == "PURCHASING_STAFF" && po.CreatedByUserId == currentUserId)
                             && po.Status == "DRAFT" && !hasInbound
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
            ExpectedArrivalDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
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

        // Bắt đầu Transaction
        using var transaction = await repo.BeginTransactionAsync();
        try
        {
            var poCode = await repo.GenerateNextPoCodeAsync();
            var now = DateTime.UtcNow;

            var po = new PurchaseOrder
            {
                SupplierId = model.SupplierId,
                WarehouseId = model.WarehouseId,
                CreatedByUserId = actorId,
                Pocode = poCode,
                OrderDate = DateOnly.FromDateTime(DateTime.Today),
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
            await transaction.RollbackAsync();
            return (false, $"Lỗi lưu đơn mua hàng: {ex.Message}", 0);
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
        if (role != "MANAGER" && po.CreatedByUserId != currentUserId) return null;
        if (po.Status != "DRAFT" && po.Status != "CONFIRMED") return null;

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
            AvailableProducts = productList
        };

        return model;
    }

    public async Task<(bool Success, string Message)> UpdateOrderAsync(long id, PoFormViewModel model, int actorId, string role)
    {
        var po = await db.PurchaseOrders
            .Include(p => p.PurchaseOrderLines)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

        if (po == null) return (false, "Không tìm thấy đơn mua hàng yêu cầu.");

        // Kiểm tra quyền
        if (role != "MANAGER" && po.CreatedByUserId != actorId)
            return (false, "Bạn không có quyền chỉnh sửa đơn mua hàng này.");

        if (po.Status != "DRAFT" && po.Status != "CONFIRMED")
            return (false, "Chỉ được phép chỉnh sửa đơn hàng ở trạng thái DRAFT hoặc CONFIRMED.");

        // Khóa cứng: Bắt buộc không có bất kỳ Inbound nào
        if (await repo.HasAnyInboundAsync(id))
            return (false, "Đơn mua hàng đã phát sinh phiếu nhận hàng (Inbound), không thể chỉnh sửa thủ công.");

        // Kiểm tra phiên bản Concurrency
        if (model.RowVersion != null && !po.RowVersion.SequenceEqual(model.RowVersion))
            return (false, "Dữ liệu đơn hàng đã được cập nhật bởi một người dùng khác. Vui lòng tải lại trang.");

        if (model.Lines == null || model.Lines.Count == 0)
            return (false, "Đơn mua hàng phải có ít nhất một dòng sản phẩm.");

        var duplicateProduct = model.Lines.GroupBy(l => l.ProductId).FirstOrDefault(g => g.Count() > 1);
        if (duplicateProduct != null)
            return (false, "Mỗi sản phẩm chỉ được xuất hiện một lần trên đơn mua hàng.");

        using var transaction = await repo.BeginTransactionAsync();
        try
        {
            if (po.Status == "DRAFT")
            {
                // Khi Draft: Được sửa toàn diện
                po.SupplierId = model.SupplierId;
                po.WarehouseId = model.WarehouseId;
                po.ExpectedArrivalDate = model.ExpectedArrivalDate;
                po.Note = model.Note?.Trim();

                // Đồng bộ dòng sản phẩm
                db.PurchaseOrderLines.RemoveRange(po.PurchaseOrderLines);
                foreach (var line in model.Lines)
                {
                    po.PurchaseOrderLines.Add(new PurchaseOrderLine
                    {
                        ProductId = line.ProductId,
                        OrderedQuantity = line.OrderedQuantity,
                        ClosedQuantity = 0
                    });
                }
            }
            else if (po.Status == "CONFIRMED")
            {
                // Khi Confirmed: Chỉ sửa ExpectedArrivalDate, Note và số lượng dòng hiện có
                po.ExpectedArrivalDate = model.ExpectedArrivalDate;
                po.Note = model.Note?.Trim();

                foreach (var lineInput in model.Lines)
                {
                    var existingLine = po.PurchaseOrderLines.FirstOrDefault(l => l.ProductId == lineInput.ProductId);
                    if (existingLine != null)
                    {
                        int oldQty = existingLine.OrderedQuantity;
                        int newQty = lineInput.OrderedQuantity;
                        existingLine.OrderedQuantity = newQty;

                        // Cập nhật lại SupplySource PO_INCOMING tương ứng
                        var supplySource = await db.SupplySources
                            .FirstOrDefaultAsync(s => s.PolineId == existingLine.PolineId);

                        if (supplySource != null)
                        {
                            int diff = newQty - oldQty;
                            supplySource.RemainingQuantity += diff;
                            supplySource.EligibleQuantity += diff;
                            supplySource.AvailableFrom = po.ExpectedArrivalDate;

                            // Nếu giảm lượng làm thiếu nguồn cho các Sales Order đang giữ chỗ:
                            if (diff < 0)
                            {
                                var activeReservations = await db.Reservations
                                    .Include(r => r.SalesOrder)
                                    .Where(r => r.SupplySourceId == supplySource.SupplySourceId && r.Status == "ACTIVE")
                                    .ToListAsync();

                                int totalReserved = activeReservations.Sum(r => r.ReservedQuantity);
                                if (totalReserved > supplySource.RemainingQuantity)
                                {
                                    // Bật cờ HasShortage cho các đơn SO bị ảnh hưởng
                                    foreach (var res in activeReservations.Where(r => r.SalesOrder != null))
                                    {
                                        res.SalesOrder!.HasShortage = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            await repo.SaveChangesWithActorAsync(actorId);
            await transaction.CommitAsync();
            return (true, "Cập nhật đơn mua hàng thành công.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return (false, $"Lỗi cập nhật: {ex.Message}");
        }
    }

    // =========================================================================
    // 5. XÁC NHẬN ĐƠN MUA HÀNG (CONFIRM PO)
    // =========================================================================
    public async Task<(bool Success, string Message)> ConfirmOrderAsync(long id, int actorId, string role)
    {
        var po = await db.PurchaseOrders
            .Include(p => p.PurchaseOrderLines)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

        if (po == null) return (false, "Không tìm thấy đơn mua hàng.");
        if (po.Status != "DRAFT") return (false, "Chỉ được xác nhận đơn mua hàng đang ở trạng thái DRAFT.");

        using var transaction = await repo.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            po.Status = "CONFIRMED";
            po.ConfirmedAt = now;

            // Sinh SupplySource PO_INCOMING
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
                    EligibleQuantity = line.OrderedQuantity - line.ClosedQuantity,
                    RemainingQuantity = line.OrderedQuantity - line.ClosedQuantity,
                    AvailableFrom = po.ExpectedArrivalDate,
                    CreatedAt = now
                };
                db.SupplySources.Add(source);
            }

            await repo.SaveChangesWithActorAsync(actorId);
            await transaction.CommitAsync();
            return (true, "Xác nhận đơn mua hàng thành công.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return (false, $"Lỗi xác nhận đơn: {ex.Message}");
        }
    }

    // =========================================================================
    // 6. ĐÓNG SỐ LƯỢNG CÒN LẠI (CLOSE REMAINDER)
    // =========================================================================
    public async Task<(bool Success, string Message)> CloseRemainderAsync(long id, string reason, int actorId, string role)
    {
        var po = await db.PurchaseOrders
            .Include(p => p.PurchaseOrderLines)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);

        if (po == null) return (false, "Không tìm thấy đơn mua hàng.");
        if (po.Status != "CONFIRMED" && po.Status != "PARTIALLY_RECEIVED")
            return (false, "Chỉ được đóng phần còn lại của đơn CONFIRMED hoặc PARTIALLY_RECEIVED.");

        using var transaction = await repo.BeginTransactionAsync();
        try
        {
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
                    }
                }
            }

            await repo.SaveChangesWithActorAsync(actorId);
            await transaction.CommitAsync();
            return (true, "Đã đóng số lượng còn lại của đơn mua hàng.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return (false, $"Lỗi đóng đơn: {ex.Message}");
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
}
