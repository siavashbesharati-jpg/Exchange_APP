using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class Notification
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public int Type { get; set; }

    public int? RelatedEntityId { get; set; }

    public int Priority { get; set; }

    public string CreatedAt { get; set; } = null!;

    public int IsRead { get; set; }

    public string? ReadAt { get; set; }

    public virtual Customer Customer { get; set; } = null!;
}
