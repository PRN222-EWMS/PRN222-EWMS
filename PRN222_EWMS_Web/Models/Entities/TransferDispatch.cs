using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class TransferDispatch
{
    public long DispatchId { get; set; }

    public long TransferOrderId { get; set; }

    public int CreatedByUserId { get; set; }

    public int? AssignedToUserId { get; set; }

    public string DispatchCode { get; set; } = null!;

    public DateOnly DispatchDate { get; set; }

    public string Status { get; set; } = null!;

    public bool IsActivePicking { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User? AssignedToUser { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual ICollection<DemandAllocation> DemandAllocationDispatches { get; set; } = new List<DemandAllocation>();

    public virtual ICollection<DemandAllocation> DemandAllocationTransferDispatches { get; set; } = new List<DemandAllocation>();

    public virtual ICollection<PickingClaim> PickingClaims { get; set; } = new List<PickingClaim>();

    public virtual ICollection<SerialMovement> SerialMovements { get; set; } = new List<SerialMovement>();

    public virtual ICollection<TransferDispatchItem> TransferDispatchItemDispatches { get; set; } = new List<TransferDispatchItem>();

    public virtual ICollection<TransferDispatchItem> TransferDispatchItemTransferDispatches { get; set; } = new List<TransferDispatchItem>();

    public virtual TransferOrder TransferOrder { get; set; } = null!;
}
