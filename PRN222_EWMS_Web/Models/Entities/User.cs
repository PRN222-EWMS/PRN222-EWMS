using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class User
{
    public int UserId { get; set; }

    public int RoleId { get; set; }

    public int? WarehouseId { get; set; }

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Role Role { get; set; } = null!;

    public virtual Warehouse? Warehouse { get; set; }
}
