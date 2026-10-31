using Microsoft.AspNetCore.Mvc.Rendering;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.ViewModels.Products;

public class ProductListViewModel
{
    public string? Keyword { get; set; }
    public int? WarehouseId { get; set; }
    public bool CanFilterWarehouse { get; set; }
    public List<SelectListItem> Warehouses { get; set; } = new();
    public List<ProductListItemViewModel> Items { get; set; } = new();
}

public class ProductListItemViewModel
{
    public Product Product { get; set; } = null!;
    public int TotalStock { get; set; }
    public int DamagedStock { get; set; }
}

public class ProductStockCount
{
    public int ProductId { get; set; }
    public int TotalStock { get; set; }
    public int DamagedStock { get; set; }
}
