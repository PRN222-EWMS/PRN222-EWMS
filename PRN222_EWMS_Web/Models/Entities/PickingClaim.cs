using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class PickingClaim
{
    public long SerialItemId { get; set; }

    public long? OutboundId { get; set; }

    public long? DispatchId { get; set; }

    public int ClaimedByUserId { get; set; }

    public DateTime ClaimedAt { get; set; }

    public virtual User ClaimedByUser { get; set; } = null!;

    public virtual TransferDispatch? Dispatch { get; set; }

    public virtual Outbound? Outbound { get; set; }

    public virtual SerialItem SerialItem { get; set; } = null!;
}
