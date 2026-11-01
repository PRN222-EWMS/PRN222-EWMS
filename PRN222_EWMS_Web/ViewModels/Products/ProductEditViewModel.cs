namespace PRN222_EWMS_Web.ViewModels.Products;

public class ProductEditViewModel : ProductCreateViewModel
{
    public int ProductId { get; set; }

    public string RowVersion { get; set; } = "";
}