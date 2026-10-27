using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace PRN222_EWMS_Web.Models;

public partial class EwmsDbContext : DbContext
{
    public EwmsDbContext(DbContextOptions<EwmsDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Warehouse> Warehouses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Role");

            entity.HasIndex(e => e.RoleName, "UX_Role_01").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.RoleName)
                .HasMaxLength(32)
                .IsUnicode(false);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("User");

            entity.HasIndex(e => e.RoleId, "IX_User_010");

            entity.HasIndex(e => e.WarehouseId, "IX_User_011");

            entity.HasIndex(e => e.Username, "UX_User_01").IsUnique();

            entity.HasIndex(e => e.Email, "UX_User_02")
                .IsUnique()
                .HasFilter("([Email] IS NOT NULL)");

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.PasswordHash).HasMaxLength(512);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.Username).HasMaxLength(100);
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_User_RoleID");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.Users)
                .HasForeignKey(d => d.WarehouseId)
                .HasConstraintName("FK_User_WarehouseID");
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.ToTable("Warehouse");

            entity.HasIndex(e => e.WarehouseName, "UX_Warehouse_01").IsUnique();

            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.WarehouseName).HasMaxLength(150);
            entity.Property(e => e.WarehouseType)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("MAIN");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
