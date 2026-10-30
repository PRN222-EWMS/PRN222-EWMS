using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class Warehouse
{
    public int WarehouseId { get; set; }

    public string WarehouseName { get; set; } = null!;

    public string WarehouseType { get; set; } = null!;

    public string Address { get; set; } = null!;

    public DateOnly? RentalStartDate { get; set; }

    public DateOnly? RentalEndDate { get; set; }

    public string Status { get; set; } = null!;

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<CustomerReturnOrder> CustomerReturnOrders { get; set; } = new List<CustomerReturnOrder>();

    public virtual ICollection<InventoryAlertThreshold> InventoryAlertThresholds { get; set; } = new List<InventoryAlertThreshold>();

    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

    public virtual ICollection<SalesOrder> SalesOrders { get; set; } = new List<SalesOrder>();

    public virtual ICollection<SerialItem> SerialItems { get; set; } = new List<SerialItem>();

    public virtual ICollection<SerialMovement> SerialMovementFromWarehouses { get; set; } = new List<SerialMovement>();

    public virtual ICollection<SerialMovement> SerialMovementToWarehouses { get; set; } = new List<SerialMovement>();

    public virtual ICollection<StaffWarehouseAssignment> StaffWarehouseAssignments { get; set; } = new List<StaffWarehouseAssignment>();

    public virtual ICollection<Stocktake> Stocktakes { get; set; } = new List<Stocktake>();

    public virtual ICollection<StorageLocation> StorageLocations { get; set; } = new List<StorageLocation>();

    public virtual ICollection<SupplySource> SupplySources { get; set; } = new List<SupplySource>();

    public virtual ICollection<TransferOrder> TransferOrderFromWarehouses { get; set; } = new List<TransferOrder>();

    public virtual ICollection<TransferOrder> TransferOrderToWarehouses { get; set; } = new List<TransferOrder>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}

