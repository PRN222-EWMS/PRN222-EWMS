namespace PRN222_EWMS_Web.ViewModels.Supplier;

public class SupplierDetailsViewModel
{
    public int SupplierId { get; set; }

    public string SupplierName { get; set; } = null!;

    public string? Nickname { get; set; }

    public string DisplayName => !string.IsNullOrWhiteSpace(Nickname)
        ? $"{SupplierName} ({Nickname})"
        : SupplierName;

    public string? Address { get; set; }

    public string AddressDisplay => !string.IsNullOrWhiteSpace(Address) ? Address : "—";

    public string? Phone { get; set; }

    public string PhoneDisplay => !string.IsNullOrWhiteSpace(Phone) ? Phone : "—";

    public string? Email { get; set; }

    public string EmailDisplay => !string.IsNullOrWhiteSpace(Email) ? Email : "—";

    public string Status { get; set; } = null!;

    public string StatusLabel => Status == "ACTIVE" ? "Đang hoạt động" : "Ngừng hoạt động";

    public string StatusClass => Status == "ACTIVE" ? "status-badge status-active" : "status-badge status-inactive";

    public bool IsActive => Status == "ACTIVE";

    public SupplierListFilter ReturnFilter { get; set; } = new();
}

