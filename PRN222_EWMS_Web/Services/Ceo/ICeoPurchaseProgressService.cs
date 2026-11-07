using PRN222_EWMS_Web.ViewModels.Ceo;

namespace PRN222_EWMS_Web.Services.Ceo;

public interface ICeoPurchaseProgressService
{
    Task<CeoPurchaseProgressViewModel> GetPageAsync(CeoPurchaseProgressFilter filter);
    Task<CeoPurchaseOrderDetailViewModel?> GetDetailsAsync(long id);
}
