using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class PushNotificationLog
{
    public int Id { get; set; }

    public int PushSubscriptionId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string Data { get; set; } = null!;

    public int WasSuccessful { get; set; }

    public string ErrorMessage { get; set; } = null!;

    public int? HttpStatusCode { get; set; }

    public DateTime SentAt { get; set; }

    public int? SendDurationMs { get; set; }

    public virtual PushSubscription PushSubscription { get; set; } = null!;
}
