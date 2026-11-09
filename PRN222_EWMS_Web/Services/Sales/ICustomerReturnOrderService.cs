using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PRN222_EWMS_Web.ViewModels.CustomerReturnOrder;

namespace PRN222_EWMS_Web.Services.Sales;

public interface ICustomerReturnOrderService
{
    Task<ReturnOrderFormModel> GetCreateViewModelAsync(long? selectedSalesOrderId = null, CancellationToken cancellationToken = default);

    Task<List<EligibleSalesOrderLineDto>> GetEligibleLinesAsync(long salesOrderId, CancellationToken cancellationToken = default);

    Task<ReturnOrderFormModel?> GetEditViewModelAsync(long returnOrderId, int currentUserId, CancellationToken cancellationToken = default);

    Task<(bool Success, string Message, long ReturnOrderId, string ReturnCode)> CreateReturnOrderAsync(
        ReturnOrderFormModel model,
        int actorId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string Message, string ReturnCode)> UpdateDraftOrderAsync(
        long returnOrderId,
        ReturnOrderFormModel model,
        int actorId,
        CancellationToken cancellationToken = default);

    Task PopulateFormOptionsAsync(ReturnOrderFormModel model, CancellationToken cancellationToken = default);

    Task<ReturnOrderListViewModel> GetReturnOrderListAsync(
        ReturnOrderListFilterViewModel filter,
        int userId,
        CancellationToken cancellationToken = default);
}

