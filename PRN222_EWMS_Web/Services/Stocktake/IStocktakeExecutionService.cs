using PRN222_EWMS_Web.ViewModels.Stocktaking;

namespace PRN222_EWMS_Web.Services.Stocktaking;

public interface IStocktakeExecutionService
{
    Task<StocktakeSerialLookup> LookupSerialAsync(int actorId, long id, long? recountRequestId, string serial, CancellationToken ct);
    Task<StocktakeExecutionListViewModel> GetListAsync(int actorId, string? search, int page, CancellationToken ct);
    Task<StocktakeExecutionViewModel> GetDetailsAsync(int actorId, long id, long? recountRequestId, CancellationToken ct);
    Task StartAsync(int actorId, StocktakeActionInput input, CancellationToken ct);
    Task ScanAsync(int actorId, StocktakeCountInput input, CancellationToken ct);
    Task SubmitAsync(int actorId, StocktakeActionInput input, CancellationToken ct);
}
