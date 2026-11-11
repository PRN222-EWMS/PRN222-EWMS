using PRN222_EWMS_Web.ViewModels.Stocktaking;

namespace PRN222_EWMS_Web.Services.Stocktaking;

public interface IStocktakeReviewService
{
    Task<StocktakeReviewViewModel?> GetDetailsAsync(int actorId, long id, CancellationToken ct);
    Task CreateRecountAsync(int actorId, StocktakeRecountInput input, CancellationToken ct);
    Task CancelRecountAsync(int actorId, StocktakeActionInput input, CancellationToken ct);
    Task SubmitAdjustmentAsync(int actorId, StocktakeAdjustmentInput input, CancellationToken ct);
    Task CompleteAsync(int actorId, StocktakeActionInput input, CancellationToken ct);
    Task<long> CreateInvestigationAsync(int actorId, StocktakeInvestigationInput input, CancellationToken ct);
    Task FinishInvestigationAsync(int actorId, StocktakeInvestigationConclusionInput input, CancellationToken ct);
    Task ConfirmNoSurplusAsync(int actorId, StocktakeNoSurplusInput input, CancellationToken ct);
    Task ConfirmSourceAsync(int actorId, StocktakeSourceResolutionInput input, CancellationToken ct);
}
