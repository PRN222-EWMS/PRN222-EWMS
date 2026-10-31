using Microsoft.EntityFrameworkCore.Storage;
using PRN222_EWMS_Web.Models;
using WarehouseEntity = PRN222_EWMS_Web.Models.Warehouse;

namespace PRN222_EWMS_Web.Repositories.Purchasing;

public interface IPurchaseOrderRepository
{
    IQueryable<PurchaseOrder> Orders { get; }
    IQueryable<VwPurchaseOrderProgress> Progress { get; }
    IQueryable<Supplier> Suppliers { get; }
    IQueryable<WarehouseEntity> Warehouses { get; }
    IQueryable<Product> Products { get; }
    IQueryable<SupplierProduct> SupplierProducts { get; }

    Task<PurchaseOrder?> FindByIdAsync(long id, bool asNoTracking = false);
    Task<PurchaseOrder?> GetDetailsAsync(long id);
    Task<bool> HasAnyInboundAsync(long purchaseOrderId);
    Task<string> GenerateNextPoCodeAsync();

    Task<IDbContextTransaction> BeginTransactionAsync();
    Task AddAsync(PurchaseOrder po, int actorId);
    Task UpdateAsync(PurchaseOrder po, int actorId);
    Task SaveChangesWithActorAsync(int actorId);
}
