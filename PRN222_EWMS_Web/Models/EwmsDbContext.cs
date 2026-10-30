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

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

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

    public virtual DbSet<InventoryAlertThreshold> InventoryAlertThresholds { get; set; }

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

    public virtual DbSet<StaffWarehouseAssignment> StaffWarehouseAssignments { get; set; }

    public virtual DbSet<Stocktake> Stocktakes { get; set; }

    public virtual DbSet<StocktakeAssignment> StocktakeAssignments { get; set; }

    public virtual DbSet<StocktakeObservation> StocktakeObservations { get; set; }

    public virtual DbSet<StocktakeRound> StocktakeRounds { get; set; }

    public virtual DbSet<StocktakeScope> StocktakeScopes { get; set; }

    public virtual DbSet<StorageLocation> StorageLocations { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<SupplierProduct> SupplierProducts { get; set; }

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

    public virtual DbSet<VwLocationCapacity> VwLocationCapacities { get; set; }

    public virtual DbSet<VwLowStockAlert> VwLowStockAlerts { get; set; }

    public virtual DbSet<VwPhysicalInventory> VwPhysicalInventories { get; set; }

    public virtual DbSet<VwPurchaseOrderProgress> VwPurchaseOrderProgresses { get; set; }

    public virtual DbSet<VwReturnProgress> VwReturnProgresses { get; set; }

    public virtual DbSet<VwSalesOrderProgress> VwSalesOrderProgresses { get; set; }

    public virtual DbSet<VwStocktakeResult> VwStocktakeResults { get; set; }

    public virtual DbSet<VwSupplyAvailability> VwSupplyAvailabilities { get; set; }

    public virtual DbSet<VwTransferInTransit> VwTransferInTransits { get; set; }

    public virtual DbSet<Warehouse> Warehouses { get; set; }

    public virtual DbSet<ZoneStorageRule> ZoneStorageRules { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditId);

            entity.ToTable("AuditLog", tb => tb.HasTrigger("TR_AuditLog_Immutable"));

            entity.HasIndex(e => new { e.ActorUserId, e.OccurredAt }, "IX_AuditLog_Actor").IsDescending(false, true);

            entity.HasIndex(e => new { e.TableName, e.EntityKey, e.OccurredAt }, "IX_AuditLog_Entity").IsDescending(false, false, true);

            entity.Property(e => e.AuditId).HasColumnName("AuditID");
            entity.Property(e => e.ActionType)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.ActorUserId).HasColumnName("ActorUserID");
            entity.Property(e => e.DatabasePrincipal).HasMaxLength(128);
            entity.Property(e => e.EntityKey).HasMaxLength(200);
            entity.Property(e => e.OccurredAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.RequestId)
                .HasMaxLength(100)
                .HasColumnName("RequestID");
            entity.Property(e => e.TableName).HasMaxLength(128);

            entity.HasOne(d => d.ActorUser).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.ActorUserId)
                .HasConstraintName("FK_AuditLog_Actor");
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.ToTable("Brand", tb => tb.HasTrigger("TR_Brand_Audit"));

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
            entity.ToTable("Category", tb => tb.HasTrigger("TR_Category_Audit"));

            entity.HasIndex(e => e.CategoryName, "UX_Category_01").IsUnique();

            entity.Property(e => e.CategoryId).HasColumnName("CategoryID");
            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customer", tb => tb.HasTrigger("TR_Customer_Audit"));

            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.CustomerName).HasMaxLength(200);
            entity.Property(e => e.Phone).HasMaxLength(30);
        });

        modelBuilder.Entity<CustomerReturnOrder>(entity =>
        {
            entity.HasKey(e => e.ReturnOrderId);

            entity.ToTable("CustomerReturnOrder", tb =>
                {
                    tb.HasTrigger("TR_CustomerReturnOrder_Audit");
                    tb.HasTrigger("TR_CustomerReturnOrder_SourceContextFrozen");
                });

            entity.HasIndex(e => e.SalesOrderId, "IX_CustomerReturnOrder_039");

            entity.HasIndex(e => e.WarehouseId, "IX_CustomerReturnOrder_040");

            entity.HasIndex(e => e.CreatedByUserId, "IX_CustomerReturnOrder_041");

            entity.HasIndex(e => new { e.ReturnOrderId, e.SalesOrderId }, "UQ_CustomerReturnOrder_Order").IsUnique();

            entity.HasIndex(e => e.ReturnCode, "UX_CustomerReturnOrder_01").IsUnique();

            entity.Property(e => e.ReturnOrderId).HasColumnName("ReturnOrderID");
            entity.Property(e => e.CloseReason).HasMaxLength(1000);
            entity.Property(e => e.ClosedAt).HasPrecision(3);
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

            entity.ToTable("CustomerReturnOrderLine", tb => tb.HasTrigger("TR_CustomerReturnOrderLine_Audit"));

            entity.HasIndex(e => e.SolineId, "IX_CustomerReturnOrderLine_042");

            entity.HasIndex(e => new { e.ReturnLineId, e.ReturnOrderId, e.SolineId }, "UQ_CustomerReturnOrderLine_Line").IsUnique();

            entity.HasIndex(e => new { e.ReturnOrderId, e.SolineId }, "UX_CustomerReturnOrderLine_01").IsUnique();

            entity.Property(e => e.ReturnLineId).HasColumnName("ReturnLineID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.ReturnOrderId).HasColumnName("ReturnOrderID");
            entity.Property(e => e.SalesOrderId).HasColumnName("SalesOrderID");
            entity.Property(e => e.SolineId).HasColumnName("SOLineID");

            entity.HasOne(d => d.ReturnOrder).WithMany(p => p.CustomerReturnOrderLineReturnOrders)
                .HasForeignKey(d => d.ReturnOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CustomerReturnOrderLine_ReturnOrderID");

            entity.HasOne(d => d.Soline).WithMany(p => p.CustomerReturnOrderLineSolines)
                .HasForeignKey(d => d.SolineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CustomerReturnOrderLine_SOLineID");

            entity.HasOne(d => d.CustomerReturnOrder).WithMany(p => p.CustomerReturnOrderLineCustomerReturnOrders)
                .HasPrincipalKey(p => new { p.ReturnOrderId, p.SalesOrderId })
                .HasForeignKey(d => new { d.ReturnOrderId, d.SalesOrderId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnLine_SameOrder");

            entity.HasOne(d => d.SalesOrderLine).WithMany(p => p.CustomerReturnOrderLineSalesOrderLines)
                .HasPrincipalKey(p => new { p.SolineId, p.SalesOrderId, p.ProductId })
                .HasForeignKey(d => new { d.SolineId, d.SalesOrderId, d.ProductId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnLine_SameSOLine");
        });

        modelBuilder.Entity<DemandAllocation>(entity =>
        {
            entity.HasKey(e => e.AllocationId);

            entity.ToTable("DemandAllocation", tb => tb.HasTrigger("TR_DemandAllocation_Audit"));

            entity.HasIndex(e => e.SupplySourceId, "IX_DemandAllocation_077");

            entity.HasIndex(e => e.OutboundId, "IX_DemandAllocation_078");

            entity.HasIndex(e => e.DispatchId, "IX_DemandAllocation_079");

            entity.HasIndex(e => new { e.AllocationId, e.SupplySourceId, e.SalesOrderId }, "UQ_DemandAllocation_ConsumeSO").IsUnique();

            entity.HasIndex(e => new { e.AllocationId, e.SupplySourceId, e.TransferOrderId }, "UQ_DemandAllocation_ConsumeTransfer").IsUnique();

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
            entity.Property(e => e.SalesOrderId)
                .HasColumnName("SalesOrderID");
            entity.Property(e => e.SupplySourceId).HasColumnName("SupplySourceID");
            entity.Property(e => e.TransferOrderId)
                .HasColumnName("TransferOrderID");

            entity.HasOne(d => d.Dispatch).WithMany(p => p.DemandAllocationDispatches)
                .HasForeignKey(d => d.DispatchId)
                .HasConstraintName("FK_DemandAllocation_DispatchID");

            entity.HasOne(d => d.Outbound).WithMany(p => p.DemandAllocationOutbounds)
                .HasForeignKey(d => d.OutboundId)
                .HasConstraintName("FK_DemandAllocation_OutboundID");

            entity.HasOne(d => d.SupplySource).WithMany(p => p.DemandAllocations)
                .HasForeignKey(d => d.SupplySourceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DemandAllocation_SupplySourceID");

            entity.HasOne(d => d.TransferDispatch).WithMany(p => p.DemandAllocationTransferDispatches)
                .HasPrincipalKey(p => new { p.DispatchId, p.TransferOrderId })
                .HasForeignKey(d => new { d.DispatchId, d.TransferOrderId })
                .HasConstraintName("FK_Allocation_SameTransfer");

            entity.HasOne(d => d.OutboundNavigation).WithMany(p => p.DemandAllocationOutboundNavigations)
                .HasPrincipalKey(p => new { p.OutboundId, p.SalesOrderId })
                .HasForeignKey(d => new { d.OutboundId, d.SalesOrderId })
                .HasConstraintName("FK_Allocation_SameSO");
        });

        modelBuilder.Entity<Inbound>(entity =>
        {
            entity.ToTable("Inbound", tb =>
                {
                    tb.HasTrigger("TR_Inbound_Audit");
                    tb.HasTrigger("TR_Inbound_FinalImmutable");
                    tb.HasTrigger("TR_Inbound_KeepDocument");
                    tb.HasTrigger("TR_Inbound_PostingFields");
                    tb.HasTrigger("TR_Inbound_SourceContextFrozen");
                });

            entity.HasIndex(e => e.PurchaseOrderId, "IX_Inbound_022");

            entity.HasIndex(e => e.CreatedByUserId, "IX_Inbound_023");

            entity.HasIndex(e => e.PerformedByUserId, "IX_Inbound_024");

            entity.HasIndex(e => new { e.InboundId, e.PurchaseOrderId }, "UQ_Inbound_Order").IsUnique();

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
            entity.ToTable("InboundItem", tb =>
                {
                    tb.HasTrigger("TR_InboundItem_Audit");
                    tb.HasTrigger("TR_InboundItem_FinalItemsImmutable");
                });

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
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.PurchaseOrderId).HasColumnName("PurchaseOrderID");
            entity.Property(e => e.ReceivedCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.ScannedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ScannedSerial).HasMaxLength(100);
            entity.Property(e => e.SerialItemId).HasColumnName("SerialItemID");

            entity.HasOne(d => d.Inbound).WithMany(p => p.InboundItemInbounds)
                .HasForeignKey(d => d.InboundId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InboundItem_InboundID");

            entity.HasOne(d => d.Poline).WithMany(p => p.InboundItemPolines)
                .HasForeignKey(d => d.PolineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InboundItem_POLineID");

            entity.HasOne(d => d.SerialItem).WithOne(p => p.InboundItemSerialItem)
                .HasForeignKey<InboundItem>(d => d.SerialItemId)
                .HasConstraintName("FK_InboundItem_SerialItemID");

            entity.HasOne(d => d.InboundNavigation).WithMany(p => p.InboundItemInboundNavigations)
                .HasPrincipalKey(p => new { p.InboundId, p.PurchaseOrderId })
                .HasForeignKey(d => new { d.InboundId, d.PurchaseOrderId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InboundItem_SameHeader");

            entity.HasOne(d => d.PurchaseOrderLine).WithMany(p => p.InboundItemPurchaseOrderLines)
                .HasPrincipalKey(p => new { p.PolineId, p.ProductId })
                .HasForeignKey(d => new { d.PolineId, d.ProductId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InboundItem_SameProduct");

            entity.HasOne(d => d.PurchaseOrderLineNavigation).WithMany(p => p.InboundItemPurchaseOrderLineNavigations)
                .HasPrincipalKey(p => new { p.PolineId, p.PurchaseOrderId })
                .HasForeignKey(d => new { d.PolineId, d.PurchaseOrderId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InboundItem_SameOrder");

            entity.HasOne(d => d.SerialItemNavigation).WithMany(p => p.InboundItemSerialItemNavigations)
                .HasPrincipalKey(p => new { p.SerialItemId, p.ProductId, p.SerialNumber })
                .HasForeignKey(d => new { d.SerialItemId, d.ProductId, d.ScannedSerial })
                .HasConstraintName("FK_InboundItem_SameSerial");
        });

        modelBuilder.Entity<InventoryAdjustment>(entity =>
        {
            entity.HasKey(e => e.AdjustmentId);

            entity.ToTable("InventoryAdjustment", tb =>
                {
                    tb.HasTrigger("TR_InventoryAdjustment_Audit");
                    tb.HasTrigger("TR_InventoryAdjustment_FinalImmutable");
                    tb.HasTrigger("TR_InventoryAdjustment_PostingFields");
                    tb.HasTrigger("TR_InventoryAdjustment_SourceContextFrozen");
                });

            entity.HasIndex(e => e.StocktakeId, "IX_InventoryAdjustment_092");

            entity.HasIndex(e => e.SubmittedByUserId, "IX_InventoryAdjustment_093");

            entity.HasIndex(e => e.DecidedByUserId, "IX_InventoryAdjustment_094");

            entity.HasIndex(e => new { e.AdjustmentId, e.StocktakeId }, "UQ_InventoryAdjustment_Stocktake").IsUnique();

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

            entity.ToTable("InventoryAdjustmentLine", tb =>
                {
                    tb.HasTrigger("TR_InventoryAdjustmentLine_Audit");
                    tb.HasTrigger("TR_InventoryAdjustmentLine_FinalItemsImmutable");
                });

            entity.HasIndex(e => e.AdjustmentId, "IX_InventoryAdjustmentLine_095");

            entity.HasIndex(e => e.ObservationId, "IX_InventoryAdjustmentLine_096");

            entity.HasIndex(e => e.SerialItemId, "IX_InventoryAdjustmentLine_097");

            entity.HasIndex(e => e.ProductId, "IX_InventoryAdjustmentLine_098");

            entity.HasIndex(e => e.TargetLocationId, "IX_InventoryAdjustmentLine_099");

            entity.HasIndex(e => new { e.StocktakeId, e.ScannedSerial }, "UX_AdjustmentLine_NoRepeatAcrossRounds")
                .IsUnique()
                .HasFilter("([IsActive]=(1))");

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
            entity.Property(e => e.ScannedSerial).HasMaxLength(100);
            entity.Property(e => e.SerialItemId).HasColumnName("SerialItemID");
            entity.Property(e => e.StocktakeId).HasColumnName("StocktakeID");
            entity.Property(e => e.TargetCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.TargetLocationId).HasColumnName("TargetLocationID");

            entity.HasOne(d => d.Adjustment).WithMany(p => p.InventoryAdjustmentLineAdjustments)
                .HasForeignKey(d => d.AdjustmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryAdjustmentLine_AdjustmentID");

            entity.HasOne(d => d.Observation).WithMany(p => p.InventoryAdjustmentLineObservations)
                .HasForeignKey(d => d.ObservationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryAdjustmentLine_ObservationID");

            entity.HasOne(d => d.Product).WithMany(p => p.InventoryAdjustmentLines)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_InventoryAdjustmentLine_ProductID");

            entity.HasOne(d => d.SerialItem).WithMany(p => p.InventoryAdjustmentLineSerialItems)
                .HasForeignKey(d => d.SerialItemId)
                .HasConstraintName("FK_InventoryAdjustmentLine_SerialItemID");

            entity.HasOne(d => d.TargetLocation).WithMany(p => p.InventoryAdjustmentLines)
                .HasForeignKey(d => d.TargetLocationId)
                .HasConstraintName("FK_InventoryAdjustmentLine_TargetLocationID");

            entity.HasOne(d => d.InventoryAdjustment).WithMany(p => p.InventoryAdjustmentLineInventoryAdjustments)
                .HasPrincipalKey(p => new { p.AdjustmentId, p.StocktakeId })
                .HasForeignKey(d => new { d.AdjustmentId, d.StocktakeId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AdjustmentLine_SameStocktake");

            entity.HasOne(d => d.StocktakeObservation).WithMany(p => p.InventoryAdjustmentLineStocktakeObservations)
                .HasPrincipalKey(p => new { p.ObservationId, p.StocktakeId, p.ScannedSerial })
                .HasForeignKey(d => new { d.ObservationId, d.StocktakeId, d.ScannedSerial })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AdjustmentLine_SameEvidence");

            entity.HasOne(d => d.SerialItemNavigation).WithMany(p => p.InventoryAdjustmentLineSerialItemNavigations)
                .HasPrincipalKey(p => new { p.SerialItemId, p.ProductId, p.SerialNumber })
                .HasForeignKey(d => new { d.SerialItemId, d.ProductId, d.ScannedSerial })
                .HasConstraintName("FK_AdjustmentLine_SameSerial");
        });

        modelBuilder.Entity<InventoryAlertThreshold>(entity =>
        {
            entity.HasKey(e => e.ThresholdId);

            entity.ToTable("InventoryAlertThreshold", tb => tb.HasTrigger("TR_InventoryAlertThreshold_Audit"));

            entity.HasIndex(e => new { e.ProductId, e.WarehouseId }, "UQ_InventoryAlertThreshold_Pair").IsUnique();

            entity.Property(e => e.ThresholdId).HasColumnName("ThresholdID");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedByUserId).HasColumnName("UpdatedByUserID");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.Product).WithMany(p => p.InventoryAlertThresholds)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryAlertThreshold_Product");

            entity.HasOne(d => d.UpdatedByUser).WithMany(p => p.InventoryAlertThresholds)
                .HasForeignKey(d => d.UpdatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryAlertThreshold_User");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.InventoryAlertThresholds)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryAlertThreshold_Warehouse");
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
            entity.ToTable("Outbound", tb =>
                {
                    tb.HasTrigger("TR_Outbound_Audit");
                    tb.HasTrigger("TR_Outbound_FinalImmutable");
                    tb.HasTrigger("TR_Outbound_KeepDocument");
                    tb.HasTrigger("TR_Outbound_PostingFields");
                });

            entity.HasIndex(e => e.SalesOrderId, "IX_Outbound_032");

            entity.HasIndex(e => e.CreatedByUserId, "IX_Outbound_033");

            entity.HasIndex(e => e.AssignedToUserId, "IX_Outbound_034");

            entity.HasIndex(e => new { e.OutboundId, e.SalesOrderId }, "UQ_Outbound_Order").IsUnique();

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
            entity.Property(e => e.CustomerAddressSnapshot).HasMaxLength(500);
            entity.Property(e => e.CustomerNameSnapshot).HasMaxLength(200);
            entity.Property(e => e.CustomerPhoneSnapshot).HasMaxLength(30);
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

            entity.HasOne(d => d.SalesOrder).WithMany(p => p.Outbounds)
                .HasForeignKey(d => d.SalesOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Outbound_SalesOrderID");
        });

        modelBuilder.Entity<OutboundItem>(entity =>
        {
            entity.ToTable("OutboundItem", tb =>
                {
                    tb.HasTrigger("TR_OutboundItem_Audit");
                    tb.HasTrigger("TR_OutboundItem_FinalItemsImmutable");
                });

            entity.HasIndex(e => e.OutboundId, "IX_OutboundItem_035");

            entity.HasIndex(e => e.SolineId, "IX_OutboundItem_036");

            entity.HasIndex(e => e.SerialItemId, "IX_OutboundItem_037");

            entity.HasIndex(e => e.SourceAtIssueId, "IX_OutboundItem_038");

            entity.HasIndex(e => new { e.OutboundItemId, e.OutboundId, e.SolineId }, "UQ_OutboundItem_ReturnContext").IsUnique();

            entity.HasIndex(e => new { e.OutboundId, e.SerialItemId }, "UX_OutboundItem_01")
                .IsUnique()
                .HasFilter("([IsActive]=(1))");

            entity.Property(e => e.OutboundItemId).HasColumnName("OutboundItemID");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ItemCondition)
                .HasMaxLength(8)
                .IsUnicode(false)
                .HasDefaultValue("GOOD");
            entity.Property(e => e.OutboundId).HasColumnName("OutboundID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.SalesOrderId).HasColumnName("SalesOrderID");
            entity.Property(e => e.ScannedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.SerialItemId).HasColumnName("SerialItemID");
            entity.Property(e => e.SolineId).HasColumnName("SOLineID");
            entity.Property(e => e.SourceAtIssueId).HasColumnName("SourceAtIssueID");
            entity.Property(e => e.SupplyState)
                .HasMaxLength(8)
                .IsUnicode(false)
                .HasDefaultValue("PHYSICAL");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.Outbound).WithMany(p => p.OutboundItemOutbounds)
                .HasForeignKey(d => d.OutboundId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OutboundItem_OutboundID");

            entity.HasOne(d => d.SerialItem).WithMany(p => p.OutboundItemSerialItems)
                .HasForeignKey(d => d.SerialItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OutboundItem_SerialItemID");

            entity.HasOne(d => d.Soline).WithMany(p => p.OutboundItemSolines)
                .HasForeignKey(d => d.SolineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OutboundItem_SOLineID");

            entity.HasOne(d => d.SourceAtIssue).WithMany(p => p.OutboundItemSourceAtIssues)
                .HasForeignKey(d => d.SourceAtIssueId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OutboundItem_SourceAtIssueID");

            entity.HasOne(d => d.OutboundNavigation).WithMany(p => p.OutboundItemOutboundNavigations)
                .HasPrincipalKey(p => new { p.OutboundId, p.SalesOrderId })
                .HasForeignKey(d => new { d.OutboundId, d.SalesOrderId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OutboundItem_SameHeader");

            entity.HasOne(d => d.SalesOrder).WithMany(p => p.OutboundItems)
                .HasPrincipalKey(p => new { p.SalesOrderId, p.WarehouseId })
                .HasForeignKey(d => new { d.SalesOrderId, d.WarehouseId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OutboundItem_SameWarehouse");

            entity.HasOne(d => d.SerialItemNavigation).WithMany(p => p.OutboundItemSerialItemNavigations)
                .HasPrincipalKey(p => new { p.SerialItemId, p.ProductId })
                .HasForeignKey(d => new { d.SerialItemId, d.ProductId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OutboundItem_SameProduct");

            entity.HasOne(d => d.SalesOrderLine).WithMany(p => p.OutboundItemSalesOrderLines)
                .HasPrincipalKey(p => new { p.SolineId, p.SalesOrderId, p.ProductId })
                .HasForeignKey(d => new { d.SolineId, d.SalesOrderId, d.ProductId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OutboundItem_SameLine");

            entity.HasOne(d => d.SupplySource).WithMany(p => p.OutboundItemSupplySources)
                .HasPrincipalKey(p => new { p.SupplySourceId, p.ProductId, p.WarehouseId, p.ItemCondition, p.SupplyState })
                .HasForeignKey(d => new { d.SourceAtIssueId, d.ProductId, d.WarehouseId, d.ItemCondition, d.SupplyState })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OutboundItem_SameSource");
        });

        modelBuilder.Entity<PickingClaim>(entity =>
        {
            entity.HasKey(e => e.SerialItemId);

            entity.ToTable("PickingClaim", tb => tb.HasTrigger("TR_PickingClaim_Audit"));

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
            entity.ToTable("Product", tb => tb.HasTrigger("TR_Product_Audit"));

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
            entity.ToTable("PurchaseOrder", tb =>
                {
                    tb.HasTrigger("TR_PurchaseOrder_Audit");
                    tb.HasTrigger("TR_PurchaseOrder_EditLock");
                    tb.HasTrigger("TR_PurchaseOrder_SupplierCatalogue");
                });

            entity.HasIndex(e => e.SupplierId, "IX_PurchaseOrder_018");

            entity.HasIndex(e => e.WarehouseId, "IX_PurchaseOrder_019");

            entity.HasIndex(e => e.CreatedByUserId, "IX_PurchaseOrder_020");

            entity.HasIndex(e => new { e.PurchaseOrderId, e.SupplierId }, "UQ_PurchaseOrder_Supplier").IsUnique();

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

            entity.ToTable("PurchaseOrderLine", tb =>
                {
                    tb.HasTrigger("TR_PurchaseOrderLine_Audit");
                    tb.HasTrigger("TR_PurchaseOrderLine_EditLock");
                    tb.HasTrigger("TR_PurchaseOrderLine_SourceContextFrozen");
                    tb.HasTrigger("TR_PurchaseOrderLine_SupplierCatalogue");
                });

            entity.HasIndex(e => e.ProductId, "IX_PurchaseOrderLine_021");

            entity.HasIndex(e => new { e.PolineId, e.PurchaseOrderId }, "UQ_PurchaseOrderLine_Order").IsUnique();

            entity.HasIndex(e => new { e.PolineId, e.ProductId }, "UQ_PurchaseOrderLine_Product").IsUnique();

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
            entity.ToTable("Reservation", tb =>
                {
                    tb.HasTrigger("TR_Reservation_ActiveSourceContext");
                    tb.HasTrigger("TR_Reservation_Audit");
                });

            entity.HasIndex(e => new { e.SupplySourceId, e.Status }, "IX_Reservation_004");

            entity.HasIndex(e => e.SolineId, "IX_Reservation_005");

            entity.HasIndex(e => e.TransferLineId, "IX_Reservation_006");

            entity.HasIndex(e => e.ReplacesReservationId, "IX_Reservation_076");

            entity.HasIndex(e => new { e.ReservationId, e.SupplySourceId, e.SalesOrderId }, "UQ_Reservation_ConsumeSO").IsUnique();

            entity.HasIndex(e => new { e.ReservationId, e.SupplySourceId, e.TransferOrderId }, "UQ_Reservation_ConsumeTransfer").IsUnique();

            entity.Property(e => e.ReservationId).HasColumnName("ReservationID");
            entity.Property(e => e.ClosedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ItemCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.ReplacesReservationId).HasColumnName("ReplacesReservationID");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.SalesOrderId)
                .HasColumnName("SalesOrderID");
            entity.Property(e => e.SolineId).HasColumnName("SOLineID");
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.SupplySourceId).HasColumnName("SupplySourceID");
            entity.Property(e => e.SupplyState)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.TransferLineId).HasColumnName("TransferLineID");
            entity.Property(e => e.TransferOrderId)
                .HasColumnName("TransferOrderID");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.ReplacesReservation).WithMany(p => p.InverseReplacesReservation)
                .HasForeignKey(d => d.ReplacesReservationId)
                .HasConstraintName("FK_Reservation_ReplacesReservationID");

            entity.HasOne(d => d.Soline).WithMany(p => p.ReservationSolines)
                .HasForeignKey(d => d.SolineId)
                .HasConstraintName("FK_Reservation_SOLineID");

            entity.HasOne(d => d.SupplySource).WithMany(p => p.ReservationSupplySources)
                .HasForeignKey(d => d.SupplySourceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reservation_SupplySourceID");

            entity.HasOne(d => d.TransferLine).WithMany(p => p.ReservationTransferLines)
                .HasForeignKey(d => d.TransferLineId)
                .HasConstraintName("FK_Reservation_TransferLineID");

            entity.HasOne(d => d.SalesOrder).WithMany(p => p.Reservations)
                .HasPrincipalKey(p => new { p.SalesOrderId, p.WarehouseId })
                .HasForeignKey(d => new { d.SalesOrderId, d.WarehouseId })
                .HasConstraintName("FK_Reservation_SOWarehouse");

            entity.HasOne(d => d.SupplySourceNavigation).WithMany(p => p.ReservationSupplySourceNavigations)
                .HasPrincipalKey(p => new { p.SupplySourceId, p.ProductId })
                .HasForeignKey(d => new { d.SupplySourceId, d.ProductId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reservation_SourceProduct");

            entity.HasOne(d => d.SalesOrderLine).WithMany(p => p.ReservationSalesOrderLines)
                .HasPrincipalKey(p => new { p.SolineId, p.SalesOrderId, p.ProductId })
                .HasForeignKey(d => new { d.SolineId, d.SalesOrderId, d.ProductId })
                .HasConstraintName("FK_Reservation_SOLineContext");

            entity.HasOne(d => d.TransferOrderLine).WithMany(p => p.ReservationTransferOrderLines)
                .HasPrincipalKey(p => new { p.TransferLineId, p.TransferOrderId, p.ProductId })
                .HasForeignKey(d => new { d.TransferLineId, d.TransferOrderId, d.ProductId })
                .HasConstraintName("FK_Reservation_TransferLineContext");

            entity.HasOne(d => d.TransferOrder).WithMany(p => p.Reservations)
                .HasPrincipalKey(p => new { p.TransferOrderId, p.FromWarehouseId, p.ItemCondition })
                .HasForeignKey(d => new { d.TransferOrderId, d.WarehouseId, d.ItemCondition })
                .HasConstraintName("FK_Reservation_TransferContext");
        });

        modelBuilder.Entity<ReservationConsumption>(entity =>
        {
            entity.HasKey(e => new { e.AllocationId, e.ReservationId });

            entity.ToTable("ReservationConsumption", tb => tb.HasTrigger("TR_ReservationConsumption_Audit"));

            entity.Property(e => e.AllocationId).HasColumnName("AllocationID");
            entity.Property(e => e.ReservationId).HasColumnName("ReservationID");
            entity.Property(e => e.SalesOrderId).HasColumnName("SalesOrderID");
            entity.Property(e => e.SupplySourceId).HasColumnName("SupplySourceID");
            entity.Property(e => e.TransferOrderId).HasColumnName("TransferOrderID");

            entity.HasOne(d => d.Allocation).WithMany(p => p.ReservationConsumptionAllocations)
                .HasForeignKey(d => d.AllocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReservationConsumption_AllocationID");

            entity.HasOne(d => d.Reservation).WithMany(p => p.ReservationConsumptionReservations)
                .HasForeignKey(d => d.ReservationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReservationConsumption_ReservationID");




        });

        modelBuilder.Entity<ReturnReceipt>(entity =>
        {
            entity.ToTable("ReturnReceipt", tb =>
                {
                    tb.HasTrigger("TR_ReturnReceipt_Audit");
                    tb.HasTrigger("TR_ReturnReceipt_FinalImmutable");
                    tb.HasTrigger("TR_ReturnReceipt_PostingFields");
                    tb.HasTrigger("TR_ReturnReceipt_SourceContextFrozen");
                });

            entity.HasIndex(e => e.ReturnOrderId, "IX_ReturnReceipt_043");

            entity.HasIndex(e => e.OutboundId, "IX_ReturnReceipt_044");

            entity.HasIndex(e => e.CreatedByUserId, "IX_ReturnReceipt_045");

            entity.HasIndex(e => e.PerformedByUserId, "IX_ReturnReceipt_046");

            entity.HasIndex(e => new { e.ReturnReceiptId, e.ReturnOrderId, e.OutboundId }, "UQ_ReturnReceipt_Context").IsUnique();

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
            entity.Property(e => e.SalesOrderId).HasColumnName("SalesOrderID");
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.ReturnReceiptCreatedByUsers)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnReceipt_CreatedByUserID");

            entity.HasOne(d => d.Outbound).WithMany(p => p.ReturnReceiptOutbounds)
                .HasForeignKey(d => d.OutboundId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnReceipt_OutboundID");

            entity.HasOne(d => d.PerformedByUser).WithMany(p => p.ReturnReceiptPerformedByUsers)
                .HasForeignKey(d => d.PerformedByUserId)
                .HasConstraintName("FK_ReturnReceipt_PerformedByUserID");

            entity.HasOne(d => d.ReturnOrder).WithMany(p => p.ReturnReceiptReturnOrders)
                .HasForeignKey(d => d.ReturnOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnReceipt_ReturnOrderID");

            entity.HasOne(d => d.OutboundNavigation).WithMany(p => p.ReturnReceiptOutboundNavigations)
                .HasPrincipalKey(p => new { p.OutboundId, p.SalesOrderId })
                .HasForeignKey(d => new { d.OutboundId, d.SalesOrderId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnReceipt_SameOutbound");

            entity.HasOne(d => d.CustomerReturnOrder).WithMany(p => p.ReturnReceiptCustomerReturnOrders)
                .HasPrincipalKey(p => new { p.ReturnOrderId, p.SalesOrderId })
                .HasForeignKey(d => new { d.ReturnOrderId, d.SalesOrderId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnReceipt_SameReturnOrder");
        });

        modelBuilder.Entity<ReturnReceiptItem>(entity =>
        {
            entity.HasKey(e => e.ReturnItemId);

            entity.ToTable("ReturnReceiptItem", tb =>
                {
                    tb.HasTrigger("TR_ReturnReceiptItem_Audit");
                    tb.HasTrigger("TR_ReturnReceiptItem_FinalItemsImmutable");
                });

            entity.HasIndex(e => e.ReturnReceiptId, "IX_ReturnReceiptItem_047");

            entity.HasIndex(e => e.ReturnLineId, "IX_ReturnReceiptItem_048");

            entity.HasIndex(e => e.OutboundItemId, "IX_ReturnReceiptItem_049");

            entity.HasIndex(e => e.OutboundItemId, "UX_ReturnReceiptItem_01")
                .IsUnique()
                .HasFilter("([IsActive]=(1))");

            entity.Property(e => e.ReturnItemId).HasColumnName("ReturnItemID");
            entity.Property(e => e.ConditionNote).HasMaxLength(1000);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.OutboundId).HasColumnName("OutboundID");
            entity.Property(e => e.OutboundItemId).HasColumnName("OutboundItemID");
            entity.Property(e => e.ReceivedCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.ReturnLineId).HasColumnName("ReturnLineID");
            entity.Property(e => e.ReturnOrderId).HasColumnName("ReturnOrderID");
            entity.Property(e => e.ReturnReceiptId).HasColumnName("ReturnReceiptID");
            entity.Property(e => e.ScannedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.SolineId).HasColumnName("SOLineID");

            entity.HasOne(d => d.OutboundItem).WithMany(p => p.ReturnReceiptItemOutboundItems)
                .HasForeignKey(d => d.OutboundItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnReceiptItem_OutboundItemID");

            entity.HasOne(d => d.ReturnLine).WithMany(p => p.ReturnReceiptItemReturnLines)
                .HasForeignKey(d => d.ReturnLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnReceiptItem_ReturnLineID");

            entity.HasOne(d => d.ReturnReceipt).WithMany(p => p.ReturnReceiptItemReturnReceipts)
                .HasForeignKey(d => d.ReturnReceiptId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnReceiptItem_ReturnReceiptID");

            entity.HasOne(d => d.OutboundItemNavigation).WithMany(p => p.ReturnReceiptItemOutboundItemNavigations)
                .HasPrincipalKey(p => new { p.OutboundItemId, p.OutboundId, p.SolineId })
                .HasForeignKey(d => new { d.OutboundItemId, d.OutboundId, d.SolineId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnItem_SameSoldLine");

            entity.HasOne(d => d.CustomerReturnOrderLine).WithMany(p => p.ReturnReceiptItemCustomerReturnOrderLines)
                .HasPrincipalKey(p => new { p.ReturnLineId, p.ReturnOrderId, p.SolineId })
                .HasForeignKey(d => new { d.ReturnLineId, d.ReturnOrderId, d.SolineId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnItem_SameReturnLine");

            entity.HasOne(d => d.ReturnReceiptNavigation).WithMany(p => p.ReturnReceiptItemReturnReceiptNavigations)
                .HasPrincipalKey(p => new { p.ReturnReceiptId, p.ReturnOrderId, p.OutboundId })
                .HasForeignKey(d => new { d.ReturnReceiptId, d.ReturnOrderId, d.OutboundId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnItem_SameHeader");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Role", tb =>
                {
                    tb.HasTrigger("TR_Role_Audit");
                    tb.HasTrigger("TR_Role_FixedCatalog");
                });

            entity.HasIndex(e => e.RoleName, "UX_Role_01").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.RoleName)
                .HasMaxLength(32)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SalesOrder>(entity =>
        {
            entity.ToTable("SalesOrder", tb =>
                {
                    tb.HasTrigger("TR_SalesOrder_Audit");
                    tb.HasTrigger("TR_SalesOrder_EditLock");
                });

            entity.HasIndex(e => e.CustomerId, "IX_SalesOrder_028");

            entity.HasIndex(e => e.WarehouseId, "IX_SalesOrder_029");

            entity.HasIndex(e => e.CreatedByUserId, "IX_SalesOrder_030");

            entity.HasIndex(e => new { e.SalesOrderId, e.WarehouseId }, "UQ_SalesOrder_Warehouse").IsUnique();

            entity.HasIndex(e => e.Socode, "UX_SalesOrder_01").IsUnique();

            entity.Property(e => e.SalesOrderId).HasColumnName("SalesOrderID");
            entity.Property(e => e.CloseReason).HasMaxLength(1000);
            entity.Property(e => e.ClosedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.CustomerAddressSnapshot).HasMaxLength(500);
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.CustomerNameSnapshot).HasMaxLength(200);
            entity.Property(e => e.CustomerPhoneSnapshot).HasMaxLength(30);
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

            entity.ToTable("SalesOrderLine", tb =>
                {
                    tb.HasTrigger("TR_SalesOrderLine_Audit");
                    tb.HasTrigger("TR_SalesOrderLine_EditLock");
                });

            entity.HasIndex(e => e.ProductId, "IX_SalesOrderLine_031");

            entity.HasIndex(e => new { e.SolineId, e.SalesOrderId, e.ProductId }, "UQ_SalesOrderLine_OrderProduct").IsUnique();

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
            entity.ToTable("SerialItem", tb =>
                {
                    tb.HasTrigger("TR_SerialItem_Audit");
                    tb.HasTrigger("TR_SerialItem_IdentityAndLeaf");
                });

            entity.HasIndex(e => new { e.CurrentWarehouseId, e.ProductId, e.StockState, e.ItemCondition }, "IX_SerialItem_001");

            entity.HasIndex(e => e.CurrentLocationId, "IX_SerialItem_002");

            entity.HasIndex(e => e.CurrentSupplySourceId, "IX_SerialItem_003");

            entity.HasIndex(e => e.ProductId, "IX_SerialItem_067");

            entity.HasIndex(e => e.OriginalInboundId, "IX_SerialItem_068");

            entity.HasIndex(e => e.OriginAdjustmentId, "IX_SerialItem_069");

            entity.HasIndex(e => new { e.SerialItemId, e.ProductId }, "UQ_SerialItem_Product").IsUnique();

            entity.HasIndex(e => new { e.SerialItemId, e.ProductId, e.SerialNumber }, "UQ_SerialItem_Scan").IsUnique();

            entity.HasIndex(e => e.SerialNumber, "UX_SerialItem_01").IsUnique();

            entity.Property(e => e.SerialItemId).HasColumnName("SerialItemID");
            entity.Property(e => e.CurrentLocationId).HasColumnName("CurrentLocationID");
            entity.Property(e => e.CurrentSupplySourceId).HasColumnName("CurrentSupplySourceID");
            entity.Property(e => e.CurrentSupplyState)
                .HasMaxLength(8)
                .IsUnicode(false);
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

            entity.HasOne(d => d.CurrentLocation).WithMany(p => p.SerialItemCurrentLocations)
                .HasForeignKey(d => d.CurrentLocationId)
                .HasConstraintName("FK_SerialItem_CurrentLocationID");

            entity.HasOne(d => d.CurrentSupplySource).WithMany(p => p.SerialItemCurrentSupplySources)
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

            entity.HasOne(d => d.StorageLocation).WithMany(p => p.SerialItemStorageLocations)
                .HasPrincipalKey(p => new { p.LocationId, p.WarehouseId })
                .HasForeignKey(d => new { d.CurrentLocationId, d.CurrentWarehouseId })
                .HasConstraintName("FK_SerialItem_LocationWarehouse");

            entity.HasOne(d => d.SupplySource).WithMany(p => p.SerialItemSupplySources)
                .HasPrincipalKey(p => new { p.SupplySourceId, p.ProductId, p.WarehouseId, p.ItemCondition, p.SupplyState })
                .HasForeignKey(d => new { d.CurrentSupplySourceId, d.ProductId, d.CurrentWarehouseId, d.ItemCondition, d.CurrentSupplyState })
                .HasConstraintName("FK_SerialItem_SourceContext");
        });

        modelBuilder.Entity<SerialMovement>(entity =>
        {
            entity.HasKey(e => e.MovementId);

            entity.ToTable("SerialMovement", tb =>
                {
                    tb.HasTrigger("TR_SerialMovement_Audit");
                    tb.HasTrigger("TR_SerialMovement_Immutable");
                });

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

        modelBuilder.Entity<StaffWarehouseAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentHistoryId);

            entity.ToTable("StaffWarehouseAssignment", tb => tb.HasTrigger("TR_StaffWarehouseAssignment_Audit"));

            entity.HasIndex(e => new { e.WarehouseId, e.AssignedAt }, "IX_StaffWarehouseAssignment_Warehouse");

            entity.HasIndex(e => e.UserId, "UX_StaffWarehouseAssignment_Current")
                .IsUnique()
                .HasFilter("([EndedAt] IS NULL)");

            entity.Property(e => e.AssignmentHistoryId).HasColumnName("AssignmentHistoryID");
            entity.Property(e => e.AssignedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ChangedByUserId).HasColumnName("ChangedByUserID");
            entity.Property(e => e.EndedAt).HasPrecision(3);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.ChangedByUser).WithMany(p => p.StaffWarehouseAssignmentChangedByUsers)
                .HasForeignKey(d => d.ChangedByUserId)
                .HasConstraintName("FK_StaffWarehouseAssignment_Actor");

            entity.HasOne(d => d.User).WithMany(p => p.StaffWarehouseAssignmentUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StaffWarehouseAssignment_User");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.StaffWarehouseAssignments)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StaffWarehouseAssignment_Warehouse");
        });

        modelBuilder.Entity<Stocktake>(entity =>
        {
            entity.ToTable("Stocktake", tb =>
                {
                    tb.HasTrigger("TR_Stocktake_Audit");
                    tb.HasTrigger("TR_Stocktake_KeepScope");
                });

            entity.HasIndex(e => e.WarehouseId, "IX_Stocktake_083");

            entity.HasIndex(e => e.CreatedByUserId, "IX_Stocktake_084");

            entity.HasIndex(e => new { e.StocktakeId, e.WarehouseId }, "UQ_Stocktake_Warehouse").IsUnique();

            entity.HasIndex(e => e.StocktakeCode, "UX_Stocktake_01").IsUnique();

            entity.Property(e => e.StocktakeId).HasColumnName("StocktakeID");
            entity.Property(e => e.ClosedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.IncludeAwaitingPutaway).HasDefaultValue(true);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.PlannedAt).HasPrecision(3);
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
        });

        modelBuilder.Entity<StocktakeAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId);

            entity.ToTable("StocktakeAssignment", tb =>
                {
                    tb.HasTrigger("TR_StocktakeAssignment_AssignedStaff");
                    tb.HasTrigger("TR_StocktakeAssignment_Audit");
                });

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
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.Location).WithMany(p => p.StocktakeAssignmentLocations)
                .HasForeignKey(d => d.LocationId)
                .HasConstraintName("FK_StocktakeAssignment_LocationID");

            entity.HasOne(d => d.Stocktake).WithMany(p => p.StocktakeAssignmentStocktakes)
                .HasForeignKey(d => d.StocktakeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StocktakeAssignment_StocktakeID");

            entity.HasOne(d => d.User).WithMany(p => p.StocktakeAssignments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StocktakeAssignment_UserID");

            entity.HasOne(d => d.StorageLocation).WithMany(p => p.StocktakeAssignmentStorageLocations)
                .HasPrincipalKey(p => new { p.LocationId, p.WarehouseId })
                .HasForeignKey(d => new { d.LocationId, d.WarehouseId })
                .HasConstraintName("FK_StocktakeAssignment_LocationWarehouse");

            entity.HasOne(d => d.StocktakeNavigation).WithMany(p => p.StocktakeAssignmentStocktakeNavigations)
                .HasPrincipalKey(p => new { p.StocktakeId, p.WarehouseId })
                .HasForeignKey(d => new { d.StocktakeId, d.WarehouseId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StocktakeAssignment_StocktakeWarehouse");
        });

        modelBuilder.Entity<StocktakeObservation>(entity =>
        {
            entity.HasKey(e => e.ObservationId);

            entity.ToTable("StocktakeObservation", tb =>
                {
                    tb.HasTrigger("TR_StocktakeObservation_Audit");
                    tb.HasTrigger("TR_StocktakeObservation_Baseline");
                });

            entity.HasIndex(e => new { e.StocktakeId, e.ReviewStatus }, "IX_StocktakeObservation_008");

            entity.HasIndex(e => e.SerialItemId, "IX_StocktakeObservation_087");

            entity.HasIndex(e => e.ProductId, "IX_StocktakeObservation_088");

            entity.HasIndex(e => e.ExpectedLocationId, "IX_StocktakeObservation_089");

            entity.HasIndex(e => e.ActualLocationId, "IX_StocktakeObservation_090");

            entity.HasIndex(e => e.ScannedByUserId, "IX_StocktakeObservation_091");

            entity.HasIndex(e => new { e.ObservationId, e.StocktakeId, e.ScannedSerial }, "UQ_StocktakeObservation_Evidence").IsUnique();

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

            entity.HasOne(d => d.StocktakeRound).WithMany(p => p.StocktakeObservations)
                .HasPrincipalKey(p => new { p.StocktakeId, p.RoundNumber })
                .HasForeignKey(d => new { d.StocktakeId, d.RoundNumber })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StocktakeObservation_Round");
        });

        modelBuilder.Entity<StocktakeRound>(entity =>
        {
            entity.HasKey(e => e.RoundId);

            entity.ToTable("StocktakeRound", tb =>
                {
                    tb.HasTrigger("TR_StocktakeRound_Audit");
                    tb.HasTrigger("TR_StocktakeRound_ClosedImmutable");
                });

            entity.HasIndex(e => new { e.StocktakeId, e.RoundNumber }, "UQ_StocktakeRound_Number").IsUnique();

            entity.HasIndex(e => e.StocktakeId, "UX_StocktakeRound_Open")
                .IsUnique()
                .HasFilter("([Status]='OPEN')");

            entity.Property(e => e.RoundId).HasColumnName("RoundID");
            entity.Property(e => e.ClosedAt).HasPrecision(3);
            entity.Property(e => e.ClosedByUserId).HasColumnName("ClosedByUserID");
            entity.Property(e => e.RecountReason).HasMaxLength(1000);
            entity.Property(e => e.StartedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.StartedByUserId).HasColumnName("StartedByUserID");
            entity.Property(e => e.Status)
                .HasMaxLength(8)
                .IsUnicode(false)
                .HasDefaultValue("OPEN");
            entity.Property(e => e.StocktakeId).HasColumnName("StocktakeID");

            entity.HasOne(d => d.ClosedByUser).WithMany(p => p.StocktakeRoundClosedByUsers)
                .HasForeignKey(d => d.ClosedByUserId)
                .HasConstraintName("FK_StocktakeRound_Closer");

            entity.HasOne(d => d.StartedByUser).WithMany(p => p.StocktakeRoundStartedByUsers)
                .HasForeignKey(d => d.StartedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StocktakeRound_Starter");

            entity.HasOne(d => d.Stocktake).WithMany(p => p.StocktakeRounds)
                .HasForeignKey(d => d.StocktakeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StocktakeRound_Stocktake");
        });

        modelBuilder.Entity<StocktakeScope>(entity =>
        {
            entity.HasKey(e => e.ScopeId);

            entity.ToTable("StocktakeScope", tb =>
                {
                    tb.HasTrigger("TR_StocktakeScope_Audit");
                    tb.HasTrigger("TR_StocktakeScope_NoDeleteAfterStart");
                    tb.HasTrigger("TR_StocktakeScope_ScopeType");
                });

            entity.HasIndex(e => new { e.StocktakeId, e.LocationId }, "UQ_StocktakeScope_Pair").IsUnique();

            entity.Property(e => e.ScopeId).HasColumnName("ScopeID");
            entity.Property(e => e.LocationId).HasColumnName("LocationID");
            entity.Property(e => e.StocktakeId).HasColumnName("StocktakeID");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.Location).WithMany(p => p.StocktakeScopeLocations)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StocktakeScope_LocationID");

            entity.HasOne(d => d.Stocktake).WithMany(p => p.StocktakeScopeStocktakes)
                .HasForeignKey(d => d.StocktakeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StocktakeScope_StocktakeID");

            entity.HasOne(d => d.StorageLocation).WithMany(p => p.StocktakeScopeStorageLocations)
                .HasPrincipalKey(p => new { p.LocationId, p.WarehouseId })
                .HasForeignKey(d => new { d.LocationId, d.WarehouseId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StocktakeScope_LocationWarehouse");

            entity.HasOne(d => d.StocktakeNavigation).WithMany(p => p.StocktakeScopeStocktakeNavigations)
                .HasPrincipalKey(p => new { p.StocktakeId, p.WarehouseId })
                .HasForeignKey(d => new { d.StocktakeId, d.WarehouseId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StocktakeScope_StocktakeWarehouse");
        });

        modelBuilder.Entity<StorageLocation>(entity =>
        {
            entity.HasKey(e => e.LocationId);

            entity.ToTable("StorageLocation", tb =>
                {
                    tb.HasTrigger("TR_StorageLocation_Audit");
                    tb.HasTrigger("TR_StorageLocation_Hierarchy");
                });

            entity.HasIndex(e => e.ParentLocationId, "IX_StorageLocation_015");

            entity.HasIndex(e => new { e.LocationId, e.WarehouseId }, "UQ_StorageLocation_Warehouse").IsUnique();

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

            entity.HasOne(d => d.StorageLocationNavigation).WithMany(p => p.InverseStorageLocationNavigation)
                .HasPrincipalKey(p => new { p.LocationId, p.WarehouseId })
                .HasForeignKey(d => new { d.ParentLocationId, d.WarehouseId })
                .HasConstraintName("FK_StorageLocation_ParentWarehouse");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("Supplier", tb => tb.HasTrigger("TR_Supplier_Audit"));

            entity.Property(e => e.SupplierId).HasColumnName("SupplierID");
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.SupplierName).HasMaxLength(200);
        });

        modelBuilder.Entity<SupplierProduct>(entity =>
        {
            entity.ToTable("SupplierProduct", tb =>
                {
                    tb.HasTrigger("TR_SupplierProduct_Audit");
                    tb.HasTrigger("TR_SupplierProduct_KeepReferencedPair");
                });

            entity.HasIndex(e => new { e.SupplierId, e.ProductId }, "UQ_SupplierProduct_Pair").IsUnique();

            entity.Property(e => e.SupplierProductId).HasColumnName("SupplierProductID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.SupplierId).HasColumnName("SupplierID");

            entity.HasOne(d => d.Product).WithMany(p => p.SupplierProducts)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SupplierProduct_ProductID");

            entity.HasOne(d => d.Supplier).WithMany(p => p.SupplierProducts)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SupplierProduct_SupplierID");
        });

        modelBuilder.Entity<SupplySource>(entity =>
        {
            entity.ToTable("SupplySource", tb =>
                {
                    tb.HasTrigger("TR_SupplySource_ActiveReservationContext");
                    tb.HasTrigger("TR_SupplySource_Audit");
                    tb.HasTrigger("TR_SupplySource_DocumentContext");
                });

            entity.HasIndex(e => new { e.WarehouseId, e.ProductId, e.ItemCondition, e.SupplyState }, "IX_SupplySource_009");

            entity.HasIndex(e => e.ProductId, "IX_SupplySource_070");

            entity.HasIndex(e => e.PolineId, "IX_SupplySource_071");

            entity.HasIndex(e => e.InboundId, "IX_SupplySource_072");

            entity.HasIndex(e => e.ReturnReceiptId, "IX_SupplySource_073");

            entity.HasIndex(e => e.TransferReceiptId, "IX_SupplySource_074");

            entity.HasIndex(e => e.AdjustmentId, "IX_SupplySource_075");

            entity.HasIndex(e => new { e.SupplySourceId, e.ProductId, e.WarehouseId, e.ItemCondition, e.SupplyState }, "UQ_SupplySource_Context").IsUnique();

            entity.HasIndex(e => new { e.SupplySourceId, e.ProductId }, "UQ_SupplySource_Product").IsUnique();

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

            entity.ToTable("TransferDispatch", tb =>
                {
                    tb.HasTrigger("TR_TransferDispatch_Audit");
                    tb.HasTrigger("TR_TransferDispatch_FinalImmutable");
                    tb.HasTrigger("TR_TransferDispatch_PostingFields");
                });

            entity.HasIndex(e => e.TransferOrderId, "IX_TransferDispatch_055");

            entity.HasIndex(e => e.CreatedByUserId, "IX_TransferDispatch_056");

            entity.HasIndex(e => e.AssignedToUserId, "IX_TransferDispatch_057");

            entity.HasIndex(e => new { e.DispatchId, e.TransferOrderId }, "UQ_TransferDispatch_Order").IsUnique();

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

            entity.HasOne(d => d.TransferOrder).WithMany(p => p.TransferDispatches)
                .HasForeignKey(d => d.TransferOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferDispatch_TransferOrderID");
        });

        modelBuilder.Entity<TransferDispatchItem>(entity =>
        {
            entity.HasKey(e => e.DispatchItemId);

            entity.ToTable("TransferDispatchItem", tb =>
                {
                    tb.HasTrigger("TR_TransferDispatchItem_Audit");
                    tb.HasTrigger("TR_TransferDispatchItem_FinalItemsImmutable");
                });

            entity.HasIndex(e => e.DispatchId, "IX_TransferDispatchItem_058");

            entity.HasIndex(e => e.TransferLineId, "IX_TransferDispatchItem_059");

            entity.HasIndex(e => e.SerialItemId, "IX_TransferDispatchItem_060");

            entity.HasIndex(e => e.SourceAtDispatchId, "IX_TransferDispatchItem_061");

            entity.HasIndex(e => new { e.DispatchItemId, e.TransferOrderId }, "UQ_DispatchItem_Order").IsUnique();

            entity.HasIndex(e => new { e.DispatchId, e.SerialItemId }, "UX_TransferDispatchItem_01")
                .IsUnique()
                .HasFilter("([IsActive]=(1))");

            entity.Property(e => e.DispatchItemId).HasColumnName("DispatchItemID");
            entity.Property(e => e.DispatchId).HasColumnName("DispatchID");
            entity.Property(e => e.DispatchedCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.ScannedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.SerialItemId).HasColumnName("SerialItemID");
            entity.Property(e => e.SourceAtDispatchId).HasColumnName("SourceAtDispatchID");
            entity.Property(e => e.SupplyState)
                .HasMaxLength(8)
                .IsUnicode(false)
                .HasDefaultValue("PHYSICAL");
            entity.Property(e => e.TransferLineId).HasColumnName("TransferLineID");
            entity.Property(e => e.TransferOrderId).HasColumnName("TransferOrderID");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.Dispatch).WithMany(p => p.TransferDispatchItemDispatches)
                .HasForeignKey(d => d.DispatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferDispatchItem_DispatchID");

            entity.HasOne(d => d.SerialItem).WithMany(p => p.TransferDispatchItemSerialItems)
                .HasForeignKey(d => d.SerialItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferDispatchItem_SerialItemID");

            entity.HasOne(d => d.SourceAtDispatch).WithMany(p => p.TransferDispatchItemSourceAtDispatches)
                .HasForeignKey(d => d.SourceAtDispatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferDispatchItem_SourceAtDispatchID");

            entity.HasOne(d => d.TransferLine).WithMany(p => p.TransferDispatchItemTransferLines)
                .HasForeignKey(d => d.TransferLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferDispatchItem_TransferLineID");

            entity.HasOne(d => d.TransferDispatch).WithMany(p => p.TransferDispatchItemTransferDispatches)
                .HasPrincipalKey(p => new { p.DispatchId, p.TransferOrderId })
                .HasForeignKey(d => new { d.DispatchId, d.TransferOrderId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DispatchItem_SameHeader");

            entity.HasOne(d => d.SerialItemNavigation).WithMany(p => p.TransferDispatchItemSerialItemNavigations)
                .HasPrincipalKey(p => new { p.SerialItemId, p.ProductId })
                .HasForeignKey(d => new { d.SerialItemId, d.ProductId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DispatchItem_SameProduct");

            entity.HasOne(d => d.TransferOrderLine).WithMany(p => p.TransferDispatchItemTransferOrderLines)
                .HasPrincipalKey(p => new { p.TransferLineId, p.TransferOrderId, p.ProductId })
                .HasForeignKey(d => new { d.TransferLineId, d.TransferOrderId, d.ProductId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DispatchItem_SameLine");

            entity.HasOne(d => d.TransferOrder).WithMany(p => p.TransferDispatchItems)
                .HasPrincipalKey(p => new { p.TransferOrderId, p.FromWarehouseId, p.ItemCondition })
                .HasForeignKey(d => new { d.TransferOrderId, d.WarehouseId, d.DispatchedCondition })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DispatchItem_SameSourceWarehouse");

            entity.HasOne(d => d.SupplySource).WithMany(p => p.TransferDispatchItemSupplySources)
                .HasPrincipalKey(p => new { p.SupplySourceId, p.ProductId, p.WarehouseId, p.ItemCondition, p.SupplyState })
                .HasForeignKey(d => new { d.SourceAtDispatchId, d.ProductId, d.WarehouseId, d.DispatchedCondition, d.SupplyState })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DispatchItem_SameSource");
        });

        modelBuilder.Entity<TransferLossRequest>(entity =>
        {
            entity.HasKey(e => e.LossRequestId);

            entity.ToTable("TransferLossRequest", tb =>
                {
                    tb.HasTrigger("TR_TransferLossRequest_Audit");
                    tb.HasTrigger("TR_TransferLossRequest_FinalImmutable");
                    tb.HasTrigger("TR_TransferLossRequest_PostingFields");
                });

            entity.HasIndex(e => e.TransferOrderId, "IX_TransferLossRequest_100");

            entity.HasIndex(e => e.SubmittedByUserId, "IX_TransferLossRequest_101");

            entity.HasIndex(e => e.DecidedByUserId, "IX_TransferLossRequest_102");

            entity.HasIndex(e => new { e.LossRequestId, e.TransferOrderId }, "UQ_TransferLossRequest_Order").IsUnique();

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

            entity.ToTable("TransferLossRequestItem", tb =>
                {
                    tb.HasTrigger("TR_TransferLossRequestItem_Audit");
                    tb.HasTrigger("TR_TransferLossRequestItem_FinalItemsImmutable");
                });

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
            entity.Property(e => e.TransferOrderId).HasColumnName("TransferOrderID");

            entity.HasOne(d => d.DispatchItem).WithMany(p => p.TransferLossRequestItemDispatchItems)
                .HasForeignKey(d => d.DispatchItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferLossRequestItem_DispatchItemID");

            entity.HasOne(d => d.LossRequest).WithMany(p => p.TransferLossRequestItemLossRequests)
                .HasForeignKey(d => d.LossRequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferLossRequestItem_LossRequestID");

            entity.HasOne(d => d.TransferDispatchItem).WithMany(p => p.TransferLossRequestItemTransferDispatchItems)
                .HasPrincipalKey(p => new { p.DispatchItemId, p.TransferOrderId })
                .HasForeignKey(d => new { d.DispatchItemId, d.TransferOrderId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferLossRequestItem_SameDispatch");

            entity.HasOne(d => d.TransferLossRequest).WithMany(p => p.TransferLossRequestItemTransferLossRequests)
                .HasPrincipalKey(p => new { p.LossRequestId, p.TransferOrderId })
                .HasForeignKey(d => new { d.LossRequestId, d.TransferOrderId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferLossRequestItem_SameHeader");
        });

        modelBuilder.Entity<TransferOrder>(entity =>
        {
            entity.ToTable("TransferOrder", tb =>
                {
                    tb.HasTrigger("TR_TransferOrder_Audit");
                    tb.HasTrigger("TR_TransferOrder_SourceContextFrozen");
                });

            entity.HasIndex(e => e.FromWarehouseId, "IX_TransferOrder_050");

            entity.HasIndex(e => e.ToWarehouseId, "IX_TransferOrder_051");

            entity.HasIndex(e => e.CreatedByUserId, "IX_TransferOrder_052");

            entity.HasIndex(e => e.AssignedToUserId, "IX_TransferOrder_053");

            entity.HasIndex(e => new { e.TransferOrderId, e.FromWarehouseId, e.ItemCondition }, "UQ_TransferOrder_Source").IsUnique();

            entity.HasIndex(e => e.TransferCode, "UX_TransferOrder_01").IsUnique();

            entity.Property(e => e.TransferOrderId).HasColumnName("TransferOrderID");
            entity.Property(e => e.AssignedToUserId).HasColumnName("AssignedToUserID");
            entity.Property(e => e.CloseReason).HasMaxLength(1000);
            entity.Property(e => e.ClosedAt).HasPrecision(3);
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

            entity.ToTable("TransferOrderLine", tb => tb.HasTrigger("TR_TransferOrderLine_Audit"));

            entity.HasIndex(e => e.ProductId, "IX_TransferOrderLine_054");

            entity.HasIndex(e => new { e.TransferLineId, e.TransferOrderId, e.ProductId }, "UQ_TransferOrderLine_OrderProduct").IsUnique();

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
            entity.ToTable("TransferReceipt", tb =>
                {
                    tb.HasTrigger("TR_TransferReceipt_Audit");
                    tb.HasTrigger("TR_TransferReceipt_FinalImmutable");
                    tb.HasTrigger("TR_TransferReceipt_PostingFields");
                    tb.HasTrigger("TR_TransferReceipt_SourceContextFrozen");
                });

            entity.HasIndex(e => e.TransferOrderId, "IX_TransferReceipt_062");

            entity.HasIndex(e => e.CreatedByUserId, "IX_TransferReceipt_063");

            entity.HasIndex(e => e.PerformedByUserId, "IX_TransferReceipt_064");

            entity.HasIndex(e => new { e.TransferReceiptId, e.TransferOrderId }, "UQ_TransferReceipt_Order").IsUnique();

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
            entity.ToTable("TransferReceiptItem", tb =>
                {
                    tb.HasTrigger("TR_TransferReceiptItem_Audit");
                    tb.HasTrigger("TR_TransferReceiptItem_FinalItemsImmutable");
                });

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
            entity.Property(e => e.TransferOrderId).HasColumnName("TransferOrderID");
            entity.Property(e => e.TransferReceiptId).HasColumnName("TransferReceiptID");

            entity.HasOne(d => d.DispatchItem).WithMany(p => p.TransferReceiptItemDispatchItems)
                .HasForeignKey(d => d.DispatchItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferReceiptItem_DispatchItemID");

            entity.HasOne(d => d.TransferReceipt).WithMany(p => p.TransferReceiptItemTransferReceipts)
                .HasForeignKey(d => d.TransferReceiptId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferReceiptItem_TransferReceiptID");

            entity.HasOne(d => d.TransferDispatchItem).WithMany(p => p.TransferReceiptItemTransferDispatchItems)
                .HasPrincipalKey(p => new { p.DispatchItemId, p.TransferOrderId })
                .HasForeignKey(d => new { d.DispatchItemId, d.TransferOrderId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferReceiptItem_SameDispatch");

            entity.HasOne(d => d.TransferReceiptNavigation).WithMany(p => p.TransferReceiptItemTransferReceiptNavigations)
                .HasPrincipalKey(p => new { p.TransferReceiptId, p.TransferOrderId })
                .HasForeignKey(d => new { d.TransferReceiptId, d.TransferOrderId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferReceiptItem_SameHeader");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("User", tb =>
                {
                    tb.HasTrigger("TR_User_Audit");
                    tb.HasTrigger("TR_User_WarehouseAndHistory");
                });

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

        modelBuilder.Entity<VwLocationCapacity>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_LocationCapacity");

            entity.Property(e => e.CapacityAreaM2).HasColumnType("numeric(38, 6)");
            entity.Property(e => e.CapacityMode)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.CapacityVolumeM3).HasColumnType("numeric(38, 6)");
            entity.Property(e => e.LocationId).HasColumnName("LocationID");
            entity.Property(e => e.LocationName).HasMaxLength(100);
            entity.Property(e => e.LocationType)
                .HasMaxLength(32)
                .IsUnicode(false);
            entity.Property(e => e.MaxWeightKg).HasColumnType("decimal(12, 3)");
            entity.Property(e => e.UsedAreaM2).HasColumnType("numeric(38, 6)");
            entity.Property(e => e.UsedVolumeM3).HasColumnType("numeric(38, 6)");
            entity.Property(e => e.UsedWeightKg).HasColumnType("decimal(38, 6)");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");
        });

        modelBuilder.Entity<VwLowStockAlert>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_LowStockAlerts");

            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");
        });

        modelBuilder.Entity<VwPhysicalInventory>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_PhysicalInventory");

            entity.Property(e => e.ItemCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");
        });

        modelBuilder.Entity<VwPurchaseOrderProgress>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_PurchaseOrderProgress");

            entity.Property(e => e.Pocode)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("POCode");
            entity.Property(e => e.PolineId).HasColumnName("POLineID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.PurchaseOrderId).HasColumnName("PurchaseOrderID");
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false);
            entity.Property(e => e.SupplierId).HasColumnName("SupplierID");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");
        });

        modelBuilder.Entity<VwReturnProgress>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_ReturnProgress");

            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.ReturnCode)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.ReturnLineId).HasColumnName("ReturnLineID");
            entity.Property(e => e.ReturnOrderId).HasColumnName("ReturnOrderID");
            entity.Property(e => e.SalesOrderId).HasColumnName("SalesOrderID");
            entity.Property(e => e.SolineId).HasColumnName("SOLineID");
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false);
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");
        });

        modelBuilder.Entity<VwSalesOrderProgress>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_SalesOrderProgress");

            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.SalesOrderId).HasColumnName("SalesOrderID");
            entity.Property(e => e.Socode)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("SOCode");
            entity.Property(e => e.SolineId).HasColumnName("SOLineID");
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .IsUnicode(false);
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");
        });

        modelBuilder.Entity<VwStocktakeResult>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_StocktakeResults");

            entity.Property(e => e.ActualCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.ActualLocationId).HasColumnName("ActualLocationID");
            entity.Property(e => e.ClosedAt).HasPrecision(3);
            entity.Property(e => e.DifferenceType)
                .HasMaxLength(24)
                .IsUnicode(false);
            entity.Property(e => e.ExpectedCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.ExpectedLocationId).HasColumnName("ExpectedLocationID");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.ObservationId).HasColumnName("ObservationID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.ReviewStatus)
                .HasMaxLength(32)
                .IsUnicode(false);
            entity.Property(e => e.RoundStatus)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.ScannedAt).HasPrecision(3);
            entity.Property(e => e.ScannedByUserId).HasColumnName("ScannedByUserID");
            entity.Property(e => e.ScannedSerial).HasMaxLength(100);
            entity.Property(e => e.StartedAt).HasPrecision(3);
            entity.Property(e => e.StocktakeCode)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.StocktakeId).HasColumnName("StocktakeID");
            entity.Property(e => e.StocktakeStatus)
                .HasMaxLength(32)
                .IsUnicode(false);
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");
        });

        modelBuilder.Entity<VwSupplyAvailability>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_SupplyAvailability");

            entity.Property(e => e.ItemCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.SourceType)
                .HasMaxLength(24)
                .IsUnicode(false);
            entity.Property(e => e.SupplySourceId).HasColumnName("SupplySourceID");
            entity.Property(e => e.SupplyState)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");
        });

        modelBuilder.Entity<VwTransferInTransit>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_TransferInTransit");

            entity.Property(e => e.DispatchId).HasColumnName("DispatchID");
            entity.Property(e => e.DispatchItemId).HasColumnName("DispatchItemID");
            entity.Property(e => e.DispatchedAt).HasPrecision(3);
            entity.Property(e => e.DispatchedCondition)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.FromWarehouseId).HasColumnName("FromWarehouseID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.SerialItemId).HasColumnName("SerialItemID");
            entity.Property(e => e.SerialNumber).HasMaxLength(100);
            entity.Property(e => e.ToWarehouseId).HasColumnName("ToWarehouseID");
            entity.Property(e => e.TransferLineId).HasColumnName("TransferLineID");
            entity.Property(e => e.TransferOrderId).HasColumnName("TransferOrderID");
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.ToTable("Warehouse", tb => tb.HasTrigger("TR_Warehouse_Audit"));

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

            entity.ToTable("ZoneStorageRule", tb =>
                {
                    tb.HasTrigger("TR_ZoneStorageRule_Audit");
                    tb.HasTrigger("TR_ZoneStorageRule_Zone");
                });

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

        // DB49 tables with triggers cannot use EF's direct OUTPUT clause.
        // Keep this configuration when regenerating the shared DbContext.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            if (entityType.GetTableName() is not null && entityType.GetDeclaredTriggers().Any())
                modelBuilder.Entity(entityType.ClrType).ToTable(t => t.UseSqlOutputClause(false));

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
