using PRN222_EWMS_Web.ViewModels.Stocktaking;

namespace PRN222_EWMS_Web.Services.Stocktaking;

public interface IStocktakeHandlingService
{
    Task<StocktakeHandlingViewModel> GetAsync(int actorId, long id, long handlingId, long? recountRequestId, CancellationToken ct);
    Task MoveAsync(int actorId, StocktakeMoveInput input, CancellationToken ct);
}
