using PRN222_EWMS_Web.ViewModels.Supplier;

namespace PRN222_EWMS_Web.Services.Supplier;

public interface ISupplierService
{
    Task<SupplierListViewModel> GetSupplierListAsync(SupplierListFilter filter);
    Task<SupplierDetailsViewModel?> GetSupplierDetailsAsync(int supplierId);
    Task<EditSupplierViewModel?> GetEditSupplierAsync(int supplierId, SupplierListFilter? returnFilter = null);
    Task<(int? SupplierId, Dictionary<string, string> Errors)> CreateAsync(CreateSupplierViewModel model);
    Task<Dictionary<string, string>> UpdateAsync(int supplierId, EditSupplierViewModel model);
    Task<bool> ToggleStatusAsync(int supplierId);
}

