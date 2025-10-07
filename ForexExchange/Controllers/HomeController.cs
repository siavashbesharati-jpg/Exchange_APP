using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace ForexExchange.Controllers;

[Authorize(Roles = "Admin,Operator,Programmer")]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ForexDbContext _context;
    // TODO: Re-enable settlement service with new architecture
    // private readonly ITransactionSettlementService _settlementService;
    private readonly ICurrencyPoolService _poolService;
    private readonly CustomerDebtCreditService _debtCreditService;
    private readonly ISettingsService _settingsService;



    public HomeController(ILogger<HomeController> logger, ForexDbContext context, ICurrencyPoolService poolService, CustomerDebtCreditService debtCreditService, ISettingsService settingsService)
    {
        _logger = logger;
        _context = context;
        _poolService = poolService;
        _debtCreditService = debtCreditService;
        _settingsService = settingsService;
    }

    public async Task<IActionResult> Index()
    {
        // Get current exchange rates (public information)
        var exchangeRates = await _context.ExchangeRates
            .Include(r => r.FromCurrency)
            .Include(r => r.ToCurrency)
            .Where(r => r.IsActive)
            .OrderBy(r => r.FromCurrency.Code)
            .ThenBy(r => r.ToCurrency.Code)
            .ToListAsync();

        // Get open and partially filled orders (public information for transparency)
        var availableOrders = await _context.Orders
            .Include(o => o.Customer)
            .OrderByDescending(o => o.CreatedAt)
            .Take(20)
            .ToListAsync();

        // Basic statistics (public)
        var totalActiveOrders = await _context.Orders
            .CountAsync();
        var today = DateTime.Now.Date;
        // TODO: Replace with AccountingDocument-based stats
        var completedTransactionsToday = 0; // await _context.Transactions
                                            // .CountAsync(t => t.Status == TransactionStatus.Completed && t.CreatedAt.Date == today);

        ViewBag.ExchangeRates = exchangeRates;
        ViewBag.AvailableOrders = availableOrders;
        ViewBag.TotalActiveOrders = totalActiveOrders;
        ViewBag.CompletedTransactionsToday = completedTransactionsToday;

        return View();
    }

    public async Task<IActionResult> Dashboard()
    {
        // Get currency pools for the widget
        var pools = await _poolService.GetAllPoolsAsync();
        ViewBag.CurrencyPools = pools;


        var customerDebtCredits = await _debtCreditService.GetCustomerDebtCreditSummaryAsync();
        ViewBag.CustomerDebtCredits = customerDebtCredits;


        return View();
    }

    public IActionResult Management()
    {
        return View();
    }

    public async Task<IActionResult> PoolWidget()
    {
        var pools = await _poolService.GetAllPoolsAsync();
        return PartialView("_PoolWidget", pools);
    }

    public async Task<IActionResult> DebtCreditWidget()
    {
        var customerDebtCredits = await _debtCreditService.GetCustomerDebtCreditSummaryAsync();
        return PartialView("_DebtCreditWidget", customerDebtCredits);
    }

    public async Task<IActionResult> AllCustomerDebtCredits()
    {
        // Get all customer debt/credit summaries for the dedicated page

        var customerDebtCredits = await _debtCreditService.GetCustomerDebtCreditSummaryAsync();
        return View(customerDebtCredits);

        return RedirectToAction("Dashboard");
    }

 


    [AllowAnonymous]
    public IActionResult Help()
    {
        return View();
    }

   


    [AllowAnonymous]
    [Route("site.webmanifest")]
    [ResponseCache(Duration = 3600)] // Cache for 1 hour
    public async Task<IActionResult> WebManifest()
    {
        try
        {
            var websiteName = await _settingsService.GetWebsiteNameAsync();
            var companyName = await _settingsService.GetCompanyNameAsync();
            var companyWebsite = await _settingsService.GetCompanyWebsiteAsync();
            var logoDataUrl = await _settingsService.GetLogoDataUrlAsync();

            // Create short name from website name (first word or up to 12 characters)
            var shortName = websiteName.Split(' ').FirstOrDefault() ?? companyName;
            if (shortName.Length > 12)
            {
                shortName = shortName.Substring(0, 12);
            }

            // Determine icon sources - use base64 logo if available, fallback to favicon
            string icon192 = "/favicon/android-chrome-192x192.png";
            string icon512 = "/favicon/android-chrome-512x512.png";

            // Use base64 logo if available
            if (!logoDataUrl.StartsWith("/favicon/"))
            {
                // We have a base64 logo, use it for both sizes
                icon192 = logoDataUrl;
                icon512 = logoDataUrl;
            }

            // Build start URL with company website if available
            var startUrl = "/";
            var scope = "/";

            // Add website URL to description if available
            var description = $"{websiteName} - خرید و فروش ارز با بهترین نرخ‌ها";
            if (!string.IsNullOrEmpty(companyWebsite))
            {
                description += $" | {companyWebsite}";
            }

            var manifest = new
            {
                name = websiteName,
                short_name = shortName,
                description = description,
                icons = new[]
                {
                    new
                    {
                        src = icon192,
                        sizes = "192x192",
                        type = "image/png",
                        purpose = "any maskable"
                    },
                    new
                    {
                        src = icon512,
                        sizes = "512x512",
                        type = "image/png",
                        purpose = "any maskable"
                    }
                },
                theme_color = "#ffffff",
                background_color = "#ffffff",
                display = "standalone",
                start_url = startUrl,
                scope = scope,
                categories = new[] { "finance", "business", "exchange" },
                lang = "fa",
                dir = "rtl"
            };

            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            return Content(json, "application/manifest+json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating web manifest");

            // Return a fallback manifest
            var fallbackManifest = new
            {
                name = "سامانه معاملات",
                short_name = "معاملات",
                description = "سامانه معاملات - خرید و فروش ارز با بهترین نرخ‌ها",
                icons = new[]
                {
                    new
                    {
                        src = "/favicon/android-chrome-192x192.png",
                        sizes = "192x192",
                        type = "image/png",
                        purpose = "any maskable"
                    },
                    new
                    {
                        src = "/favicon/android-chrome-512x512.png",
                        sizes = "512x512",
                        type = "image/png",
                        purpose = "any maskable"
                    }
                },
                theme_color = "#ffffff",
                background_color = "#ffffff",
                display = "standalone",
                start_url = "/",
                scope = "/",
                categories = new[] { "finance", "business", "exchange" },
                lang = "fa",
                dir = "rtl"
            };

            var fallbackJson = JsonSerializer.Serialize(fallbackManifest, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            return Content(fallbackJson, "application/manifest+json");
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
