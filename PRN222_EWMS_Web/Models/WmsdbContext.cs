using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models.Entities;

namespace PRN222_EWMS_Web.Models;

public partial class WmsdbContext : DbContext
{
    public WmsdbContext()
    {
    }

    public WmsdbContext(DbContextOptions<WmsdbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Brand> Brands { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<CustomerReturnOrder> CustomerReturnOrders { get; set; }

    public virtual DbSet<CustomerReturnOrderLine> CustomerReturnOrderLines { get; set; }

    public virtual DbSet<DemandAllocation> DemandAllocations { get; set; }

    public virtual DbSet<Inbound> Inbounds { get; set; }

    public virtual DbSet<InboundItem> InboundItems { get; set; }

    public virtual DbSet<InventoryAdjustment> InventoryAdjustments { get; set; }

    public virtual DbSet<InventoryAdjustmentLine> InventoryAdjustmentLines { get; set; }

    public virtual DbSet<Notice> Notices { get; set; }

    public virtual DbSet<Outbound> Outbounds { get; set; }

    public virtual DbSet<OutboundItem> OutboundItems { get; set; }

    public virtual DbSet<PickingClaim> PickingClaims { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<PurchaseOrder> PurchaseOrders { get; set; }

    public virtual DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; }

    public virtual DbSet<Reservation> Reservations { get; set; }

    public virtual DbSet<ReservationConsumption> ReservationConsumptions { get; set; }

    public virtual DbSet<ReturnReceipt> ReturnReceipts { get; set; }

    public virtual DbSet<ReturnReceiptItem> ReturnReceiptItems { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<SalesOrder> SalesOrders { get; set; }

    public virtual DbSet<SalesOrderLine> SalesOrderLines { get; set; }

    public virtual DbSet<SerialItem> SerialItems { get; set; }

    public virtual DbSet<SerialMovement> SerialMovements { get; set; }

    public virtual DbSet<Stocktake> Stocktakes { get; set; }

    public virtual DbSet<StocktakeAssignment> StocktakeAssignments { get; set; }

    public virtual DbSet<StocktakeObservation> StocktakeObservations { get; set; }

    public virtual DbSet<StorageLocation> StorageLocations { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<SupplySource> SupplySources { get; set; }

    public virtual DbSet<TransferDispatch> TransferDispatches { get; set; }

    public virtual DbSet<TransferDispatchItem> TransferDispatchItems { get; set; }

    public virtual DbSet<TransferLossRequest> TransferLossRequests { get; set; }

    public virtual DbSet<TransferLossRequestItem> TransferLossRequestItems { get; set; }

    public virtual DbSet<TransferOrder> TransferOrders { get; set; }

    public virtual DbSet<TransferOrderLine> TransferOrderLines { get; set; }

    public virtual DbSet<TransferReceipt> TransferReceipts { get; set; }

    public virtual DbSet<TransferReceiptItem> TransferReceiptItems { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Warehouse> Warehouses { get; set; }

    public virtual DbSet<ZoneStorageRule> ZoneStorageRules { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=localhost;Initial Catalog=WMSDB;User ID=sa;Password=123;Encrypt=True;Trust Server Certificate=True;Command Timeout=120");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Brand>(entity =>
        {
            entity.ToTable("Brand");

            entity.HasIndex(e => e.BrandName, "UX_Brand_01").IsUnique();

            entity.Property(e => e.BrandId).HasColumnName("BrandID");
            entity.Property(e => e.BrandName).HasMaxLength(100);
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Category");

            entity.HasIndex(e => e.CategoryName, "UX_Category_01").IsUnique();

            entity.Property(e => e.CategoryId).HasColumnName("CategoryID");
            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customer");

            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.CustomerName).HasMaxLength(200);
            entity.Property(e => e.Phone).HasMaxLength(30);
        });

        modelBuilder.Entity<CustomerReturnOrder>(entity =>
        {
            entity.HasKey(e => e.ReturnOrderId);

            entity.ToTable("CustomerReturnOrder");

            entity.HasIndex(e => e.SalesOrderId, "IX_CustomerReturnOrder_039");

            entity.HasIndex(e => e.WarehouseId, "IX_CustomerReturnOrder_040");

            entity.HasIndex(e => e.CreatedByUserId, "IX_CustomerReturnOrder_041");

            entity.HasIndex(e => e.ReturnCode, "UX_CustomerReturnOrder_01").IsUnique();

            entity.Property(e => e.ReturnOrderId).HasColumnName("ReturnOrderID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.ReturnCode)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.SalesOrderId).HasColumnName("SalesOrderID");
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.CustomerReturnOrders)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CustomerReturnOrder_CreatedByUserID");

            entity.HasOne(d => d.SalesOrder).WithMany(p => p.CustomerReturnOrders)
                .HasForeignKey(d => d.SalesOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CustomerReturnOrder_SalesOrderID");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.CustomerReturnOrders)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CustomerReturnOrder_WarehouseID");
        });

        modelBuilder.Entity<CustomerReturnOrderLine>(entity =>
        {
            entity.HasKey(e => e.ReturnLineId);

            entity.ToTable("CustomerReturnOrderLine");

            entity.HasIndex(e => e.SolineId, "IX_CustomerReturnOrderLine_042");

            entity.HasIndex(e => new { e.ReturnOrderId, e.SolineId }, "UX_CustomerReturnOrderLine_01").IsUnique();

            entity.Property(e => e.ReturnLineId).HasColumnName("ReturnLineID");
            entity.Property(e => e.ReturnOrderId).HasColumnName("ReturnOrderID");
            entity.Property(e => e.SolineId).HasColumnName("SOLineID");

            entity.HasOne(d => d.ReturnOrder).WithMany(p => p.CustomerReturnOrderLines)
                .HasForeignKey(d => d.ReturnOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CustomerReturnOrderLine_ReturnOrderID");

            entity.HasOne(d => d.Soline).WithMany(p => p.CustomerReturnOrderLines)
                .HasForeignKey(d => d.SolineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CustomerReturnOrderLine_SOLineID");
        });

        modelBuilder.Entity<DemandAllocation>(entity =>
        {
            entity.HasKey(e => e.AllocationId);

            entity.ToTable("DemandAllocation");

            entity.HasIndex(e => e.SupplySourceId, "IX_DemandAllocation_077");

            entity.HasIndex(e => e.OutboundId, "IX_DemandAllocation_078");

            entity.HasIndex(e => e.DispatchId, "IX_DemandAllocation_079");

            entity.HasIndex(e => new { e.OutboundId, e.SupplySourceId }, "UX_DemandAllocation_01")
                .IsUnique()
                .HasFilter("([OutboundID] IS NOT NULL)");

            entity.HasIndex(e => new { e.DispatchId, e.SupplySourceId }, "UX_DemandAllocation_02")
                .IsUnique()
                .HasFilter("([DispatchID] IS NOT NULL)");

            entity.Property(e => e.AllocationId).HasColumnName("AllocationID");
            entity.Property(e => e.AllocatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DispatchId).HasColumnName("DispatchID");
            entity.Property(e => e.OutboundId).HasColumnName("OutboundID");
            entity.Property(e => e.SupplySourceId).HasColumnName("SupplySourceID");

            entity.HasOne(d => d.Dispatch).WithMany(p => p.DemandAllocations)
                .HasForeignKey(d => d.DispatchId)
                .HasConstraintName("FK_DemandAllocation_DispatchID");

            entity.HasOne(d => d.Outbound).WithMany(p => p.DemandAllocations)
                .HasForeignKey(d => d.OutboundId)
                .HasConstraintName("FK_DemandAllocation_OutboundID");

            entity.HasOne(d => d.SupplySource).WithMany(p => p.DemandAllocations)
                .HasForeignKey(d => d.SupplySourceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DemandAllocation_SupplySourceID");
        });

        modelBuilder.Entity<Inbound>(entity =>
        {
            entity.ToTable("Inbound");

            entity.HasIndex(e => e.PurchaseOrderId, "IX_Inbound_022");

            entity.HasIndex(e => e.CreatedByUserId, "IX_Inbound_023");

            entity.HasIndex(e => e.PerformedByUserId, "IX_Inbound_024");

            entity.HasIndex(e => e.ReceiptCode, "UX_Inbound_01").IsUnique();

            entity.Property(e => e.InboundId).HasColumnName("InboundID");
            entity.Property(e => e.CompletedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.PerformedByUserId).HasColumnName("PerformedByUserID");
            entity.Property(e => e.PurchaseOrderId).HasColumnName("PurchaseOrderID");
            entity.Property(e => e.ReceiptCode)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.InboundCreatedByUsers)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Inbound_CreatedByUserID");

            entity.HasOne(d => d.PerformedByUser).WithMany(p => p.InboundPerformedByUsers)
                .HasForeignKey(d => d.PerformedByUserId)
                .HasConstraintName("FK_Inbound_PerformedByUserID");

            entity.HasOne(d => d.PurchaseOrder).WithMany(p => p.Inbounds)
                .HasForeignKey(d => d.PurchaseOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Inbound_PurchaseOrderID");
        });

        modelBuilder.Entity<InboundItem>(entity =>
        {
            entity.ToTable("InboundItem");

            entity.HasIndex(e => e.InboundId, "IX_InboundItem_025");

            entity.HasIndex(e => e.PolineId, "IX_InboundItem_026");

            entity.HasIndex(e => e.SerialItemId, "IX_InboundItem_027");

            entity.HasIndex(e => e.ScannedSerial, "UX_InboundItem_01")
                .IsUnique()
                .HasFilter("([IsActive]=(1))");

            entity.HasIndex(e => e.SerialItemId, "UX_InboundItem_02")
                .IsUnique()
                .HasFilter("([SerialItemID] IS NOT NULL)");

            entity.Property(e => e.InboundItemId).HasColumnName("InboundItemID");
            entity.Property(e => e.ConditionNote).HasMaxLength(1000);
            entity.Property(e => e.InboundId).HasColumnName("InboundID");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PolineId).HasColumnName("POLineID");
            entity.Property(e => e.ReceivedCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.ScannedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ScannedSerial).HasMaxLength(100);
            entity.Property(e => e.SerialItemId).HasColumnName("SerialItemID");

            entity.HasOne(d => d.Inbound).WithMany(p => p.InboundItems)
                .HasForeignKey(d => d.InboundId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InboundItem_InboundID");

            entity.HasOne(d => d.Poline).WithMany(p => p.InboundItems)
                .HasForeignKey(d => d.PolineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InboundItem_POLineID");

            entity.HasOne(d => d.SerialItem).WithOne(p => p.InboundItem)
                .HasForeignKey<InboundItem>(d => d.SerialItemId)
                .HasConstraintName("FK_InboundItem_SerialItemID");
        });

        modelBuilder.Entity<InventoryAdjustment>(entity =>
        {
            entity.HasKey(e => e.AdjustmentId);

            entity.ToTable("InventoryAdjustment");

            entity.HasIndex(e => e.StocktakeId, "IX_InventoryAdjustment_092");

            entity.HasIndex(e => e.SubmittedByUserId, "IX_InventoryAdjustment_093");

            entity.HasIndex(e => e.DecidedByUserId, "IX_InventoryAdjustment_094");

            entity.HasIndex(e => e.AdjustmentCode, "UX_InventoryAdjustment_01").IsUnique();

            entity.Property(e => e.AdjustmentId).HasColumnName("AdjustmentID");
            entity.Property(e => e.AdjustmentCode)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.AppliedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DecidedAt).HasPrecision(3);
            entity.Property(e => e.DecidedByUserId).HasColumnName("DecidedByUserID");
            entity.Property(e => e.DecisionNote).HasMaxLength(1000);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.StocktakeId).HasColumnName("StocktakeID");
            entity.Property(e => e.SubmittedAt).HasPrecision(3);
            entity.Property(e => e.SubmittedByUserId).HasColumnName("SubmittedByUserID");

            entity.HasOne(d => d.DecidedByUser).WithMany(p => p.InventoryAdjustmentDecidedByUsers)
                .HasForeignKey(d => d.DecidedByUserId)
                .HasConstraintName("FK_InventoryAdjustment_DecidedByUserID");

            entity.HasOne(d => d.Stocktake).WithMany(p => p.InventoryAdjustments)
                .HasForeignKey(d => d.StocktakeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryAdjustment_StocktakeID");

            entity.HasOne(d => d.SubmittedByUser).WithMany(p => p.InventoryAdjustmentSubmittedByUsers)
                .HasForeignKey(d => d.SubmittedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryAdjustment_SubmittedByUserID");
        });

        modelBuilder.Entity<InventoryAdjustmentLine>(entity =>
        {
            entity.HasKey(e => e.AdjustmentLineId);

            entity.ToTable("InventoryAdjustmentLine");

            entity.HasIndex(e => e.AdjustmentId, "IX_InventoryAdjustmentLine_095");

            entity.HasIndex(e => e.ObservationId, "IX_InventoryAdjustmentLine_096");

            entity.HasIndex(e => e.SerialItemId, "IX_InventoryAdjustmentLine_097");

            entity.HasIndex(e => e.ProductId, "IX_InventoryAdjustmentLine_098");

            entity.HasIndex(e => e.TargetLocationId, "IX_InventoryAdjustmentLine_099");

            entity.HasIndex(e => e.ObservationId, "UX_InventoryAdjustmentLine_01")
                .IsUnique()
                .HasFilter("([IsActive]=(1))");

            entity.Property(e => e.AdjustmentLineId).HasColumnName("AdjustmentLineID");
            entity.Property(e => e.AdjustmentId).HasColumnName("AdjustmentID");
            entity.Property(e => e.AdjustmentType)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.ObservationId).HasColumnName("ObservationID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.SerialItemId).HasColumnName("SerialItemID");
            entity.Property(e => e.TargetCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.TargetLocationId).HasColumnName("TargetLocationID");

            entity.HasOne(d => d.Adjustment).WithMany(p => p.InventoryAdjustmentLines)
                .HasForeignKey(d => d.AdjustmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryAdjustmentLine_AdjustmentID");

            entity.HasOne(d => d.Observation).WithOne(p => p.InventoryAdjustmentLine)
                .HasForeignKey<InventoryAdjustmentLine>(d => d.ObservationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryAdjustmentLine_ObservationID");

            entity.HasOne(d => d.Product).WithMany(p => p.InventoryAdjustmentLines)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_InventoryAdjustmentLine_ProductID");

            entity.HasOne(d => d.SerialItem).WithMany(p => p.InventoryAdjustmentLines)
                .HasForeignKey(d => d.SerialItemId)
                .HasConstraintName("FK_InventoryAdjustmentLine_SerialItemID");

            entity.HasOne(d => d.TargetLocation).WithMany(p => p.InventoryAdjustmentLines)
                .HasForeignKey(d => d.TargetLocationId)
                .HasConstraintName("FK_InventoryAdjustmentLine_TargetLocationID");
        });

        modelBuilder.Entity<Notice>(entity =>
        {
            entity.ToTable("Notice");

            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt }, "IX_Notice_UserID_IsRead_CreatedAt").IsDescending(false, false, true);

            entity.Property(e => e.NoticeId).HasColumnName("NoticeID");
            entity.Property(e => e.Content).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.NoticeType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("INFO");
            entity.Property(e => e.ReadAt).HasPrecision(3);
            entity.Property(e => e.RelatedUrl).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithMany(p => p.Notices)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notice_User");
        });

        modelBuilder.Entity<Outbound>(entity =>
        {
            entity.ToTable("Outbound");

            entity.HasIndex(e => e.SalesOrderId, "IX_Outbound_032");

            entity.HasIndex(e => e.CreatedByUserId, "IX_Outbound_033");

            entity.HasIndex(e => e.AssignedToUserId, "IX_Outbound_034");

            entity.HasIndex(e => e.IssueCode, "UX_Outbound_01").IsUnique();

            entity.HasIndex(e => e.SalesOrderId, "UX_Outbound_02")
                .IsUnique()
                .HasFilter("([IsActivePicking]=(1))");

            entity.Property(e => e.OutboundId).HasColumnName("OutboundID");
            entity.Property(e => e.AssignedToUserId).HasColumnName("AssignedToUserID");
            entity.Property(e => e.CompletedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.IssueCode)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.RecipientName).HasMaxLength(150);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.SalesOrderId).HasColumnName("SalesOrderID");
            entity.Property(e => e.StartedAt).HasPrecision(3);
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");

            entity.HasOne(d => d.AssignedToUser).WithMany(p => p.OutboundAssignedToUsers)
                .HasForeignKey(d => d.AssignedToUserId)
                .HasConstraintName("FK_Outbound_AssignedToUserID");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.OutboundCreatedByUsers)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Outbound_CreatedByUserID");

            entity.HasOne(d => d.SalesOrder).WithOne(p => p.Outbound)
                .HasForeignKey<Outbound>(d => d.SalesOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Outbound_SalesOrderID");
        });

        modelBuilder.Entity<OutboundItem>(entity =>
        {
            entity.ToTable("OutboundItem");

            entity.HasIndex(e => e.OutboundId, "IX_OutboundItem_035");

            entity.HasIndex(e => e.SolineId, "IX_OutboundItem_036");

            entity.HasIndex(e => e.SerialItemId, "IX_OutboundItem_037");

            entity.HasIndex(e => e.SourceAtIssueId, "IX_OutboundItem_038");

            entity.HasIndex(e => new { e.OutboundId, e.SerialItemId }, "UX_OutboundItem_01")
                .IsUnique()
                .HasFilter("([IsActive]=(1))");

            entity.Property(e => e.OutboundItemId).HasColumnName("OutboundItemID");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.OutboundId).HasColumnName("OutboundID");
            entity.Property(e => e.ScannedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.SerialItemId).HasColumnName("SerialItemID");
            entity.Property(e => e.SolineId).HasColumnName("SOLineID");
            entity.Property(e => e.SourceAtIssueId).HasColumnName("SourceAtIssueID");

            entity.HasOne(d => d.Outbound).WithMany(p => p.OutboundItems)
                .HasForeignKey(d => d.OutboundId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OutboundItem_OutboundID");

            entity.HasOne(d => d.SerialItem).WithMany(p => p.OutboundItems)
                .HasForeignKey(d => d.SerialItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OutboundItem_SerialItemID");

            entity.HasOne(d => d.Soline).WithMany(p => p.OutboundItems)
                .HasForeignKey(d => d.SolineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OutboundItem_SOLineID");

            entity.HasOne(d => d.SourceAtIssue).WithMany(p => p.OutboundItems)
                .HasForeignKey(d => d.SourceAtIssueId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OutboundItem_SourceAtIssueID");
        });

        modelBuilder.Entity<PickingClaim>(entity =>
        {
            entity.HasKey(e => e.SerialItemId);

            entity.ToTable("PickingClaim");

            entity.HasIndex(e => e.OutboundId, "IX_PickingClaim_080");

            entity.HasIndex(e => e.DispatchId, "IX_PickingClaim_081");

            entity.HasIndex(e => e.ClaimedByUserId, "IX_PickingClaim_082");

            entity.Property(e => e.SerialItemId)
                .ValueGeneratedNever()
                .HasColumnName("SerialItemID");
            entity.Property(e => e.ClaimedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ClaimedByUserId).HasColumnName("ClaimedByUserID");
            entity.Property(e => e.DispatchId).HasColumnName("DispatchID");
            entity.Property(e => e.OutboundId).HasColumnName("OutboundID");

            entity.HasOne(d => d.ClaimedByUser).WithMany(p => p.PickingClaims)
                .HasForeignKey(d => d.ClaimedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PickingClaim_ClaimedByUserID");

            entity.HasOne(d => d.Dispatch).WithMany(p => p.PickingClaims)
                .HasForeignKey(d => d.DispatchId)
                .HasConstraintName("FK_PickingClaim_DispatchID");

            entity.HasOne(d => d.Outbound).WithMany(p => p.PickingClaims)
                .HasForeignKey(d => d.OutboundId)
                .HasConstraintName("FK_PickingClaim_OutboundID");

            entity.HasOne(d => d.SerialItem).WithOne(p => p.PickingClaim)
                .HasForeignKey<PickingClaim>(d => d.SerialItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PickingClaim_SerialItemID");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Product");

            entity.HasIndex(e => e.CategoryId, "IX_Product_012");

            entity.HasIndex(e => e.BrandId, "IX_Product_013");

            entity.HasIndex(e => e.CreatedByUserId, "IX_Product_014");

            entity.HasIndex(e => e.ProductCode, "UX_Product_01").IsUnique();

            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.BrandId).HasColumnName("BrandID");
            entity.Property(e => e.CategoryId).HasColumnName("CategoryID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.GrossWeightKg).HasColumnType("decimal(10, 3)");
            entity.Property(e => e.HeightCm).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.LengthCm).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Model).HasMaxLength(100);
            entity.Property(e => e.ProductCode)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.Unit).HasMaxLength(30);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
            entity.Property(e => e.WidthCm).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Brand).WithMany(p => p.Products)
                .HasForeignKey(d => d.BrandId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Product_BrandID");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Product_CategoryID");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.Products)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Product_CreatedByUserID");
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("PurchaseOrder");

            entity.HasIndex(e => e.SupplierId, "IX_PurchaseOrder_018");

            entity.HasIndex(e => e.WarehouseId, "IX_PurchaseOrder_019");

            entity.HasIndex(e => e.CreatedByUserId, "IX_PurchaseOrder_020");

            entity.HasIndex(e => e.Pocode, "UX_PurchaseOrder_01").IsUnique();

            entity.Property(e => e.PurchaseOrderId).HasColumnName("PurchaseOrderID");
            entity.Property(e => e.CloseReason).HasMaxLength(1000);
            entity.Property(e => e.ClosedAt).HasPrecision(3);
            entity.Property(e => e.ConfirmedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.Pocode)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("POCode");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.SupplierId).HasColumnName("SupplierID");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseOrder_CreatedByUserID");

            entity.HasOne(d => d.Supplier).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseOrder_SupplierID");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseOrder_WarehouseID");
        });

        modelBuilder.Entity<PurchaseOrderLine>(entity =>
        {
            entity.HasKey(e => e.PolineId);

            entity.ToTable("PurchaseOrderLine");

            entity.HasIndex(e => e.ProductId, "IX_PurchaseOrderLine_021");

            entity.HasIndex(e => new { e.PurchaseOrderId, e.ProductId }, "UX_PurchaseOrderLine_01").IsUnique();

            entity.Property(e => e.PolineId).HasColumnName("POLineID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.PurchaseOrderId).HasColumnName("PurchaseOrderID");

            entity.HasOne(d => d.Product).WithMany(p => p.PurchaseOrderLines)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseOrderLine_ProductID");

            entity.HasOne(d => d.PurchaseOrder).WithMany(p => p.PurchaseOrderLines)
                .HasForeignKey(d => d.PurchaseOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseOrderLine_PurchaseOrderID");
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.ToTable("Reservation");

            entity.HasIndex(e => new { e.SupplySourceId, e.Status }, "IX_Reservation_004");

            entity.HasIndex(e => e.SolineId, "IX_Reservation_005");

            entity.HasIndex(e => e.TransferLineId, "IX_Reservation_006");

            entity.HasIndex(e => e.ReplacesReservationId, "IX_Reservation_076");

            entity.Property(e => e.ReservationId).HasColumnName("ReservationID");
            entity.Property(e => e.ClosedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ReplacesReservationId).HasColumnName("ReplacesReservationID");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.SolineId).HasColumnName("SOLineID");
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.SupplySourceId).HasColumnName("SupplySourceID");
            entity.Property(e => e.TransferLineId).HasColumnName("TransferLineID");

            entity.HasOne(d => d.ReplacesReservation).WithMany(p => p.InverseReplacesReservation)
                .HasForeignKey(d => d.ReplacesReservationId)
                .HasConstraintName("FK_Reservation_ReplacesReservationID");

            entity.HasOne(d => d.Soline).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.SolineId)
                .HasConstraintName("FK_Reservation_SOLineID");

            entity.HasOne(d => d.SupplySource).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.SupplySourceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reservation_SupplySourceID");

            entity.HasOne(d => d.TransferLine).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.TransferLineId)
                .HasConstraintName("FK_Reservation_TransferLineID");
        });

        modelBuilder.Entity<ReservationConsumption>(entity =>
        {
            entity.HasKey(e => new { e.AllocationId, e.ReservationId });

            entity.ToTable("ReservationConsumption");

            entity.Property(e => e.AllocationId).HasColumnName("AllocationID");
            entity.Property(e => e.ReservationId).HasColumnName("ReservationID");

            entity.HasOne(d => d.Allocation).WithMany(p => p.ReservationConsumptions)
                .HasForeignKey(d => d.AllocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReservationConsumption_AllocationID");

            entity.HasOne(d => d.Reservation).WithMany(p => p.ReservationConsumptions)
                .HasForeignKey(d => d.ReservationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReservationConsumption_ReservationID");
        });

        modelBuilder.Entity<ReturnReceipt>(entity =>
        {
            entity.ToTable("ReturnReceipt");

            entity.HasIndex(e => e.ReturnOrderId, "IX_ReturnReceipt_043");

            entity.HasIndex(e => e.OutboundId, "IX_ReturnReceipt_044");

            entity.HasIndex(e => e.CreatedByUserId, "IX_ReturnReceipt_045");

            entity.HasIndex(e => e.PerformedByUserId, "IX_ReturnReceipt_046");

            entity.HasIndex(e => e.ReceiptCode, "UX_ReturnReceipt_01").IsUnique();

            entity.Property(e => e.ReturnReceiptId).HasColumnName("ReturnReceiptID");
            entity.Property(e => e.CompletedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.OutboundId).HasColumnName("OutboundID");
            entity.Property(e => e.PerformedByUserId).HasColumnName("PerformedByUserID");
            entity.Property(e => e.ReceiptCode)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.ReturnOrderId).HasColumnName("ReturnOrderID");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.ReturnReceiptCreatedByUsers)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnReceipt_CreatedByUserID");

            entity.HasOne(d => d.Outbound).WithMany(p => p.ReturnReceipts)
                .HasForeignKey(d => d.OutboundId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnReceipt_OutboundID");

            entity.HasOne(d => d.PerformedByUser).WithMany(p => p.ReturnReceiptPerformedByUsers)
                .HasForeignKey(d => d.PerformedByUserId)
                .HasConstraintName("FK_ReturnReceipt_PerformedByUserID");

            entity.HasOne(d => d.ReturnOrder).WithMany(p => p.ReturnReceipts)
                .HasForeignKey(d => d.ReturnOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnReceipt_ReturnOrderID");
        });

        modelBuilder.Entity<ReturnReceiptItem>(entity =>
        {
            entity.HasKey(e => e.ReturnItemId);

            entity.ToTable("ReturnReceiptItem");

            entity.HasIndex(e => e.ReturnReceiptId, "IX_ReturnReceiptItem_047");

            entity.HasIndex(e => e.ReturnLineId, "IX_ReturnReceiptItem_048");

            entity.HasIndex(e => e.OutboundItemId, "IX_ReturnReceiptItem_049");

            entity.HasIndex(e => e.OutboundItemId, "UX_ReturnReceiptItem_01")
                .IsUnique()
                .HasFilter("([IsActive]=(1))");

            entity.Property(e => e.ReturnItemId).HasColumnName("ReturnItemID");
            entity.Property(e => e.ConditionNote).HasMaxLength(1000);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.OutboundItemId).HasColumnName("OutboundItemID");
            entity.Property(e => e.ReceivedCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.ReturnLineId).HasColumnName("ReturnLineID");
            entity.Property(e => e.ReturnReceiptId).HasColumnName("ReturnReceiptID");
            entity.Property(e => e.ScannedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.OutboundItem).WithOne(p => p.ReturnReceiptItem)
                .HasForeignKey<ReturnReceiptItem>(d => d.OutboundItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnReceiptItem_OutboundItemID");

            entity.HasOne(d => d.ReturnLine).WithMany(p => p.ReturnReceiptItems)
                .HasForeignKey(d => d.ReturnLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnReceiptItem_ReturnLineID");

            entity.HasOne(d => d.ReturnReceipt).WithMany(p => p.ReturnReceiptItems)
                .HasForeignKey(d => d.ReturnReceiptId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnReceiptItem_ReturnReceiptID");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Role");

            entity.HasIndex(e => e.RoleName, "UX_Role_01").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.RoleName)
                .HasMaxLength(32)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SalesOrder>(entity =>
        {
            entity.ToTable("SalesOrder");

            entity.HasIndex(e => e.CustomerId, "IX_SalesOrder_028");

            entity.HasIndex(e => e.WarehouseId, "IX_SalesOrder_029");

            entity.HasIndex(e => e.CreatedByUserId, "IX_SalesOrder_030");

            entity.HasIndex(e => e.Socode, "UX_SalesOrder_01").IsUnique();

            entity.Property(e => e.SalesOrderId).HasColumnName("SalesOrderID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Socode)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("SOCode");
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.SubmittedAt).HasPrecision(3);
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.SalesOrders)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SalesOrder_CreatedByUserID");

            entity.HasOne(d => d.Customer).WithMany(p => p.SalesOrders)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SalesOrder_CustomerID");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.SalesOrders)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SalesOrder_WarehouseID");
        });

        modelBuilder.Entity<SalesOrderLine>(entity =>
        {
            entity.HasKey(e => e.SolineId);

            entity.ToTable("SalesOrderLine");

            entity.HasIndex(e => e.ProductId, "IX_SalesOrderLine_031");

            entity.HasIndex(e => new { e.SalesOrderId, e.ProductId }, "UX_SalesOrderLine_01").IsUnique();

            entity.Property(e => e.SolineId).HasColumnName("SOLineID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.SalesOrderId).HasColumnName("SalesOrderID");

            entity.HasOne(d => d.Product).WithMany(p => p.SalesOrderLines)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SalesOrderLine_ProductID");

            entity.HasOne(d => d.SalesOrder).WithMany(p => p.SalesOrderLines)
                .HasForeignKey(d => d.SalesOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SalesOrderLine_SalesOrderID");
        });

        modelBuilder.Entity<SerialItem>(entity =>
        {
            entity.ToTable("SerialItem");

            entity.HasIndex(e => new { e.CurrentWarehouseId, e.ProductId, e.StockState, e.ItemCondition }, "IX_SerialItem_001");

            entity.HasIndex(e => e.CurrentLocationId, "IX_SerialItem_002");

            entity.HasIndex(e => e.CurrentSupplySourceId, "IX_SerialItem_003");

            entity.HasIndex(e => e.ProductId, "IX_SerialItem_067");

            entity.HasIndex(e => e.OriginalInboundId, "IX_SerialItem_068");

            entity.HasIndex(e => e.OriginAdjustmentId, "IX_SerialItem_069");

            entity.HasIndex(e => e.SerialNumber, "UX_SerialItem_01").IsUnique();

            entity.Property(e => e.SerialItemId).HasColumnName("SerialItemID");
            entity.Property(e => e.CurrentLocationId).HasColumnName("CurrentLocationID");
            entity.Property(e => e.CurrentSupplySourceId).HasColumnName("CurrentSupplySourceID");
            entity.Property(e => e.CurrentWarehouseId).HasColumnName("CurrentWarehouseID");
            entity.Property(e => e.ItemCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.OriginAdjustmentId).HasColumnName("OriginAdjustmentID");
            entity.Property(e => e.OriginType)
                .HasMaxLength(24)
                .IsUnicode(false);
            entity.Property(e => e.OriginalInboundId).HasColumnName("OriginalInboundID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.QuarantineReason).HasMaxLength(1000);
            entity.Property(e => e.RegisteredAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.SerialNumber).HasMaxLength(100);
            entity.Property(e => e.StockState)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("IN_WAREHOUSE");

            entity.HasOne(d => d.CurrentLocation).WithMany(p => p.SerialItems)
                .HasForeignKey(d => d.CurrentLocationId)
                .HasConstraintName("FK_SerialItem_CurrentLocationID");

            entity.HasOne(d => d.CurrentSupplySource).WithMany(p => p.SerialItems)
                .HasForeignKey(d => d.CurrentSupplySourceId)
                .HasConstraintName("FK_SerialItem_CurrentSupplySourceID");

            entity.HasOne(d => d.CurrentWarehouse).WithMany(p => p.SerialItems)
                .HasForeignKey(d => d.CurrentWarehouseId)
                .HasConstraintName("FK_SerialItem_CurrentWarehouseID");

            entity.HasOne(d => d.OriginAdjustment).WithMany(p => p.SerialItems)
                .HasForeignKey(d => d.OriginAdjustmentId)
                .HasConstraintName("FK_SerialItem_OriginAdjustmentID");

            entity.HasOne(d => d.OriginalInbound).WithMany(p => p.SerialItems)
                .HasForeignKey(d => d.OriginalInboundId)
                .HasConstraintName("FK_SerialItem_OriginalInboundID");

            entity.HasOne(d => d.Product).WithMany(p => p.SerialItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SerialItem_ProductID");
        });

        modelBuilder.Entity<SerialMovement>(entity =>
        {
            entity.HasKey(e => e.MovementId);

            entity.ToTable("SerialMovement");

            entity.HasIndex(e => new { e.SerialItemId, e.OccurredAt }, "IX_SerialMovement_007");

            entity.HasIndex(e => e.PerformedByUserId, "IX_SerialMovement_105");

            entity.HasIndex(e => e.FromWarehouseId, "IX_SerialMovement_106");

            entity.HasIndex(e => e.ToWarehouseId, "IX_SerialMovement_107");

            entity.HasIndex(e => e.FromLocationId, "IX_SerialMovement_108");

            entity.HasIndex(e => e.ToLocationId, "IX_SerialMovement_109");

            entity.HasIndex(e => e.InboundId, "IX_SerialMovement_110");

            entity.HasIndex(e => e.OutboundId, "IX_SerialMovement_111");

            entity.HasIndex(e => e.ReturnReceiptId, "IX_SerialMovement_112");

            entity.HasIndex(e => e.DispatchId, "IX_SerialMovement_113");

            entity.HasIndex(e => e.TransferReceiptId, "IX_SerialMovement_114");

            entity.HasIndex(e => e.AdjustmentId, "IX_SerialMovement_115");

            entity.HasIndex(e => e.LossRequestId, "IX_SerialMovement_116");

            entity.HasIndex(e => new { e.SerialItemId, e.InboundId, e.EventType }, "UX_SerialMovement_01")
                .IsUnique()
                .HasFilter("([InboundID] IS NOT NULL)");

            entity.HasIndex(e => new { e.SerialItemId, e.OutboundId, e.EventType }, "UX_SerialMovement_02")
                .IsUnique()
                .HasFilter("([OutboundID] IS NOT NULL)");

            entity.HasIndex(e => new { e.SerialItemId, e.ReturnReceiptId, e.EventType }, "UX_SerialMovement_03")
                .IsUnique()
                .HasFilter("([ReturnReceiptID] IS NOT NULL)");

            entity.HasIndex(e => new { e.SerialItemId, e.DispatchId, e.EventType }, "UX_SerialMovement_04")
                .IsUnique()
                .HasFilter("([DispatchID] IS NOT NULL)");

            entity.HasIndex(e => new { e.SerialItemId, e.TransferReceiptId, e.EventType }, "UX_SerialMovement_05")
                .IsUnique()
                .HasFilter("([TransferReceiptID] IS NOT NULL)");

            entity.HasIndex(e => new { e.SerialItemId, e.AdjustmentId, e.EventType }, "UX_SerialMovement_06")
                .IsUnique()
                .HasFilter("([AdjustmentID] IS NOT NULL)");

            entity.HasIndex(e => new { e.SerialItemId, e.LossRequestId, e.EventType }, "UX_SerialMovement_07")
                .IsUnique()
                .HasFilter("([LossRequestID] IS NOT NULL)");

            entity.Property(e => e.MovementId).HasColumnName("MovementID");
            entity.Property(e => e.AdjustmentId).HasColumnName("AdjustmentID");
            entity.Property(e => e.DispatchId).HasColumnName("DispatchID");
            entity.Property(e => e.EventType)
                .HasMaxLength(32)
                .IsUnicode(false);
            entity.Property(e => e.FromCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.FromLocationId).HasColumnName("FromLocationID");
            entity.Property(e => e.FromState)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.FromWarehouseId).HasColumnName("FromWarehouseID");
            entity.Property(e => e.InboundId).HasColumnName("InboundID");
            entity.Property(e => e.LossRequestId).HasColumnName("LossRequestID");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.OccurredAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.OutboundId).HasColumnName("OutboundID");
            entity.Property(e => e.PerformedByUserId).HasColumnName("PerformedByUserID");
            entity.Property(e => e.ReturnReceiptId).HasColumnName("ReturnReceiptID");
            entity.Property(e => e.SerialItemId).HasColumnName("SerialItemID");
            entity.Property(e => e.ToCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.ToLocationId).HasColumnName("ToLocationID");
            entity.Property(e => e.ToState)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.ToWarehouseId).HasColumnName("ToWarehouseID");
            entity.Property(e => e.TransferReceiptId).HasColumnName("TransferReceiptID");

            entity.HasOne(d => d.Adjustment).WithMany(p => p.SerialMovements)
                .HasForeignKey(d => d.AdjustmentId)
                .HasConstraintName("FK_SerialMovement_AdjustmentID");

            entity.HasOne(d => d.Dispatch).WithMany(p => p.SerialMovements)
                .HasForeignKey(d => d.DispatchId)
                .HasConstraintName("FK_SerialMovement_DispatchID");

            entity.HasOne(d => d.FromLocation).WithMany(p => p.SerialMovementFromLocations)
                .HasForeignKey(d => d.FromLocationId)
                .HasConstraintName("FK_SerialMovement_FromLocationID");

            entity.HasOne(d => d.FromWarehouse).WithMany(p => p.SerialMovementFromWarehouses)
                .HasForeignKey(d => d.FromWarehouseId)
                .HasConstraintName("FK_SerialMovement_FromWarehouseID");

            entity.HasOne(d => d.Inbound).WithMany(p => p.SerialMovements)
                .HasForeignKey(d => d.InboundId)
                .HasConstraintName("FK_SerialMovement_InboundID");

            entity.HasOne(d => d.LossRequest).WithMany(p => p.SerialMovements)
                .HasForeignKey(d => d.LossRequestId)
                .HasConstraintName("FK_SerialMovement_LossRequestID");

            entity.HasOne(d => d.Outbound).WithMany(p => p.SerialMovements)
                .HasForeignKey(d => d.OutboundId)
                .HasConstraintName("FK_SerialMovement_OutboundID");

            entity.HasOne(d => d.PerformedByUser).WithMany(p => p.SerialMovements)
                .HasForeignKey(d => d.PerformedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SerialMovement_PerformedByUserID");

            entity.HasOne(d => d.ReturnReceipt).WithMany(p => p.SerialMovements)
                .HasForeignKey(d => d.ReturnReceiptId)
                .HasConstraintName("FK_SerialMovement_ReturnReceiptID");

            entity.HasOne(d => d.SerialItem).WithMany(p => p.SerialMovements)
                .HasForeignKey(d => d.SerialItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SerialMovement_SerialItemID");

            entity.HasOne(d => d.ToLocation).WithMany(p => p.SerialMovementToLocations)
                .HasForeignKey(d => d.ToLocationId)
                .HasConstraintName("FK_SerialMovement_ToLocationID");

            entity.HasOne(d => d.ToWarehouse).WithMany(p => p.SerialMovementToWarehouses)
                .HasForeignKey(d => d.ToWarehouseId)
                .HasConstraintName("FK_SerialMovement_ToWarehouseID");

            entity.HasOne(d => d.TransferReceipt).WithMany(p => p.SerialMovements)
                .HasForeignKey(d => d.TransferReceiptId)
                .HasConstraintName("FK_SerialMovement_TransferReceiptID");
        });

        modelBuilder.Entity<Stocktake>(entity =>
        {
            entity.ToTable("Stocktake");

            entity.HasIndex(e => e.WarehouseId, "IX_Stocktake_083");

            entity.HasIndex(e => e.CreatedByUserId, "IX_Stocktake_084");

            entity.HasIndex(e => e.StocktakeCode, "UX_Stocktake_01").IsUnique();

            entity.Property(e => e.StocktakeId).HasColumnName("StocktakeID");
            entity.Property(e => e.ClosedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.ScopeType)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.StartedAt).HasPrecision(3);
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.StocktakeCode)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.Stocktakes)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Stocktake_CreatedByUserID");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.Stocktakes)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Stocktake_WarehouseID");

            entity.HasMany(d => d.Locations).WithMany(p => p.Stocktakes)
                .UsingEntity<Dictionary<string, object>>(
                    "StocktakeScope",
                    r => r.HasOne<StorageLocation>().WithMany()
                        .HasForeignKey("LocationId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_StocktakeScope_LocationID"),
                    l => l.HasOne<Stocktake>().WithMany()
                        .HasForeignKey("StocktakeId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_StocktakeScope_StocktakeID"),
                    j =>
                    {
                        j.HasKey("StocktakeId", "LocationId");
                        j.ToTable("StocktakeScope");
                        j.IndexerProperty<long>("StocktakeId").HasColumnName("StocktakeID");
                        j.IndexerProperty<int>("LocationId").HasColumnName("LocationID");
                    });
        });

        modelBuilder.Entity<StocktakeAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId);

            entity.ToTable("StocktakeAssignment");

            entity.HasIndex(e => e.UserId, "IX_StocktakeAssignment_085");

            entity.HasIndex(e => e.LocationId, "IX_StocktakeAssignment_086");

            entity.HasIndex(e => new { e.StocktakeId, e.UserId, e.LocationId }, "UX_StocktakeAssignment_01").IsUnique();

            entity.Property(e => e.AssignmentId).HasColumnName("AssignmentID");
            entity.Property(e => e.AssignedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.LocationId).HasColumnName("LocationID");
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("ASSIGNED");
            entity.Property(e => e.StocktakeId).HasColumnName("StocktakeID");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Location).WithMany(p => p.StocktakeAssignments)
                .HasForeignKey(d => d.LocationId)
                .HasConstraintName("FK_StocktakeAssignment_LocationID");

            entity.HasOne(d => d.Stocktake).WithMany(p => p.StocktakeAssignments)
                .HasForeignKey(d => d.StocktakeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StocktakeAssignment_StocktakeID");

            entity.HasOne(d => d.User).WithMany(p => p.StocktakeAssignments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StocktakeAssignment_UserID");
        });

        modelBuilder.Entity<StocktakeObservation>(entity =>
        {
            entity.HasKey(e => e.ObservationId);

            entity.ToTable("StocktakeObservation");

            entity.HasIndex(e => new { e.StocktakeId, e.ReviewStatus }, "IX_StocktakeObservation_008");

            entity.HasIndex(e => e.SerialItemId, "IX_StocktakeObservation_087");

            entity.HasIndex(e => e.ProductId, "IX_StocktakeObservation_088");

            entity.HasIndex(e => e.ExpectedLocationId, "IX_StocktakeObservation_089");

            entity.HasIndex(e => e.ActualLocationId, "IX_StocktakeObservation_090");

            entity.HasIndex(e => e.ScannedByUserId, "IX_StocktakeObservation_091");

            entity.HasIndex(e => new { e.StocktakeId, e.RoundNumber, e.ScannedSerial }, "UX_StocktakeObservation_01").IsUnique();

            entity.Property(e => e.ObservationId).HasColumnName("ObservationID");
            entity.Property(e => e.ActualCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.ActualLocationId).HasColumnName("ActualLocationID");
            entity.Property(e => e.CapturedAt).HasPrecision(3);
            entity.Property(e => e.DifferenceType)
                .HasMaxLength(24)
                .IsUnicode(false);
            entity.Property(e => e.ExpectedCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.ExpectedLocationId).HasColumnName("ExpectedLocationID");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.ReviewStatus)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");
            entity.Property(e => e.RoundNumber).HasDefaultValue(1);
            entity.Property(e => e.ScannedAt).HasPrecision(3);
            entity.Property(e => e.ScannedByUserId).HasColumnName("ScannedByUserID");
            entity.Property(e => e.ScannedSerial).HasMaxLength(100);
            entity.Property(e => e.SerialItemId).HasColumnName("SerialItemID");
            entity.Property(e => e.StocktakeId).HasColumnName("StocktakeID");

            entity.HasOne(d => d.ActualLocation).WithMany(p => p.StocktakeObservationActualLocations)
                .HasForeignKey(d => d.ActualLocationId)
                .HasConstraintName("FK_StocktakeObservation_ActualLocationID");

            entity.HasOne(d => d.ExpectedLocation).WithMany(p => p.StocktakeObservationExpectedLocations)
                .HasForeignKey(d => d.ExpectedLocationId)
                .HasConstraintName("FK_StocktakeObservation_ExpectedLocationID");

            entity.HasOne(d => d.Product).WithMany(p => p.StocktakeObservations)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_StocktakeObservation_ProductID");

            entity.HasOne(d => d.ScannedByUser).WithMany(p => p.StocktakeObservations)
                .HasForeignKey(d => d.ScannedByUserId)
                .HasConstraintName("FK_StocktakeObservation_ScannedByUserID");

            entity.HasOne(d => d.SerialItem).WithMany(p => p.StocktakeObservations)
                .HasForeignKey(d => d.SerialItemId)
                .HasConstraintName("FK_StocktakeObservation_SerialItemID");

            entity.HasOne(d => d.Stocktake).WithMany(p => p.StocktakeObservations)
                .HasForeignKey(d => d.StocktakeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StocktakeObservation_StocktakeID");
        });

        modelBuilder.Entity<StorageLocation>(entity =>
        {
            entity.HasKey(e => e.LocationId);

            entity.ToTable("StorageLocation");

            entity.HasIndex(e => e.ParentLocationId, "IX_StorageLocation_015");

            entity.HasIndex(e => new { e.WarehouseId, e.ParentLocationId, e.LocationName }, "UX_StorageLocation_01").IsUnique();

            entity.Property(e => e.LocationId).HasColumnName("LocationID");
            entity.Property(e => e.CapacityMode)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.ConditionPolicy)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.HeightCm).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.LengthCm).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.LocationName).HasMaxLength(100);
            entity.Property(e => e.LocationType)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("ZONE");
            entity.Property(e => e.MaxWeightKg).HasColumnType("decimal(12, 3)");
            entity.Property(e => e.ParentLocationId).HasColumnName("ParentLocationID");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.StorageType)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");
            entity.Property(e => e.WidthCm).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.ZoneType)
                .HasMaxLength(16)
                .IsUnicode(false);

            entity.HasOne(d => d.ParentLocation).WithMany(p => p.InverseParentLocation)
                .HasForeignKey(d => d.ParentLocationId)
                .HasConstraintName("FK_StorageLocation_ParentLocationID");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.StorageLocations)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StorageLocation_WarehouseID");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("Supplier");

            entity.Property(e => e.SupplierId).HasColumnName("SupplierID");
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.SupplierName).HasMaxLength(200);

            entity.HasMany(d => d.Products).WithMany(p => p.Suppliers)
                .UsingEntity<Dictionary<string, object>>(
                    "SupplierProduct",
                    r => r.HasOne<Product>().WithMany()
                        .HasForeignKey("ProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_SupplierProduct_ProductID"),
                    l => l.HasOne<Supplier>().WithMany()
                        .HasForeignKey("SupplierId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_SupplierProduct_SupplierID"),
                    j =>
                    {
                        j.HasKey("SupplierId", "ProductId");
                        j.ToTable("SupplierProduct");
                        j.IndexerProperty<int>("SupplierId").HasColumnName("SupplierID");
                        j.IndexerProperty<int>("ProductId").HasColumnName("ProductID");
                    });
        });

        modelBuilder.Entity<SupplySource>(entity =>
        {
            entity.ToTable("SupplySource");

            entity.HasIndex(e => new { e.WarehouseId, e.ProductId, e.ItemCondition, e.SupplyState }, "IX_SupplySource_009");

            entity.HasIndex(e => e.ProductId, "IX_SupplySource_070");

            entity.HasIndex(e => e.PolineId, "IX_SupplySource_071");

            entity.HasIndex(e => e.InboundId, "IX_SupplySource_072");

            entity.HasIndex(e => e.ReturnReceiptId, "IX_SupplySource_073");

            entity.HasIndex(e => e.TransferReceiptId, "IX_SupplySource_074");

            entity.HasIndex(e => e.AdjustmentId, "IX_SupplySource_075");

            entity.HasIndex(e => e.PolineId, "UX_SupplySource_01")
                .IsUnique()
                .HasFilter("([POLineID] IS NOT NULL)");

            entity.HasIndex(e => new { e.InboundId, e.ProductId, e.ItemCondition }, "UX_SupplySource_02")
                .IsUnique()
                .HasFilter("([InboundID] IS NOT NULL)");

            entity.HasIndex(e => new { e.ReturnReceiptId, e.ProductId, e.ItemCondition }, "UX_SupplySource_03")
                .IsUnique()
                .HasFilter("([ReturnReceiptID] IS NOT NULL)");

            entity.HasIndex(e => new { e.TransferReceiptId, e.ProductId, e.ItemCondition }, "UX_SupplySource_04")
                .IsUnique()
                .HasFilter("([TransferReceiptID] IS NOT NULL)");

            entity.HasIndex(e => new { e.AdjustmentId, e.ProductId, e.ItemCondition }, "UX_SupplySource_05")
                .IsUnique()
                .HasFilter("([AdjustmentID] IS NOT NULL)");

            entity.Property(e => e.SupplySourceId).HasColumnName("SupplySourceID");
            entity.Property(e => e.AdjustmentId).HasColumnName("AdjustmentID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.InboundId).HasColumnName("InboundID");
            entity.Property(e => e.ItemCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.PolineId).HasColumnName("POLineID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.ReturnReceiptId).HasColumnName("ReturnReceiptID");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.SourceType)
                .HasMaxLength(24)
                .IsUnicode(false);
            entity.Property(e => e.SupplyState)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.TransferReceiptId).HasColumnName("TransferReceiptID");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.Adjustment).WithMany(p => p.SupplySources)
                .HasForeignKey(d => d.AdjustmentId)
                .HasConstraintName("FK_SupplySource_AdjustmentID");

            entity.HasOne(d => d.Inbound).WithMany(p => p.SupplySources)
                .HasForeignKey(d => d.InboundId)
                .HasConstraintName("FK_SupplySource_InboundID");

            entity.HasOne(d => d.Poline).WithOne(p => p.SupplySource)
                .HasForeignKey<SupplySource>(d => d.PolineId)
                .HasConstraintName("FK_SupplySource_POLineID");

            entity.HasOne(d => d.Product).WithMany(p => p.SupplySources)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SupplySource_ProductID");

            entity.HasOne(d => d.ReturnReceipt).WithMany(p => p.SupplySources)
                .HasForeignKey(d => d.ReturnReceiptId)
                .HasConstraintName("FK_SupplySource_ReturnReceiptID");

            entity.HasOne(d => d.TransferReceipt).WithMany(p => p.SupplySources)
                .HasForeignKey(d => d.TransferReceiptId)
                .HasConstraintName("FK_SupplySource_TransferReceiptID");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.SupplySources)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SupplySource_WarehouseID");
        });

        modelBuilder.Entity<TransferDispatch>(entity =>
        {
            entity.HasKey(e => e.DispatchId);

            entity.ToTable("TransferDispatch");

            entity.HasIndex(e => e.TransferOrderId, "IX_TransferDispatch_055");

            entity.HasIndex(e => e.CreatedByUserId, "IX_TransferDispatch_056");

            entity.HasIndex(e => e.AssignedToUserId, "IX_TransferDispatch_057");

            entity.HasIndex(e => e.DispatchCode, "UX_TransferDispatch_01").IsUnique();

            entity.HasIndex(e => e.TransferOrderId, "UX_TransferDispatch_02")
                .IsUnique()
                .HasFilter("([IsActivePicking]=(1))");

            entity.Property(e => e.DispatchId).HasColumnName("DispatchID");
            entity.Property(e => e.AssignedToUserId).HasColumnName("AssignedToUserID");
            entity.Property(e => e.CompletedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.DispatchCode)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.StartedAt).HasPrecision(3);
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.TransferOrderId).HasColumnName("TransferOrderID");

            entity.HasOne(d => d.AssignedToUser).WithMany(p => p.TransferDispatchAssignedToUsers)
                .HasForeignKey(d => d.AssignedToUserId)
                .HasConstraintName("FK_TransferDispatch_AssignedToUserID");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.TransferDispatchCreatedByUsers)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferDispatch_CreatedByUserID");

            entity.HasOne(d => d.TransferOrder).WithOne(p => p.TransferDispatch)
                .HasForeignKey<TransferDispatch>(d => d.TransferOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferDispatch_TransferOrderID");
        });

        modelBuilder.Entity<TransferDispatchItem>(entity =>
        {
            entity.HasKey(e => e.DispatchItemId);

            entity.ToTable("TransferDispatchItem");

            entity.HasIndex(e => e.DispatchId, "IX_TransferDispatchItem_058");

            entity.HasIndex(e => e.TransferLineId, "IX_TransferDispatchItem_059");

            entity.HasIndex(e => e.SerialItemId, "IX_TransferDispatchItem_060");

            entity.HasIndex(e => e.SourceAtDispatchId, "IX_TransferDispatchItem_061");

            entity.HasIndex(e => new { e.DispatchId, e.SerialItemId }, "UX_TransferDispatchItem_01")
                .IsUnique()
                .HasFilter("([IsActive]=(1))");

            entity.Property(e => e.DispatchItemId).HasColumnName("DispatchItemID");
            entity.Property(e => e.DispatchId).HasColumnName("DispatchID");
            entity.Property(e => e.DispatchedCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ScannedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.SerialItemId).HasColumnName("SerialItemID");
            entity.Property(e => e.SourceAtDispatchId).HasColumnName("SourceAtDispatchID");
            entity.Property(e => e.TransferLineId).HasColumnName("TransferLineID");

            entity.HasOne(d => d.Dispatch).WithMany(p => p.TransferDispatchItems)
                .HasForeignKey(d => d.DispatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferDispatchItem_DispatchID");

            entity.HasOne(d => d.SerialItem).WithMany(p => p.TransferDispatchItems)
                .HasForeignKey(d => d.SerialItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferDispatchItem_SerialItemID");

            entity.HasOne(d => d.SourceAtDispatch).WithMany(p => p.TransferDispatchItems)
                .HasForeignKey(d => d.SourceAtDispatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferDispatchItem_SourceAtDispatchID");

            entity.HasOne(d => d.TransferLine).WithMany(p => p.TransferDispatchItems)
                .HasForeignKey(d => d.TransferLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferDispatchItem_TransferLineID");
        });

        modelBuilder.Entity<TransferLossRequest>(entity =>
        {
            entity.HasKey(e => e.LossRequestId);

            entity.ToTable("TransferLossRequest");

            entity.HasIndex(e => e.TransferOrderId, "IX_TransferLossRequest_100");

            entity.HasIndex(e => e.SubmittedByUserId, "IX_TransferLossRequest_101");

            entity.HasIndex(e => e.DecidedByUserId, "IX_TransferLossRequest_102");

            entity.HasIndex(e => e.RequestCode, "UX_TransferLossRequest_01").IsUnique();

            entity.Property(e => e.LossRequestId).HasColumnName("LossRequestID");
            entity.Property(e => e.AppliedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DecidedAt).HasPrecision(3);
            entity.Property(e => e.DecidedByUserId).HasColumnName("DecidedByUserID");
            entity.Property(e => e.DecisionNote).HasMaxLength(1000);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.RequestCode)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.SubmittedAt).HasPrecision(3);
            entity.Property(e => e.SubmittedByUserId).HasColumnName("SubmittedByUserID");
            entity.Property(e => e.TransferOrderId).HasColumnName("TransferOrderID");

            entity.HasOne(d => d.DecidedByUser).WithMany(p => p.TransferLossRequestDecidedByUsers)
                .HasForeignKey(d => d.DecidedByUserId)
                .HasConstraintName("FK_TransferLossRequest_DecidedByUserID");

            entity.HasOne(d => d.SubmittedByUser).WithMany(p => p.TransferLossRequestSubmittedByUsers)
                .HasForeignKey(d => d.SubmittedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferLossRequest_SubmittedByUserID");

            entity.HasOne(d => d.TransferOrder).WithMany(p => p.TransferLossRequests)
                .HasForeignKey(d => d.TransferOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferLossRequest_TransferOrderID");
        });

        modelBuilder.Entity<TransferLossRequestItem>(entity =>
        {
            entity.HasKey(e => e.LossItemId);

            entity.ToTable("TransferLossRequestItem");

            entity.HasIndex(e => e.LossRequestId, "IX_TransferLossRequestItem_103");

            entity.HasIndex(e => e.DispatchItemId, "IX_TransferLossRequestItem_104");

            entity.HasIndex(e => e.DispatchItemId, "UX_TransferLossRequestItem_01")
                .IsUnique()
                .HasFilter("([IsActive]=(1))");

            entity.Property(e => e.LossItemId).HasColumnName("LossItemID");
            entity.Property(e => e.DispatchItemId).HasColumnName("DispatchItemID");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LossRequestId).HasColumnName("LossRequestID");
            entity.Property(e => e.Note).HasMaxLength(1000);

            entity.HasOne(d => d.DispatchItem).WithOne(p => p.TransferLossRequestItem)
                .HasForeignKey<TransferLossRequestItem>(d => d.DispatchItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferLossRequestItem_DispatchItemID");

            entity.HasOne(d => d.LossRequest).WithMany(p => p.TransferLossRequestItems)
                .HasForeignKey(d => d.LossRequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferLossRequestItem_LossRequestID");
        });

        modelBuilder.Entity<TransferOrder>(entity =>
        {
            entity.ToTable("TransferOrder");

            entity.HasIndex(e => e.FromWarehouseId, "IX_TransferOrder_050");

            entity.HasIndex(e => e.ToWarehouseId, "IX_TransferOrder_051");

            entity.HasIndex(e => e.CreatedByUserId, "IX_TransferOrder_052");

            entity.HasIndex(e => e.AssignedToUserId, "IX_TransferOrder_053");

            entity.HasIndex(e => e.TransferCode, "UX_TransferOrder_01").IsUnique();

            entity.Property(e => e.TransferOrderId).HasColumnName("TransferOrderID");
            entity.Property(e => e.AssignedToUserId).HasColumnName("AssignedToUserID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.FromWarehouseId).HasColumnName("FromWarehouseID");
            entity.Property(e => e.ItemCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.ToWarehouseId).HasColumnName("ToWarehouseID");
            entity.Property(e => e.TransferCode)
                .HasMaxLength(40)
                .IsUnicode(false);

            entity.HasOne(d => d.AssignedToUser).WithMany(p => p.TransferOrderAssignedToUsers)
                .HasForeignKey(d => d.AssignedToUserId)
                .HasConstraintName("FK_TransferOrder_AssignedToUserID");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.TransferOrderCreatedByUsers)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferOrder_CreatedByUserID");

            entity.HasOne(d => d.FromWarehouse).WithMany(p => p.TransferOrderFromWarehouses)
                .HasForeignKey(d => d.FromWarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferOrder_FromWarehouseID");

            entity.HasOne(d => d.ToWarehouse).WithMany(p => p.TransferOrderToWarehouses)
                .HasForeignKey(d => d.ToWarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferOrder_ToWarehouseID");
        });

        modelBuilder.Entity<TransferOrderLine>(entity =>
        {
            entity.HasKey(e => e.TransferLineId);

            entity.ToTable("TransferOrderLine");

            entity.HasIndex(e => e.ProductId, "IX_TransferOrderLine_054");

            entity.HasIndex(e => new { e.TransferOrderId, e.ProductId }, "UX_TransferOrderLine_01").IsUnique();

            entity.Property(e => e.TransferLineId).HasColumnName("TransferLineID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.TransferOrderId).HasColumnName("TransferOrderID");

            entity.HasOne(d => d.Product).WithMany(p => p.TransferOrderLines)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferOrderLine_ProductID");

            entity.HasOne(d => d.TransferOrder).WithMany(p => p.TransferOrderLines)
                .HasForeignKey(d => d.TransferOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferOrderLine_TransferOrderID");
        });

        modelBuilder.Entity<TransferReceipt>(entity =>
        {
            entity.ToTable("TransferReceipt");

            entity.HasIndex(e => e.TransferOrderId, "IX_TransferReceipt_062");

            entity.HasIndex(e => e.CreatedByUserId, "IX_TransferReceipt_063");

            entity.HasIndex(e => e.PerformedByUserId, "IX_TransferReceipt_064");

            entity.HasIndex(e => e.ReceiptCode, "UX_TransferReceipt_01").IsUnique();

            entity.Property(e => e.TransferReceiptId).HasColumnName("TransferReceiptID");
            entity.Property(e => e.CompletedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.PerformedByUserId).HasColumnName("PerformedByUserID");
            entity.Property(e => e.ReceiptCode)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.TransferOrderId).HasColumnName("TransferOrderID");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.TransferReceiptCreatedByUsers)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferReceipt_CreatedByUserID");

            entity.HasOne(d => d.PerformedByUser).WithMany(p => p.TransferReceiptPerformedByUsers)
                .HasForeignKey(d => d.PerformedByUserId)
                .HasConstraintName("FK_TransferReceipt_PerformedByUserID");

            entity.HasOne(d => d.TransferOrder).WithMany(p => p.TransferReceipts)
                .HasForeignKey(d => d.TransferOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferReceipt_TransferOrderID");
        });

        modelBuilder.Entity<TransferReceiptItem>(entity =>
        {
            entity.ToTable("TransferReceiptItem");

            entity.HasIndex(e => e.TransferReceiptId, "IX_TransferReceiptItem_065");

            entity.HasIndex(e => e.DispatchItemId, "IX_TransferReceiptItem_066");

            entity.HasIndex(e => e.DispatchItemId, "UX_TransferReceiptItem_01")
                .IsUnique()
                .HasFilter("([IsActive]=(1))");

            entity.Property(e => e.TransferReceiptItemId).HasColumnName("TransferReceiptItemID");
            entity.Property(e => e.ConditionNote).HasMaxLength(1000);
            entity.Property(e => e.DispatchItemId).HasColumnName("DispatchItemID");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ReceivedCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.ScannedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.TransferReceiptId).HasColumnName("TransferReceiptID");

            entity.HasOne(d => d.DispatchItem).WithOne(p => p.TransferReceiptItem)
                .HasForeignKey<TransferReceiptItem>(d => d.DispatchItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferReceiptItem_DispatchItemID");

            entity.HasOne(d => d.TransferReceipt).WithMany(p => p.TransferReceiptItems)
                .HasForeignKey(d => d.TransferReceiptId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferReceiptItem_TransferReceiptID");
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

        modelBuilder.Entity<ZoneStorageRule>(entity =>
        {
            entity.HasKey(e => e.ZoneRuleId);

            entity.ToTable("ZoneStorageRule");

            entity.HasIndex(e => e.CategoryId, "IX_ZoneStorageRule_016");

            entity.HasIndex(e => e.BrandId, "IX_ZoneStorageRule_017");

            entity.HasIndex(e => new { e.ZoneLocationId, e.CategoryId, e.BrandId }, "UX_ZoneStorageRule_01").IsUnique();

            entity.Property(e => e.ZoneRuleId).HasColumnName("ZoneRuleID");
            entity.Property(e => e.BrandId).HasColumnName("BrandID");
            entity.Property(e => e.CategoryId).HasColumnName("CategoryID");
            entity.Property(e => e.ZoneLocationId).HasColumnName("ZoneLocationID");

            entity.HasOne(d => d.Brand).WithMany(p => p.ZoneStorageRules)
                .HasForeignKey(d => d.BrandId)
                .HasConstraintName("FK_ZoneStorageRule_BrandID");

            entity.HasOne(d => d.Category).WithMany(p => p.ZoneStorageRules)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ZoneStorageRule_CategoryID");

            entity.HasOne(d => d.ZoneLocation).WithMany(p => p.ZoneStorageRules)
                .HasForeignKey(d => d.ZoneLocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ZoneStorageRule_ZoneLocationID");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
