using PRN222_EWMS_Web.ViewModels.Sales;

namespace PRN222_EWMS_Web.ViewModels.Customer;

public class CustomerDetailsViewModel : CustomerRowViewModel
{
    public List<SalesOrderRowViewModel> Orders { get; set; } = [];
    public CustomerListFilter ReturnFilter { get; set; } = new();
}
