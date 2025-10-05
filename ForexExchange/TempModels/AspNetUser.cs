using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class AspNetUser
{
    public string Id { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? NationalId { get; set; }

    public string? Address { get; set; }

    public DateTime CreatedAt { get; set; }

    public int IsActive { get; set; }

    public int Role { get; set; }

    public int? CustomerId { get; set; }

    public string? UserName { get; set; }

    public string? NormalizedUserName { get; set; }

    public string? Email { get; set; }

    public string? NormalizedEmail { get; set; }

    public int EmailConfirmed { get; set; }

    public string? PasswordHash { get; set; }

    public string? SecurityStamp { get; set; }

    public string? ConcurrencyStamp { get; set; }

    public string? PhoneNumber { get; set; }

    public int PhoneNumberConfirmed { get; set; }

    public int TwoFactorEnabled { get; set; }

    public string? LockoutEnd { get; set; }

    public int LockoutEnabled { get; set; }

    public int AccessFailedCount { get; set; }

    public virtual ICollection<AspNetUserClaim> AspNetUserClaims { get; set; } = new List<AspNetUserClaim>();

    public virtual ICollection<AspNetUserLogin> AspNetUserLogins { get; set; } = new List<AspNetUserLogin>();

    public virtual ICollection<AspNetUserToken> AspNetUserTokens { get; set; } = new List<AspNetUserToken>();

    public virtual Customer? Customer { get; set; }

    public virtual ICollection<PushSubscription> PushSubscriptionUserId1Navigations { get; set; } = new List<PushSubscription>();

    public virtual ICollection<PushSubscription> PushSubscriptionUsers { get; set; } = new List<PushSubscription>();

    public virtual ICollection<AspNetRole> Roles { get; set; } = new List<AspNetRole>();
}
