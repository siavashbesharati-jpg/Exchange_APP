# Central Notification System Documentation
## سیستم مرکزی اعلان‌ها

### Overview | بررسی کلی

The central notification system provides a unified way to send notifications through multiple channels (SignalR, Push, SMS, Email, Telegram) with a single API call. This makes it easy to add new notification providers in the future without changing existing code.

سیستم مرکزی اعلان‌ها روشی یکپارچه برای ارسال اعلان از طریق کانال‌های مختلف (SignalR، Push، SMS، Email، Telegram) با یک فراخوانی API ارائه می‌دهد.

### Architecture | معماری

```
Controllers → NotificationHub → Providers (SignalR, Push, SMS, Email, Telegram)
```

### Components | اجزا

#### 1. **INotificationProvider** Interface
Base interface that all notification providers must implement.

#### 2. **NotificationHub** 
Central hub that coordinates all notification providers.

#### 3. **NotificationContext**
Contains all notification data including title, message, target users, event type, and related entity information.

#### 4. **Providers**
- **SignalRNotificationProvider**: Real-time browser notifications
- **PushNotificationProvider**: Web push notifications  
- **SmsNotificationProvider**: SMS messages (template ready)
- **EmailNotificationProvider**: Email notifications (template ready)
- **TelegramNotificationProvider**: Telegram bot messages (template ready)

### Usage | استفاده

Instead of calling multiple notification services:

```csharp
// OLD WAY - Multiple calls
await _adminNotificationService.SendOrderNotificationAsync(order, "created");
await _businessEventNotificationService.SendOrderCreatedNotificationAsync(order, userId);
```

Now use the central hub:

```csharp
// NEW WAY - Single call to central hub
await _notificationHub.SendOrderNotificationAsync(order, NotificationEventType.OrderCreated, userId);
```

### Supported Event Types | انواع رویدادهای پشتیبانی شده

- **Orders**: OrderCreated, OrderUpdated, OrderCompleted, OrderCancelled
- **Accounting Documents**: AccountingDocumentCreated, AccountingDocumentVerified, AccountingDocumentRejected
- **Customers**: CustomerRegistered, CustomerBalanceChanged, CustomerStatusChanged
- **System**: SystemError, SystemMaintenance, ExchangeRateUpdated
- **Custom**: Custom events

### Configuration | تنظیمات

Enable/disable providers in `appsettings.json`:

```json
{
  "Notifications": {
    "SignalR": { "Enabled": true },
    "Push": { "Enabled": true },
    "SMS": { "Enabled": false },
    "Email": { "Enabled": false },
    "Telegram": { "Enabled": false }
  }
}
```

### Adding New Providers | افزودن ارائه‌دهندگان جدید

1. Create a new class implementing `INotificationProvider`
2. Register it in `Program.cs`
3. Add to notification hub registration
4. Add configuration in `appsettings.json`

Example:

```csharp
public class WhatsAppNotificationProvider : INotificationProvider
{
    public string ProviderName => "WhatsApp";
    public bool IsEnabled => _configuration.GetValue<bool>("Notifications:WhatsApp:Enabled", false);
    
    // Implement required methods...
}
```

### Benefits | مزایا

1. **Centralized**: Single point for all notifications
2. **Scalable**: Easy to add new providers
3. **Configurable**: Enable/disable providers via config
4. **Consistent**: Same interface for all notification types
5. **Maintainable**: Changes to notification logic only need to be made in one place

### Current Status | وضعیت فعلی

✅ **Active Providers**:
- SignalR (real-time browser notifications)
- Push (web push notifications)

🔄 **Ready Templates**:
- SMS (needs API integration)
- Email (needs SMTP configuration)  
- Telegram (needs bot token)

### Future Extensions | توسعه‌های آینده

When you're ready to implement SMS, Email, or Telegram:

1. **SMS**: Update `SmsNotificationProvider` with your SMS service API
2. **Email**: Configure SMTP settings and implement email templates
3. **Telegram**: Add bot token and implement Telegram API calls
4. **WhatsApp Business API**: Create new provider for WhatsApp
5. **Discord**: Create provider for Discord webhooks

### Testing | تست

You can test the system by:

1. Creating a new order → Should trigger both SignalR and Push notifications
2. Uploading an accounting document → Should trigger notifications
3. Verifying a document → Should trigger notifications

Check the console logs to see which providers are triggered and their success/failure status.
