namespace PRN222_EWMS_Web.Repositories.User;

public sealed class UserListRecord
{
    public int UserId { get; init; }
    public string FullName { get; init; } = "";
    public string? Email { get; init; }
    public string RoleName { get; init; } = "";
    public string? WarehouseName { get; init; }
    public string Status { get; init; } = "";
    public DateTime CreatedAt { get; init; }
}
