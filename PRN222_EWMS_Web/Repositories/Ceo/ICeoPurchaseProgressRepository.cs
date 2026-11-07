using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Ceo;

namespace PRN222_EWMS_Web.Repositories.Ceo;

public interface ICeoPurchaseProgressRepository
{
    Task<int> CountAsync(CeoPurchaseProgressFilter filter);
    Task<List<PurchaseOrder>> GetPageAsync(CeoPurchaseProgressFilter filter, int page, int pageSize);
    Task<PurchaseOrder?> GetOrderAsync(long id);
    Task<List<VwPurchaseOrderProgress>> GetProgressAsync(IEnumerable<long> orderIds);
    Task<List<CeoLookupOption>> GetWarehousesAsync();
    Task<List<CeoLookupOption>> GetSuppliersAsync();
}
