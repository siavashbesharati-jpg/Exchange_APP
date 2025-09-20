# Notification Center System

## Overview
The Notification Center is a centralized system for managing and sending notifications across multiple channels in the ForexExchange application. It supports real-time notifications (SignalR), push notifications, SMS, email, and Telegram notifications through a provider-based architecture.

## Architecture

### Core Components

#### 1. INotificationProvider Interface
Base interface for all notification providers with methods for different event types:
- `SendOrderNotificationAsync()` - Order-related notifications
- `SendAccountingDocumentNotificationAsync()` - Document notifications  
- `SendCustomerNotificationAsync()` - Customer-related notifications
- `SendSystemNotificationAsync()` - System-wide notifications
- `SendCustomNotificationAsync()` - Custom notifications

#### 2. NotificationHub (Central Coordinator)
Main orchestrator that manages all notification providers and handles:
- Provider registration and management
- Event-specific notification building
- Coordinated sending across multiple channels
- User exclusion logic for preventing self-notifications

#### 3. NotificationContext
Data container that holds all notification information:
- Event type and priority
- Target users and exclusions
- Title, message, and navigation URLs
- Related entity information
- Custom data payload

## Provider Types

### Currently Implemented:
1. **SignalRNotificationProvider** - Real-time browser notifications
2. **PushNotificationProvider** - Web push notifications using VAPID
3. **SmsNotificationProvider** - SMS notifications (template)
4. **EmailNotificationProvider** - Email notifications (template)
5. **TelegramNotificationProvider** - Telegram bot notifications (template)

### Provider States:
- **Active**: SignalR and Push providers are fully implemented
- **Template**: SMS, Email, and Telegram providers are scaffolded for future implementation

## Event Types

### Supported Events:
```csharp
public enum NotificationEventType
{
    // Order events
    OrderCreated,
    OrderUpdated, 
    OrderCompleted,
    OrderCancelled,
    
    // Accounting document events
    AccountingDocumentCreated,
    AccountingDocumentVerified,
    AccountingDocumentRejected,
    
    // Customer events
    CustomerRegistered,
    CustomerBalanceChanged,
    CustomerStatusChanged,
    
    // System events
    SystemError,
    SystemMaintenance,
    ExchangeRateUpdated,
    
    // Custom events
    Custom
}
```

### Priority Levels:
```csharp
public enum NotificationPriority
{
    Low,
    Normal,
    High,
    Critical
}
```

## Usage Examples

### 1. Order Notifications
```csharp
// In OrdersController.cs
await _notificationHub.SendOrderNotificationAsync(
    order, 
    NotificationEventType.OrderCreated, 
    currentUser.Id  // Excludes current user from SignalR popups
);
```

### 2. Manual Transaction Notifications
```csharp
// In DatabaseController.cs
await _notificationHub.SendCustomNotificationAsync(
    title: "تعدیل دستی موجودی ایجاد شد",
    message: $"مشتری: {customerName} | مبلغ: {amount:N2} {currencyCode}",
    eventType: NotificationEventType.CustomerBalanceChanged,
    userId: currentUser?.Id, // Excludes current user from popups
    navigationUrl: $"/Reports/CustomerReports?customerId={customerId}",
    priority: NotificationPriority.Normal
);
```

### 3. System Notifications
```csharp
await _notificationHub.SendCustomNotificationAsync(
    title: "System Maintenance",
    message: "Scheduled maintenance will begin in 30 minutes",
    eventType: NotificationEventType.SystemMaintenance,
    priority: NotificationPriority.High
);
```

## Configuration

### 1. Service Registration (Program.cs)
```csharp
// Individual providers
builder.Services.AddScoped<SignalRNotificationProvider>();
builder.Services.AddScoped<PushNotificationProvider>();
builder.Services.AddScoped<SmsNotificationProvider>();
builder.Services.AddScoped<EmailNotificationProvider>();
builder.Services.AddScoped<TelegramNotificationProvider>();

// Central hub with provider registration
builder.Services.AddScoped<INotificationHub>(serviceProvider =>
{
    var hub = new NotificationHub(context, logger, configuration);
    
    // Register all providers
    hub.RegisterProvider(serviceProvider.GetRequiredService<SignalRNotificationProvider>());
    hub.RegisterProvider(serviceProvider.GetRequiredService<PushNotificationProvider>());
    // ... register other providers
    
    return hub;
});
```

### 2. Configuration Settings
```json
{
  "Notifications": {
    "SignalR": {
      "Enabled": true
    },
    "Push": {
      "Enabled": true
    },
    "SMS": {
      "Enabled": false
    },
    "Email": {
      "Enabled": false  
    },
    "Telegram": {
      "Enabled": false
    }
  }
}
```

## User Exclusion Logic

### Preventing Self-Notifications:
The system prevents users from seeing popup notifications for actions they performed:

1. **Controller gets current user**:
   ```csharp
   var currentUser = await _userManager.GetUserAsync(User);
   ```

2. **Pass user ID to notification system**:
   ```csharp
   userId: currentUser?.Id  // This user will be excluded from SignalR popups
   ```

3. **SignalR provider excludes user**:
   ```csharp
   if (context.ExcludeUserIds.Any()) {
       await _hubContext.Clients.GroupExcept("Admins", context.ExcludeUserIds)
           .SendAsync("ReceiveNotification", notificationData);
   }
   ```

### Behavior:
- ✅ **SignalR popups**: Excluded for the performing user
- ✅ **Push notifications**: Sent to all admins (including performer)
- ✅ **Other channels**: Follow their individual logic

## File Structure

```
Services/Notifications/
├── README.md                           # This documentation
├── INotificationProvider.cs            # Base interface and shared types
├── NotificationHub.cs                  # Central coordination hub
└── Providers/
    ├── SignalRNotificationProvider.cs  # Real-time browser notifications
    ├── PushNotificationProvider.cs     # Web push notifications
    └── FutureNotificationProviders.cs  # SMS, Email, Telegram templates
```

## Integration Points

### Controllers Using Notifications:
1. **OrdersController** - Order lifecycle notifications
2. **DatabaseController** - Manual transaction notifications  
3. **AdminManagementController** - User management notifications
4. **AccountingDocumentsController** - Document workflow notifications

### Dependencies:
- **ASP.NET Core Identity** - User management and authentication
- **SignalR** - Real-time communication
- **Entity Framework** - Database operations
- **WebPush library** - Push notification delivery

## Adding New Notifications

### Step 1: Determine Event Type
- Use existing `NotificationEventType` if applicable
- Add new enum value if needed

### Step 2: Choose Method
- **Specific events**: Use typed methods (`SendOrderNotificationAsync`, etc.)
- **Custom events**: Use `SendCustomNotificationAsync`

### Step 3: Controller Integration
```csharp
public class YourController : Controller
{
    private readonly INotificationHub _notificationHub;
    private readonly UserManager<ApplicationUser> _userManager;
    
    // In your action method:
    var currentUser = await _userManager.GetUserAsync(User);
    
    await _notificationHub.SendCustomNotificationAsync(
        title: "Your Notification Title",
        message: "Your notification message",
        eventType: NotificationEventType.YourEventType,
        userId: currentUser?.Id, // Excludes current user from popups
        navigationUrl: "/your/target/url",
        priority: NotificationPriority.Normal
    );
```

### Step 4: Test Behavior
- Verify the performing user doesn't see SignalR popups
- Confirm other admin users receive real-time notifications
- Check push notifications are sent to all admins

## Troubleshooting

### Common Issues:

1. **User still sees their own notifications**:
   - Verify `UserManager<ApplicationUser>` is injected in controller
   - Ensure `currentUser?.Id` is passed to notification methods
   - Check SignalR user groups are configured correctly

2. **Notifications not appearing**:
   - Verify provider is enabled in configuration
   - Check provider registration in Program.cs
   - Ensure SignalR connection is established

3. **Push notifications failing**:
   - Verify VAPID keys are configured
   - Check user has active push subscriptions
   - Review push notification logs

### Debug Information:
- Notification Hub logs provider registration and sending attempts
- SignalR provider logs successful deliveries and user exclusions
- Push provider logs subscription management and delivery status

## Related Models

### Database Models:
- **Notification** (`Models/Notification.cs`) - Persistent notification storage
- **PushSubscription** - User push notification subscriptions
- **PushNotificationLog** - Delivery tracking and analytics

### DTOs and Context:
- **NotificationContext** - Runtime notification data
- **RelatedEntity** - Links notifications to business entities

---

*Last Updated: September 21, 2025*
*Version: 1.0*