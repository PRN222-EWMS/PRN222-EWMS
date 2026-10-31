namespace PRN222_EWMS_Web.ViewModels.Admin;

public class AccountDetailsViewModel
{
    public int UserId { get; set; }

    public string FullName { get; set; } = "";

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string RoleName { get; set; } = "";

    public string StatusLabel { get; set; } = "";

    public string StatusClass { get; set; } = "unknown";

    public string WarehouseDisplay { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public UserListFilter ReturnFilter { get; set; } = new();
}
