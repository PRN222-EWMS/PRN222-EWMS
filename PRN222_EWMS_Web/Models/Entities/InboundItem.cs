using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class InboundItem
{
    public long InboundItemId { get; set; }

    public long InboundId { get; set; }

    public long PolineId { get; set; }

    public string ScannedSerial { get; set; } = null!;

    public long? SerialItemId { get; set; }

    public byte? ManufacturingMonth { get; set; }

    public short? ManufacturingYear { get; set; }

    public string? ReceivedCondition { get; set; }

    public string? ConditionNote { get; set; }

    public DateTime ScannedAt { get; set; }

    public bool IsActive { get; set; }

    public virtual Inbound Inbound { get; set; } = null!;

    public virtual PurchaseOrderLine Poline { get; set; } = null!;

    public virtual SerialItem? SerialItem { get; set; }
}
