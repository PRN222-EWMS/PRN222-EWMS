using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.CustomerReturnOrder;
using WarehouseEntity = PRN222_EWMS_Web.Models.Warehouse;

namespace PRN222_EWMS_Web.Repositories.Sales;

public interface ICustomerReturnOrderRepository
{
    Task<List<EligibleSalesOrderOptionDto>> GetEligibleSalesOrdersAsync(CancellationToken cancellationToken = default);

    Task<List<WarehouseOptionDto>> GetActiveWarehousesAsync(CancellationToken cancellationToken = default);

    Task<WarehouseEntity?> GetWarehouseWithZonesAsync(int warehouseId, CancellationToken cancellationToken = default);

    Task<List<Product>> GetProductsByIdsAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default);

    Task<SalesOrder?> GetSalesOrderDetailsAsync(long salesOrderId, CancellationToken cancellationToken = default);

    Task<List<EligibleSalesOrderLineDto>> GetEligibleSalesOrderLinesAsync(
        long salesOrderId,
        long? currentEditingReturnOrderId = null,
        CancellationToken cancellationToken = default);

    Task<CustomerReturnOrder?> FindByIdAsync(
        long returnOrderId,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default);

    Task<string> GenerateReturnCodeAsync(CancellationToken cancellationToken = default);

    Task<T> InTransactionAsync<T>(int actorId, Func<Task<T>> work);

    Task AcquireSalesOrderLineLocksAsync(IEnumerable<long> solineIds, CancellationToken cancellationToken = default);

    Task SaveDraftAsync(
        CustomerReturnOrder order,
        IEnumerable<CustomerReturnOrderLine> lines,
        int actorId,
        CancellationToken cancellationToken = default);

    Task UpdateDraftAsync(
        CustomerReturnOrder order,
        IEnumerable<CustomerReturnOrderLine> lines,
        byte[] originalRowVersion,
        int actorId,
        CancellationToken cancellationToken = default);

    Task ConfirmOrderAsync(
        CustomerReturnOrder order,
        IEnumerable<CustomerReturnOrderLine> lines,
        byte[]? originalRowVersion,
        int actorId,
        CancellationToken cancellationToken = default);

    Task CancelOrderAsync(
        long returnOrderId,
        string? cancelReason,
        byte[]? originalRowVersion,
        int actorId,
        CancellationToken cancellationToken = default);

    Task<bool> HasReturnReceiptsAsync(
        long returnOrderId,
        CancellationToken cancellationToken = default);

    Task<(List<ReturnOrderListRowDto> Items, int TotalCount)> GetReturnOrdersPagedAsync(
        ReturnOrderFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<List<WarehouseOptionDto>> GetAllWarehousesForFilterAsync(CancellationToken cancellationToken = default);

    Task<(int? WarehouseId, string? WarehouseName, string? RoleName)> GetUserRoleAndWarehouseAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<Dictionary<long, int>> GetLineReceivedCountsAsync(
        long returnOrderId,
        CancellationToken cancellationToken = default);
}
