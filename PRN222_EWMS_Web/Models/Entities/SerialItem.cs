using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class SerialItem
{
    public long SerialItemId { get; set; }

    public int ProductId { get; set; }

    public string SerialNumber { get; set; } = null!;

    public byte? ManufacturingMonth { get; set; }

    public short? ManufacturingYear { get; set; }

    public string OriginType { get; set; } = null!;

    public long? OriginalInboundId { get; set; }

    public long? OriginAdjustmentId { get; set; }

    public int? CurrentWarehouseId { get; set; }

    public int? CurrentLocationId { get; set; }

    public long? CurrentSupplySourceId { get; set; }

    public string StockState { get; set; } = null!;

    public string ItemCondition { get; set; } = null!;

    public bool IsQuarantined { get; set; }

    public string? QuarantineReason { get; set; }

    public DateTime RegisteredAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual StorageLocation? CurrentLocation { get; set; }

    public virtual SupplySource? CurrentSupplySource { get; set; }

    public virtual Warehouse? CurrentWarehouse { get; set; }

    public virtual InboundItem? InboundItem { get; set; }

    public virtual ICollection<InventoryAdjustmentLine> InventoryAdjustmentLines { get; set; } = new List<InventoryAdjustmentLine>();

    public virtual InventoryAdjustment? OriginAdjustment { get; set; }

    public virtual Inbound? OriginalInbound { get; set; }

    public virtual ICollection<OutboundItem> OutboundItems { get; set; } = new List<OutboundItem>();

    public virtual PickingClaim? PickingClaim { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<SerialMovement> SerialMovements { get; set; } = new List<SerialMovement>();

    public virtual ICollection<StocktakeObservation> StocktakeObservations { get; set; } = new List<StocktakeObservation>();

    public virtual ICollection<TransferDispatchItem> TransferDispatchItems { get; set; } = new List<TransferDispatchItem>();
}
