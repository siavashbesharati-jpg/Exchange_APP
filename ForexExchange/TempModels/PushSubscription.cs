using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class PushSubscription
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public string Endpoint { get; set; } = null!;

    public string P256dhKey { get; set; } = null!;

    public string AuthKey { get; set; } = null!;

    public string UserAgent { get; set; } = null!;

    public int IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? LastNotificationSent { get; set; }

    public int SuccessfulNotifications { get; set; }

    public int FailedNotifications { get; set; }

    public string DeviceType { get; set; } = null!;

    public string? UserId1 { get; set; }

    public virtual ICollection<PushNotificationLog> PushNotificationLogs { get; set; } = new List<PushNotificationLog>();

    public virtual AspNetUser User { get; set; } = null!;

    public virtual AspNetUser? UserId1Navigation { get; set; }
}
