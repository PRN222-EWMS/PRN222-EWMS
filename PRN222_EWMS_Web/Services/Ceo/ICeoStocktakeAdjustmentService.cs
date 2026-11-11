using PRN222_EWMS_Web.ViewModels.Ceo;

namespace PRN222_EWMS_Web.Services.Ceo;

public interface ICeoStocktakeAdjustmentService
{
    Task<CeoStocktakeAdjustmentListViewModel> GetListAsync(int actorId, string? search, string? status, int page, CancellationToken ct);
    Task<CeoStocktakeAdjustmentDetailsViewModel?> GetDetailsAsync(int actorId, long id, CancellationToken ct);
    Task DecideAsync(int actorId, CeoStocktakeAdjustmentDecisionInput input, bool approve, CancellationToken ct);
}
