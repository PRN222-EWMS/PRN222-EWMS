using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Sales;
using PRN222_EWMS_Web.ViewModels.CustomerReturnOrder;

namespace PRN222_EWMS_Web.Services.Sales;

public class CustomerReturnOrderService(
    ICustomerReturnOrderRepository repo,
    ILogger<CustomerReturnOrderService> logger) : ICustomerReturnOrderService
{
    private static readonly TimeZoneInfo VietnamTime = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    public async Task<ReturnOrderListViewModel> GetReturnOrderListAsync(
        ReturnOrderListFilterViewModel filter,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var (userWarehouseId, userWarehouseName, roleName) = await repo.GetUserRoleAndWarehouseAsync(userId, cancellationToken);
        if (roleName != "SALES_STAFF" && roleName != "WAREHOUSE_STAFF")
        {
            throw new UnauthorizedAccessException("Người dùng không có quyền truy cập danh sách đơn trả hàng.");
        }
        bool isWarehouseStaff = roleName == "WAREHOUSE_STAFF";
        bool isSalesStaff = roleName == "SALES_STAFF";

        int page = Math.Max(1, filter.Page);
        int pageSize = Math.Clamp(filter.PageSize < 1 ? 10 : filter.PageSize, 1, 100);
        int skip = (page - 1) * pageSize;

        var result = new ReturnOrderListViewModel
        {
            Search = filter.Search,
            Status = filter.Status,
            WarehouseId = isWarehouseStaff ? userWarehouseId : filter.WarehouseId,
            FromDate = filter.FromDate,
            ToDate = filter.ToDate,
            Mine = filter.Mine,
            Page = page,
            PageSize = pageSize,
            CanFilterMine = isSalesStaff,
            CanCreate = isSalesStaff,
            IsWarehouseStaff = isWarehouseStaff,
            AssignedWarehouseName = isWarehouseStaff ? userWarehouseName : null
        };

        if (isWarehouseStaff && !userWarehouseId.HasValue)
        {
            result.CanFilterMine = false;
            result.CanCreate = false;
            result.ScopeMessage = "Bạn chưa được phân công kho làm việc. Vui lòng liên hệ quản lý để được phân công kho.";
            return result;
        }

        result.Warehouses = isWarehouseStaff ? [] : await repo.GetAllWarehousesForFilterAsync(cancellationToken);

        if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate.Value > filter.ToDate.Value)
        {
            return result;
        }

        var fromUtc = filter.FromDate.HasValue
            ? TimeZoneInfo.ConvertTimeToUtc(filter.FromDate.Value.ToDateTime(TimeOnly.MinValue), VietnamTime)
            : (DateTime?)null;
        var toUtc = filter.ToDate.HasValue
            ? TimeZoneInfo.ConvertTimeToUtc(filter.ToDate.Value.ToDateTime(TimeOnly.MaxValue), VietnamTime)
            : (DateTime?)null;

        var filterDto = new ReturnOrderFilterDto(
            Search: filter.Search,
            Status: isWarehouseStaff && filter.Status == "DRAFT" ? null : filter.Status,
            WarehouseId: isWarehouseStaff ? userWarehouseId : filter.WarehouseId,
            FromUtc: fromUtc,
            ToUtc: toUtc,
            CreatedByUserId: isSalesStaff && filter.Mine ? userId : null,
            ExcludeDraft: isWarehouseStaff,
            Skip: skip,
            Take: pageSize
        );

        var (items, totalCount) = await repo.GetReturnOrdersPagedAsync(filterDto, cancellationToken);

        result.TotalCount = totalCount;
        result.Items = items.Select(dto => new ReturnOrderListRowViewModel
        {
            Id = dto.ReturnOrderId,
            ReturnCode = dto.ReturnCode,
            SalesOrderId = dto.SalesOrderId,
            SalesOrderCode = dto.SalesOrderCode,
            CustomerName = dto.CustomerName,
            WarehouseId = dto.WarehouseId,
            WarehouseName = dto.WarehouseName,
            CreatedAt = dto.CreatedAt,
            CreatedByUserId = dto.CreatedByUserId,
            CreatedByName = dto.CreatedByName,
            Status = dto.Status,
            LinesCount = dto.LinesCount,
            TotalRequestedQuantity = dto.TotalRequestedQuantity,
            CompletedReceivedQuantity = dto.CompletedReceivedQuantity,
            Reason = dto.Reason,
            CanEdit = isSalesStaff && dto.Status == "DRAFT",
            CanCancel = isSalesStaff && (dto.Status == "DRAFT" || dto.Status == "OPEN") && !dto.HasReturnReceipts,
            CanReceive = isWarehouseStaff && (dto.Status is "OPEN" or "PARTIALLY_RECEIVED") && dto.CompletedReceivedQuantity < dto.TotalRequestedQuantity
        }).ToList();

        return result;
    }

    public async Task<ReturnOrderDetailViewModel?> GetReturnOrderDetailAsync(
        long returnOrderId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (returnOrderId <= 0) return null;

        var (userWarehouseId, _, roleName) = await repo.GetUserRoleAndWarehouseAsync(userId, cancellationToken);
        if (roleName != "SALES_STAFF" && roleName != "WAREHOUSE_STAFF")
        {
            throw new UnauthorizedAccessException("Người dùng không có quyền truy cập thông tin chi tiết đơn trả hàng.");
        }
        bool isWarehouseStaff = roleName == "WAREHOUSE_STAFF";
        bool isSalesStaff = roleName == "SALES_STAFF";

        var order = await repo.FindByIdAsync(returnOrderId, asNoTracking: true, cancellationToken);
        if (order == null) return null;

        // Warehouse Staff: strictly enforce warehouse isolation and DRAFT exclusion
        if (isWarehouseStaff)
        {
            if (!userWarehouseId.HasValue || order.WarehouseId != userWarehouseId.Value || order.Status == "DRAFT")
            {
                return null;
            }
        }

        var lineReceivedCounts = await repo.GetLineReceivedCountsAsync(returnOrderId, cancellationToken);
        var receiptHistory = await repo.GetReturnReceiptHistoryAsync(returnOrderId, cancellationToken);

        var lines = order.CustomerReturnOrderLineReturnOrders
            .OrderBy(line => line.ReturnLineId)
            .Select(line =>
        {
            int receivedQty = lineReceivedCounts.GetValueOrDefault(line.ReturnLineId, 0);
            int remainingQty = Math.Max(0, line.RequestedQuantity - line.CancelledQuantity - receivedQty);
            return new ReturnOrderDetailLineViewModel
            {
                ReturnLineId = line.ReturnLineId,
                SolineId = line.SolineId,
                ProductId = line.ProductId,
                ProductCode = line.Soline?.Product?.ProductCode ?? string.Empty,
                ProductName = line.Soline?.Product?.ProductName ?? string.Empty,
                Unit = line.Soline?.Product?.Unit,
                RequestedQuantity = line.RequestedQuantity,
                CancelledQuantity = line.CancelledQuantity,
                ReceivedQuantity = receivedQty,
                RemainingQuantity = remainingQty
            };
        }).ToList();

        int totalRequested = lines.Sum(l => l.RequestedQuantity);
        int totalCancelled = lines.Sum(l => l.CancelledQuantity);
        int totalReceived = lines.Sum(l => l.ReceivedQuantity);
        int totalRemaining = lines.Sum(l => l.RemainingQuantity);
        int progressPercentage = totalRequested > 0 ? Math.Clamp((int)Math.Round((double)totalReceived * 100 / totalRequested), 0, 100) : 0;

        var receipts = receiptHistory.Select(r => new ReturnReceiptHistoryItemViewModel
        {
            ReturnReceiptId = r.ReturnReceiptId,
            ReceiptCode = r.ReceiptCode,
            ReceiptDate = r.ReceiptDate,
            Status = r.Status,
            CreatedAt = r.CreatedAt,
            CompletedAt = r.CompletedAt,
            PerformedByName = r.PerformedByName,
            ActiveItemCount = r.ActiveItemCount,
            Note = r.Note
        }).ToList();

        return new ReturnOrderDetailViewModel
        {
            ReturnOrderId = order.ReturnOrderId,
            ReturnCode = order.ReturnCode,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            Reason = order.Reason,
            CloseReason = order.CloseReason,
            ClosedAt = order.ClosedAt,

            SalesOrderId = order.SalesOrderId,
            SalesOrderCode = order.SalesOrder?.Socode ?? string.Empty,
            CustomerName = order.SalesOrder?.Customer?.CustomerName ?? "Khách lẻ",
            WarehouseId = order.WarehouseId,
            WarehouseName = order.Warehouse?.WarehouseName ?? string.Empty,
            CreatedByUserId = order.CreatedByUserId,
            CreatedByName = order.CreatedByUser?.FullName ?? string.Empty,

            TotalRequestedQuantity = totalRequested,
            TotalCancelledQuantity = totalCancelled,
            TotalReceivedQuantity = totalReceived,
            TotalRemainingQuantity = totalRemaining,
            ProgressPercentage = progressPercentage,

            IsWarehouseStaff = isWarehouseStaff,
            CanEdit = isSalesStaff && order.Status == "DRAFT",
            CanReceive = isWarehouseStaff && (order.Status is "OPEN" or "PARTIALLY_RECEIVED") && totalReceived < totalRequested,

            Lines = lines,
            Receipts = receipts
        };
    }

    public async Task<ReturnOrderFormModel> GetCreateViewModelAsync(long? selectedSalesOrderId = null, CancellationToken cancellationToken = default)
    {
        var model = new ReturnOrderFormModel();
        await PopulateFormOptionsAsync(model, cancellationToken);

        if (selectedSalesOrderId.HasValue && selectedSalesOrderId.Value > 0)
        {
            model.SalesOrderId = selectedSalesOrderId.Value;
            var so = await repo.GetSalesOrderDetailsAsync(selectedSalesOrderId.Value, cancellationToken);
            if (so != null)
            {
                model.SalesOrderCode = so.Socode;
                model.CustomerName = so.Customer?.CustomerName ?? "Khách lẻ";
                model.WarehouseId = so.WarehouseId;
            }
            model.AvailableLines = await repo.GetEligibleSalesOrderLinesAsync(selectedSalesOrderId.Value, null, cancellationToken);
        }

        return model;
    }

    public Task<List<EligibleSalesOrderLineDto>> GetEligibleLinesAsync(long salesOrderId, CancellationToken cancellationToken = default) =>
        salesOrderId <= 0
            ? Task.FromResult<List<EligibleSalesOrderLineDto>>([])
            : repo.GetEligibleSalesOrderLinesAsync(salesOrderId, null, cancellationToken);

    public async Task<ReturnOrderFormModel?> GetEditViewModelAsync(long returnOrderId, int currentUserId, CancellationToken cancellationToken = default)
    {
        var order = await repo.FindByIdAsync(returnOrderId, asNoTracking: true, cancellationToken);
        if (order is null || order.Status != "DRAFT") return null;

        var model = new ReturnOrderFormModel
        {
            ReturnOrderId = order.ReturnOrderId,
            ReturnCode = order.ReturnCode,
            SalesOrderId = order.SalesOrderId,
            SalesOrderCode = order.SalesOrder?.Socode,
            CustomerName = order.SalesOrder?.Customer?.CustomerName ?? "Khách lẻ",
            WarehouseId = order.WarehouseId,
            Reason = order.Reason,
            RowVersion = order.RowVersion,
            Status = order.Status,
            Action = "SaveDraft"
        };

        await PopulateFormOptionsAsync(model, cancellationToken);

        // Map existing lines
        var eligibleLines = await repo.GetEligibleSalesOrderLinesAsync(order.SalesOrderId, order.ReturnOrderId, cancellationToken);
        var eligibleMap = eligibleLines.ToDictionary(l => l.SolineId);

        foreach (var line in order.CustomerReturnOrderLineReturnOrders.OrderBy(l => l.ReturnLineId))
        {
            eligibleMap.TryGetValue(line.SolineId, out var eligibleDto);
            model.Lines.Add(new ReturnOrderLineItemModel
            {
                ReturnLineId = line.ReturnLineId,
                SolineId = line.SolineId,
                ProductId = line.ProductId,
                ProductCode = eligibleDto?.ProductCode ?? line.Soline?.Product?.ProductCode,
                ProductName = eligibleDto?.ProductName ?? line.Soline?.Product?.ProductName,
                Unit = eligibleDto?.Unit ?? line.Soline?.Product?.Unit,
                ShippedQuantity = eligibleDto?.ShippedQuantity ?? 0,
                EligibleQuantity = eligibleDto?.EligibleQuantity ?? 0,
                RequestedQuantity = line.RequestedQuantity
            });
        }

        return model;
    }

    public async Task<(bool Success, string Message, long ReturnOrderId, string ReturnCode)> CreateReturnOrderAsync(
        ReturnOrderFormModel model,
        int actorId,
        CancellationToken cancellationToken = default)
    {
        // 1. Common Server-side Validation
        var valResult = await ValidateFormModelAsync(model, isCreate: true, cancellationToken);
        if (!valResult.IsValid)
        {
            return (false, valResult.ErrorMessage, 0, string.Empty);
        }

        try
        {
            var createdResult = await repo.InTransactionAsync(actorId, async () =>
            {
                bool isConfirm = string.Equals(model.Action, "SaveAndConfirm", StringComparison.OrdinalIgnoreCase);
                var freshEligibleLines = await LockAndRevalidateEligibilityUnderTxAsync(model.SalesOrderId, null, model.Lines, isConfirm, cancellationToken);

                var order = new CustomerReturnOrder
                {
                    SalesOrderId = model.SalesOrderId,
                    WarehouseId = model.WarehouseId,
                    Reason = string.IsNullOrWhiteSpace(model.Reason) ? null : model.Reason.Trim(),
                    ReturnOrderId = 0
                };

                // Products dictionary from fresh eligibility
                var productMap = freshEligibleLines.ToDictionary(l => l.SolineId, l => l.ProductId);

                var orderLines = model.Lines.Select(l => new CustomerReturnOrderLine
                {
                    SolineId = l.SolineId,
                    ProductId = productMap.GetValueOrDefault(l.SolineId, l.ProductId),
                    RequestedQuantity = l.RequestedQuantity,
                    CancelledQuantity = 0
                }).ToList();

                if (isConfirm)
                {
                    await repo.ConfirmOrderAsync(order, orderLines, null, actorId, cancellationToken);
                    return (order.ReturnOrderId, order.ReturnCode, "OPEN");
                }
                else
                {
                    await repo.SaveDraftAsync(order, orderLines, actorId, cancellationToken);
                    return (order.ReturnOrderId, order.ReturnCode, "DRAFT");
                }
            });

            string successMsg = createdResult.Item3 == "OPEN"
                ? $"Tạo mới và xác nhận đơn trả hàng thành công. Mã đơn: {createdResult.Item2}"
                : $"Lưu bản nháp đơn trả hàng thành công. Mã đơn: {createdResult.Item2}";

            return (true, successMsg, createdResult.Item1, createdResult.Item2);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "Dữ liệu đơn hàng hoặc dòng sản phẩm đã bị thay đổi bởi người dùng khác. Vui lòng tải lại trang.", 0, string.Empty);
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message, 0, string.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi hệ thống khi tạo đơn trả hàng cho SalesOrder ID: {SalesOrderId}", model.SalesOrderId);
            return (false, "Đã xảy ra lỗi trong quá trình xử lý đơn trả hàng. Vui lòng thử lại sau.", 0, string.Empty);
        }
    }

    public async Task<(bool Success, string Message, string ReturnCode)> UpdateDraftOrderAsync(
        long returnOrderId,
        ReturnOrderFormModel model,
        int actorId,
        CancellationToken cancellationToken = default)
    {
        model.ReturnOrderId = returnOrderId;

        // 1. Common Server-side Validation
        var valResult = await ValidateFormModelAsync(model, isCreate: false, cancellationToken);
        if (!valResult.IsValid)
        {
            return (false, valResult.ErrorMessage, model.ReturnCode ?? string.Empty);
        }

        if (model.RowVersion == null || model.RowVersion.Length == 0)
        {
            return (false, "Bắt buộc cung cấp thông tin phiên bản dữ liệu (RowVersion). Vui lòng tải lại trang.", model.ReturnCode ?? string.Empty);
        }

        try
        {
            var updatedResult = await repo.InTransactionAsync(actorId, async () =>
            {
                bool isConfirm = string.Equals(model.Action, "SaveAndConfirm", StringComparison.OrdinalIgnoreCase);
                var freshEligibleLines = await LockAndRevalidateEligibilityUnderTxAsync(model.SalesOrderId, returnOrderId, model.Lines, isConfirm, cancellationToken);

                var order = new CustomerReturnOrder
                {
                    ReturnOrderId = returnOrderId,
                    WarehouseId = model.WarehouseId,
                    Reason = string.IsNullOrWhiteSpace(model.Reason) ? null : model.Reason.Trim()
                };

                // Products dictionary from fresh eligibility
                var productMap = freshEligibleLines.ToDictionary(l => l.SolineId, l => l.ProductId);

                var orderLines = model.Lines.Select(l => new CustomerReturnOrderLine
                {
                    ReturnOrderId = returnOrderId,
                    SolineId = l.SolineId,
                    ProductId = productMap.GetValueOrDefault(l.SolineId, l.ProductId),
                    RequestedQuantity = l.RequestedQuantity,
                    CancelledQuantity = 0
                }).ToList();

                if (isConfirm)
                {
                    await repo.ConfirmOrderAsync(order, orderLines, model.RowVersion, actorId, cancellationToken);
                    return (model.ReturnCode ?? $"RO-{returnOrderId}", "OPEN");
                }
                else
                {
                    await repo.UpdateDraftAsync(order, orderLines, model.RowVersion, actorId, cancellationToken);
                    return (model.ReturnCode ?? $"RO-{returnOrderId}", "DRAFT");
                }
            });

            string successMsg = updatedResult.Item2 == "OPEN"
                ? $"Xác nhận đơn trả hàng thành công. Mã đơn: {updatedResult.Item1}"
                : $"Cập nhật bản nháp đơn trả hàng thành công. Mã đơn: {updatedResult.Item1}";

            return (true, successMsg, updatedResult.Item1);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "Dữ liệu đơn trả hàng đã được cập nhật bởi một người dùng khác. Vui lòng tải lại trang để xem dữ liệu mới nhất.", model.ReturnCode ?? string.Empty);
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message, model.ReturnCode ?? string.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi hệ thống khi cập nhật đơn trả hàng ID: {ReturnOrderId}", returnOrderId);
            return (false, "Đã xảy ra lỗi trong quá trình cập nhật đơn trả hàng. Vui lòng thử lại sau.", model.ReturnCode ?? string.Empty);
        }
    }

    public async Task<(bool Success, string Message)> CancelReturnOrderAsync(
        long returnOrderId,
        string? cancelReason,
        byte[]? rowVersion,
        int actorId,
        CancellationToken cancellationToken = default)
    {
        if (returnOrderId <= 0)
        {
            return (false, "Mã đơn trả hàng không hợp lệ.");
        }

        try
        {
            await repo.InTransactionAsync(actorId, async () =>
            {
                await repo.CancelOrderAsync(returnOrderId, cancelReason, rowVersion, actorId, cancellationToken);
                return true;
            });

            return (true, $"Hủy đơn trả hàng #{returnOrderId} thành công.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "Dữ liệu đơn trả hàng đã được cập nhật bởi một người dùng khác. Vui lòng tải lại trang.");
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi hệ thống khi hủy đơn trả hàng ID: {ReturnOrderId}", returnOrderId);
            return (false, "Đã xảy ra lỗi trong quá trình hủy đơn trả hàng. Vui lòng thử lại sau.");
        }
    }

    private async Task<List<EligibleSalesOrderLineDto>> LockAndRevalidateEligibilityUnderTxAsync(
        long salesOrderId,
        long? currentReturnOrderId,
        List<ReturnOrderLineItemModel> inputLines,
        bool isConfirm,
        CancellationToken cancellationToken)
    {
        var lineSolineIds = inputLines.Select(l => l.SolineId).Distinct().OrderBy(id => id).ToList();

        if (isConfirm)
        {
            // Serializable lock on all target SalesOrderLines in ascending order
            await repo.AcquireSalesOrderLineLocksAsync(lineSolineIds, cancellationToken);
        }

        // Fetch fresh Option A eligibility under locks (or without locks for draft)
        var freshEligibleLines = await repo.GetEligibleSalesOrderLinesAsync(salesOrderId, currentReturnOrderId, cancellationToken);

        if (isConfirm)
        {
            var freshMap = freshEligibleLines.ToDictionary(l => l.SolineId);
            foreach (var inputLine in inputLines)
            {
                if (!freshMap.TryGetValue(inputLine.SolineId, out var fresh) || fresh.EligibleQuantity < inputLine.RequestedQuantity)
                {
                    int available = fresh?.EligibleQuantity ?? 0;
                    throw new InvalidOperationException($"Số lượng yêu cầu của sản phẩm '{inputLine.ProductName ?? inputLine.ProductCode}' ({inputLine.RequestedQuantity}) vượt quá số lượng khả dụng được phép trả ({available}).");
                }
            }
        }

        return freshEligibleLines;
    }

    public async Task PopulateFormOptionsAsync(ReturnOrderFormModel model, CancellationToken cancellationToken = default)
    {
        model.EligibleSalesOrders = await repo.GetEligibleSalesOrdersAsync(cancellationToken);
        model.ActiveWarehouses = await repo.GetActiveWarehousesAsync(cancellationToken);

        if (model.SalesOrderId > 0)
        {
            model.AvailableLines = await repo.GetEligibleSalesOrderLinesAsync(model.SalesOrderId, model.ReturnOrderId, cancellationToken);
            if (string.IsNullOrEmpty(model.CustomerName))
            {
                var so = await repo.GetSalesOrderDetailsAsync(model.SalesOrderId, cancellationToken);
                if (so != null)
                {
                    model.CustomerName = so.Customer?.CustomerName ?? "Khách lẻ";
                    model.SalesOrderCode = so.Socode;
                }
            }
        }

        model.CanCancel = model.ReturnOrderId > 0
            && model.Status is "DRAFT" or "OPEN"
            && !await repo.HasReturnReceiptsAsync(model.ReturnOrderId.Value, cancellationToken);
    }

    private async Task<(bool IsValid, string ErrorMessage)> ValidateFormModelAsync(
        ReturnOrderFormModel model,
        bool isCreate,
        CancellationToken cancellationToken)
    {
        // 1. Action Whitelist
        if (model.Action is not ("SaveDraft" or "SaveAndConfirm"))
        {
            return (false, "Hành động yêu cầu không hợp lệ. Chỉ chấp nhận Lưu bản nháp hoặc Lưu & Xác nhận.");
        }

        // 2. Sales Order Check
        if (model.SalesOrderId <= 0)
        {
            return (false, "Vui lòng chọn đơn bán hàng hợp lệ.");
        }

        // 3. Line Items Check
        if (model.Lines is not { Count: > 0 })
        {
            return (false, "Đơn trả hàng phải có ít nhất một dòng sản phẩm cần trả.");
        }

        // 4. Duplicate SolineId Check
        var duplicateSoline = model.Lines.GroupBy(l => l.SolineId).FirstOrDefault(g => g.Count() > 1);
        if (duplicateSoline != null)
        {
            return (false, "Mỗi dòng sản phẩm của đơn bán hàng chỉ được xuất hiện một lần trên đơn trả hàng.");
        }

        // 5. Positive Requested Quantity
        if (model.Lines.Any(line => line.RequestedQuantity <= 0))
        {
            return (false, "Số lượng yêu cầu của mỗi sản phẩm phải là số nguyên dương lớn hơn 0.");
        }

        // 6. Reason length check
        if (model.Reason?.Trim().Length > 1000)
        {
            return (false, "Lý do trả hàng không được vượt quá 1000 ký tự.");
        }

        // 7. Verify SO lines membership and preliminary quantity check
        var eligibleLines = await repo.GetEligibleSalesOrderLinesAsync(model.SalesOrderId, isCreate ? null : model.ReturnOrderId, cancellationToken);
        var eligibleMap = eligibleLines.ToDictionary(l => l.SolineId);

        foreach (var line in model.Lines)
        {
            if (!eligibleMap.TryGetValue(line.SolineId, out var eligibleDto))
            {
                return (false, $"Dòng sản phẩm với mã tham chiếu #{line.SolineId} không thuộc đơn bán hàng đã chọn hoặc chưa có đợt xuất hàng hoàn tất.");
            }

            if (line.RequestedQuantity > eligibleDto.EligibleQuantity)
            {
                return (false, $"Số lượng yêu cầu của sản phẩm '{eligibleDto.ProductName}' ({line.RequestedQuantity}) vượt quá số lượng tối đa được phép trả ({eligibleDto.EligibleQuantity}).");
            }
        }

        // 8. Warehouse Compatibility Validation (T014)
        var productIds = model.Lines.Select(l => eligibleMap[l.SolineId].ProductId).Distinct();
        var (isCompatible, warehouseError) = await ValidateWarehouseCompatibilityAsync(model.WarehouseId, productIds, cancellationToken);
        if (!isCompatible)
        {
            return (false, warehouseError);
        }

        return (true, string.Empty);
    }

    private async Task<(bool IsCompatible, string ErrorMessage)> ValidateWarehouseCompatibilityAsync(
        int warehouseId,
        IEnumerable<int> productIds,
        CancellationToken cancellationToken)
    {
        var warehouse = await repo.GetWarehouseWithZonesAsync(warehouseId, cancellationToken);
        if (warehouse == null || warehouse.Status != "ACTIVE")
        {
            return (false, "Kho tiếp nhận không tồn tại hoặc đang ở trạng thái không hoạt động (INACTIVE).");
        }

        var products = await repo.GetProductsByIdsAsync(productIds, cancellationToken);
        var activeZones = warehouse.StorageLocations
            .Where(l => l.LocationType == "ZONE" && l.Status == "ACTIVE" && l.ZoneType != "DEFECTIVE"
                     && l.StorageType != "QUARANTINE" && l.ConditionPolicy != "DEFECTIVE_ONLY").ToList();

        if (activeZones.Count == 0)
        {
            return (false, $"Kho tiếp nhận '{warehouse.WarehouseName}' không có khu vực lưu trữ (Zone) hợp lệ nào đang hoạt động.");
        }

        foreach (var p in products)
        {
            bool hasCompatibleZone = activeZones.Any(z => z.ZoneType == "RESERVE" ||
                (z.ZoneType == "STANDARD" && z.ZoneStorageRules.Any(r => r.CategoryId == p.CategoryId && (r.BrandId == null || r.BrandId == p.BrandId))));

            if (!hasCompatibleZone)
            {
                return (false, $"Sản phẩm '{p.ProductName}' (Mã: {p.ProductCode}) không tương thích với các quy tắc lưu trữ tại kho '{warehouse.WarehouseName}'.");
            }
        }

        return (true, string.Empty);
    }
}

