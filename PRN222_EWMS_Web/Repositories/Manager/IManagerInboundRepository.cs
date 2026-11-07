using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Repositories.Manager;

public interface IManagerInboundRepository
{
    Task<int> CountAsync(ManagerInboundFilter filter);
    Task<List<Inbound>> GetPageAsync(ManagerInboundFilter filter, int page, int pageSize);
    Task<Inbound?> GetDetailAsync(long id);
    Task<List<VwPurchaseOrderProgress>> GetPoProgressAsync(IEnumerable<long> poIds);
    Task<List<ManagerWarehouseOption>> GetWarehousesAsync();
}
