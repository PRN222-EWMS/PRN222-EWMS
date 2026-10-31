using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class Notice
{
    public long NoticeId { get; set; }

    public int UserId { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string NoticeType { get; set; } = null!;

    public string? RelatedUrl { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public virtual User User { get; set; } = null!;
}
