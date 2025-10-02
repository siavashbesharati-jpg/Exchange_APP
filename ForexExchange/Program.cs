using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using ForexExchange.Models;
using ForexExchange.Services;
using ForexExchange.Hubs;
using ForexExchange.Services.Notifications.Providers;
using ForexExchange.Services.Notifications;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        options.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
        options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
    });

// Add Entity Framework
builder.Services.AddDbContext<ForexDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ??
                     "Data Source=ForexExchange.db"));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password requirements
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;

    // User requirements
    options.User.RequireUniqueEmail = false;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+"; // Allow phone numbers

    // Sign in requirements
    options.SignIn.RequireConfirmedEmail = false;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<ForexDbContext>()
.AddDefaultTokenProviders();

// Add RoleManager
builder.Services.AddScoped<RoleManager<IdentityRole>>();

// Add HttpClient for OpenRouter API
builder.Services.AddHttpClient();
// Add HttpContextAccessor for admin activity logging
builder.Services.AddHttpContextAccessor();

// Add SignalR with custom user ID provider
builder.Services.AddSignalR().AddHubOptions<ForexExchange.Hubs.NotificationHub>(options =>
{
    // Configure SignalR to use user ID instead of username for identification
    options.EnableDetailedErrors = true;
});

// Register custom user ID provider for SignalR
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();

// Configure SignalR to use the user ID claim for user identification
builder.Services.Configure<IdentityOptions>(options =>
{
    // This ensures SignalR can properly identify users by their ID
    options.ClaimsIdentity.UserIdClaimType = System.Security.Claims.ClaimTypes.NameIdentifier;
});

// Add Services
builder.Services.AddScoped<IOcrService, OpenRouterOcrService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IBankStatementService, BankStatementService>();
builder.Services.AddScoped<ICurrencyPoolService, CurrencyPoolService>();
builder.Services.AddScoped<IDataSeedService, DataSeedService>();
builder.Services.AddScoped<IWebScrapingService, WebScrapingService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IRateCalculationService, RateCalculationService>();
builder.Services.AddScoped<CustomerDebtCreditService>();
builder.Services.AddScoped<AdminActivityService>();
builder.Services.AddScoped<AdminNotificationService>();
// New balance management services
builder.Services.AddScoped<ICustomerBalanceService, CustomerBalanceService>();
builder.Services.AddScoped<IBankAccountBalanceService, BankAccountBalanceService>();
builder.Services.AddScoped<IShareableLinkService, ShareableLinkService>();
// Customer financial history service
builder.Services.AddScoped<CustomerFinancialHistoryService>();
// Pool financial history service
builder.Services.AddScoped<PoolFinancialHistoryService>();
// Bank account financial history service
builder.Services.AddScoped<BankAccountFinancialHistoryService>();
// Central Financial Service - Event Sourcing with Complete Audit Trail
builder.Services.AddScoped<ICentralFinancialService, CentralFinancialService>();
// Order data preparation service - SRP for shared order validation logic
builder.Services.AddScoped<IOrderDataService, OrderDataService>();
// Push notification services
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<IVapidService, VapidService>();
// Excel export service
builder.Services.AddScoped<ExcelExportService>();

// Central notification system
builder.Services.AddScoped<INotificationHub>(serviceProvider =>
{
    var context = serviceProvider.GetRequiredService<ForexDbContext>();
    var logger = serviceProvider.GetRequiredService<ILogger<ForexExchange.Services.Notifications.NotificationHub>>();
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();
    
    var hub = new ForexExchange.Services.Notifications.NotificationHub(context, logger, configuration, environment);
    
    // Register providers
    var signalRProvider = serviceProvider.GetRequiredService<SignalRNotificationProvider>();
    var pushProvider = serviceProvider.GetRequiredService<PushNotificationProvider>();
    var smsProvider = serviceProvider.GetRequiredService<SmsNotificationProvider>();
    var emailProvider = serviceProvider.GetRequiredService<EmailNotificationProvider>();
    var telegramProvider = serviceProvider.GetRequiredService<TelegramNotificationProvider>();
    
    hub.RegisterProvider(signalRProvider);
    hub.RegisterProvider(pushProvider);
    hub.RegisterProvider(smsProvider);
    hub.RegisterProvider(emailProvider);
    hub.RegisterProvider(telegramProvider);
    
    return hub;
});

// Notification providers - register as individual services, not as INotificationProvider
builder.Services.AddScoped<SignalRNotificationProvider>();
builder.Services.AddScoped<PushNotificationProvider>();
builder.Services.AddScoped<SmsNotificationProvider>();
builder.Services.AddScoped<EmailNotificationProvider>();
builder.Services.AddScoped<TelegramNotificationProvider>();


var app = builder.Build();

// Auto-apply migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var dbContext = services.GetRequiredService<ForexDbContext>();

        // Check if there are pending migrations
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            logger.LogInformation("Found {Count} pending migrations. Applying...", pendingMigrations.Count());
            foreach (var migration in pendingMigrations)
            {
                logger.LogInformation("Pending migration: {Migration}", migration);
            }

            // Apply all pending migrations
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("All migrations applied successfully");
        }
        else
        {
            logger.LogInformation("Database is up to date. No pending migrations found");
        }



        // // Seed initial data
        var dataSeedService = services.GetRequiredService<IDataSeedService>();
        await dataSeedService.SeedDataAsync();

        logger.LogInformation("Application startup completed successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating or seeding the database");
        throw; // Re-throw to prevent app from starting with incomplete database
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // SECURITY WARNING: This shows detailed exception information in production
    // Remove this configuration before deploying to a public production environment
    app.UseDeveloperExceptionPage();

    // Alternative: Use custom error handling with detailed logging
    // app.UseExceptionHandler("/Home/Error");

    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseRouting();

// Add authentication middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=ExchangeRates}/{action=Index}/{id?}");

// Map SignalR hubs
app.MapHub<ForexExchange.Hubs.NotificationHub>("/notificationHub");

app.Run();
