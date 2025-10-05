using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class ShareableLink
{
    public int Id { get; set; }

    public string Token { get; set; } = null!;

    public int CustomerId { get; set; }

    public int LinkType { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public int IsActive { get; set; }

    public string? CreatedBy { get; set; }

    public int AccessCount { get; set; }

    public string? LastAccessedAt { get; set; }

    public string? Description { get; set; }

    public virtual Customer Customer { get; set; } = null!;
}
