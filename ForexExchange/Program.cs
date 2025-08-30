using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Models;
using ForexExchange.Services;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        options.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
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

// Add HttpContextAccessor for admin activity logging
builder.Services.AddHttpContextAccessor();

// Add HttpClient for OpenRouter API
builder.Services.AddHttpClient();

// Add SignalR
builder.Services.AddSignalR();

// Add Services
builder.Services.AddScoped<IOcrService, OpenRouterOcrService>();
builder.Services.AddScoped<ITransactionSettlementService, TransactionSettlementService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IBankStatementService, BankStatementService>();
builder.Services.AddScoped<ICurrencyPoolService, CurrencyPoolService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDataSeedService, DataSeedService>();
builder.Services.AddScoped<IWebScrapingService, WebScrapingService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IRateCalculationService, RateCalculationService>();
builder.Services.AddScoped<CustomerDebtCreditService>();
builder.Services.AddScoped<AdminActivityService>();
builder.Services.AddScoped<AdminNotificationService>();

var app = builder.Build();

// Create database and run migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ForexDbContext>();
    dbContext.Database.EnsureCreated();

    // Seed initial data
    var dataSeedService = scope.ServiceProvider.GetRequiredService<IDataSeedService>();
    await dataSeedService.SeedDataAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// Add authentication middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// Map SignalR hub
app.MapHub<ForexExchange.Hubs.NotificationHub>("/notificationHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=ExchangeRates}/{action=Index}/{id?}");

app.Run();