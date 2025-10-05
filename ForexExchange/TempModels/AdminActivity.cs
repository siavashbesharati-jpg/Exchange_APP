using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class AdminActivity
{
    public int Id { get; set; }

    public string AdminUserId { get; set; } = null!;

    public string AdminUsername { get; set; } = null!;

    public int ActivityType { get; set; }

    public string Description { get; set; } = null!;

    public string? Details { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime Timestamp { get; set; }

    public int IsSuccess { get; set; }

    public string? EntityType { get; set; }

    public int? EntityId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }
}
