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

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<CustomerReturnOrder> CustomerReturnOrders { get; set; } = new List<CustomerReturnOrder>();

    public virtual ICollection<Inbound> InboundCreatedByUsers { get; set; } = new List<Inbound>();

    public virtual ICollection<Inbound> InboundPerformedByUsers { get; set; } = new List<Inbound>();

    public virtual ICollection<InventoryAdjustment> InventoryAdjustmentDecidedByUsers { get; set; } = new List<InventoryAdjustment>();

    public virtual ICollection<InventoryAdjustment> InventoryAdjustmentSubmittedByUsers { get; set; } = new List<InventoryAdjustment>();

    public virtual ICollection<InventoryAlertThreshold> InventoryAlertThresholds { get; set; } = new List<InventoryAlertThreshold>();

    public virtual ICollection<Notice> Notices { get; set; } = new List<Notice>();

    public virtual ICollection<Outbound> OutboundAssignedToUsers { get; set; } = new List<Outbound>();

    public virtual ICollection<Outbound> OutboundCreatedByUsers { get; set; } = new List<Outbound>();

    public virtual ICollection<PickingClaim> PickingClaims { get; set; } = new List<PickingClaim>();

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

    public virtual ICollection<ReturnReceipt> ReturnReceiptCreatedByUsers { get; set; } = new List<ReturnReceipt>();

    public virtual ICollection<ReturnReceipt> ReturnReceiptPerformedByUsers { get; set; } = new List<ReturnReceipt>();

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<SalesOrder> SalesOrders { get; set; } = new List<SalesOrder>();

    public virtual ICollection<SerialMovement> SerialMovements { get; set; } = new List<SerialMovement>();

    public virtual ICollection<StaffWarehouseAssignment> StaffWarehouseAssignmentChangedByUsers { get; set; } = new List<StaffWarehouseAssignment>();

    public virtual StaffWarehouseAssignment? StaffWarehouseAssignmentUser { get; set; }

    public virtual ICollection<StocktakeAssignment> StocktakeAssignments { get; set; } = new List<StocktakeAssignment>();

    public virtual ICollection<StocktakeObservation> StocktakeObservations { get; set; } = new List<StocktakeObservation>();

    public virtual ICollection<StocktakeRound> StocktakeRoundClosedByUsers { get; set; } = new List<StocktakeRound>();

    public virtual ICollection<StocktakeRound> StocktakeRoundStartedByUsers { get; set; } = new List<StocktakeRound>();

    public virtual ICollection<Stocktake> Stocktakes { get; set; } = new List<Stocktake>();

    public virtual ICollection<TransferDispatch> TransferDispatchAssignedToUsers { get; set; } = new List<TransferDispatch>();

    public virtual ICollection<TransferDispatch> TransferDispatchCreatedByUsers { get; set; } = new List<TransferDispatch>();

    public virtual ICollection<TransferLossRequest> TransferLossRequestDecidedByUsers { get; set; } = new List<TransferLossRequest>();

    public virtual ICollection<TransferLossRequest> TransferLossRequestSubmittedByUsers { get; set; } = new List<TransferLossRequest>();

    public virtual ICollection<TransferOrder> TransferOrderAssignedToUsers { get; set; } = new List<TransferOrder>();

    public virtual ICollection<TransferOrder> TransferOrderCreatedByUsers { get; set; } = new List<TransferOrder>();

    public virtual ICollection<TransferReceipt> TransferReceiptCreatedByUsers { get; set; } = new List<TransferReceipt>();

    public virtual ICollection<TransferReceipt> TransferReceiptPerformedByUsers { get; set; } = new List<TransferReceipt>();

    public virtual Warehouse? Warehouse { get; set; }
}

