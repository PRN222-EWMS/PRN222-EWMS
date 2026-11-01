namespace PRN222_EWMS_Web.ViewModels.Customer;

public class CustomerListFilter
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
}

public class CustomerRowViewModel
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Nickname { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string DisplayName => string.IsNullOrWhiteSpace(Nickname) ? CustomerName : Nickname;
}

public class CustomerListViewModel
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; } = 1;
    public List<CustomerRowViewModel> Items { get; set; } = [];
    public int FirstItem => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int LastItem => Math.Min(Page * PageSize, TotalCount);
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search);
}
