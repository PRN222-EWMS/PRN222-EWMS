using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class Outbound
{
    public long OutboundId { get; set; }

    public long SalesOrderId { get; set; }

    public int CreatedByUserId { get; set; }

    public int? AssignedToUserId { get; set; }

    public string IssueCode { get; set; } = null!;

    public DateOnly IssueDate { get; set; }

    public string Status { get; set; } = null!;

    public bool IsActivePicking { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? RecipientName { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public string? CustomerNameSnapshot { get; set; }

    public string? CustomerAddressSnapshot { get; set; }

    public string? CustomerPhoneSnapshot { get; set; }

    public virtual User? AssignedToUser { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual ICollection<DemandAllocation> DemandAllocationOutboundNavigations { get; set; } = new List<DemandAllocation>();

    public virtual ICollection<DemandAllocation> DemandAllocationOutbounds { get; set; } = new List<DemandAllocation>();

    public virtual ICollection<OutboundItem> OutboundItemOutboundNavigations { get; set; } = new List<OutboundItem>();

    public virtual ICollection<OutboundItem> OutboundItemOutbounds { get; set; } = new List<OutboundItem>();

    public virtual ICollection<PickingClaim> PickingClaims { get; set; } = new List<PickingClaim>();

    public virtual ICollection<ReturnReceipt> ReturnReceiptOutboundNavigations { get; set; } = new List<ReturnReceipt>();

    public virtual ICollection<ReturnReceipt> ReturnReceiptOutbounds { get; set; } = new List<ReturnReceipt>();

    public virtual SalesOrder SalesOrder { get; set; } = null!;

    public virtual ICollection<SerialMovement> SerialMovements { get; set; } = new List<SerialMovement>();
}
