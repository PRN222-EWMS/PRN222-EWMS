namespace PRN222_EWMS_Web.ViewModels.Account;

public class ProfileViewModel
{
    public string Username { get; set; } = "";
    public string RoleName { get; set; } = "";
    public string? WarehouseName { get; set; }
    public EditProfileViewModel Profile { get; set; } = new();
    public ChangePasswordViewModel Password { get; set; } = new();
}
