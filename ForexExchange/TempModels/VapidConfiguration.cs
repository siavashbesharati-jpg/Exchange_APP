using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class VapidConfiguration
{
    public int Id { get; set; }

    public string ApplicationId { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string PublicKey { get; set; } = null!;

    public string PrivateKey { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int IsActive { get; set; }

    public int NotificationsSent { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string Notes { get; set; } = null!;
}
