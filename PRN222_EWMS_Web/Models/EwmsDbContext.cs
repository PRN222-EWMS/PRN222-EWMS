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
    public virtual DbSet<StorageLocation> StorageLocations { get; set; }
    public virtual DbSet<ZoneStorageRule> ZoneStorageRules { get; set; }
    public virtual DbSet<Category> Categories { get; set; }
    public virtual DbSet<Brand> Brands { get; set; }

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
            // Compatible with databases that have audit triggers on Warehouse.
            entity.ToTable("Warehouse", table => table.UseSqlOutputClause(false));

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

        modelBuilder.Entity<StorageLocation>(entity =>
        {
            entity.ToTable("StorageLocation", table => table.UseSqlOutputClause(false));
            entity.HasKey(e => e.LocationId);
            entity.Property(e => e.LocationId).HasColumnName("LocationID");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");
            entity.Property(e => e.ParentLocationId).HasColumnName("ParentLocationID");
            entity.Property(e => e.LocationName).HasMaxLength(100);
            entity.Property(e => e.LocationType).HasMaxLength(32).IsUnicode(false);
            entity.Property(e => e.ZoneType).HasMaxLength(16).IsUnicode(false);
            entity.Property(e => e.StorageType).HasMaxLength(8).IsUnicode(false);
            entity.Property(e => e.ConditionPolicy).HasMaxLength(8).IsUnicode(false);
            entity.Property(e => e.CapacityMode).HasMaxLength(16).IsUnicode(false);
            entity.Property(e => e.LengthCm).HasPrecision(10, 2);
            entity.Property(e => e.WidthCm).HasPrecision(10, 2);
            entity.Property(e => e.HeightCm).HasPrecision(10, 2);
            entity.Property(e => e.MaxWeightKg).HasPrecision(12, 3);
            entity.Property(e => e.Status).HasMaxLength(32).IsUnicode(false);
            entity.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasOne<Warehouse>().WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<StorageLocation>().WithMany().HasForeignKey(e => e.ParentLocationId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Category");
            entity.Property(e => e.CategoryId).HasColumnName("CategoryID");
            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.ToTable("Brand");
            entity.Property(e => e.BrandId).HasColumnName("BrandID");
            entity.Property(e => e.BrandName).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(32).IsUnicode(false);
        });

        modelBuilder.Entity<ZoneStorageRule>(entity =>
        {
            entity.ToTable("ZoneStorageRule", table => table.UseSqlOutputClause(false));
            entity.HasKey(e => e.ZoneRuleId);
            entity.Property(e => e.ZoneRuleId).HasColumnName("ZoneRuleID");
            entity.Property(e => e.ZoneLocationId).HasColumnName("ZoneLocationID");
            entity.Property(e => e.CategoryId).HasColumnName("CategoryID");
            entity.Property(e => e.BrandId).HasColumnName("BrandID");
            entity.HasOne(e => e.ZoneLocation).WithMany().HasForeignKey(e => e.ZoneLocationId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Category).WithMany().HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Brand).WithMany().HasForeignKey(e => e.BrandId).OnDelete(DeleteBehavior.NoAction);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
