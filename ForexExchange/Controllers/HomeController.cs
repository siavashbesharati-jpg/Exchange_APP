using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;
using Microsoft.AspNetCore.Authorization;

namespace ForexExchange.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ForexDbContext _context;
    // TODO: Re-enable settlement service with new architecture
    // private readonly ITransactionSettlementService _settlementService;
    private readonly ICurrencyPoolService _poolService;
    private readonly CustomerDebtCreditService _debtCreditService;
    private readonly IShareableLinkService _shareableLinkService;



    public HomeController(ILogger<HomeController> logger, ForexDbContext context, /* ITransactionSettlementService settlementService, */ ICurrencyPoolService poolService, CustomerDebtCreditService debtCreditService, IShareableLinkService shareableLinkService)
    {
        _logger = logger;
        _context = context;
        // _settlementService = settlementService;
        _poolService = poolService;
        _debtCreditService = debtCreditService;
        _shareableLinkService = shareableLinkService;
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

    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<IActionResult> Dashboard()
    {
        // Get currency pools for the widget
        var pools = await _poolService.GetAllPoolsAsync();
        ViewBag.CurrencyPools = pools;

        // Get customer debt/credit summary for admin/staff
        if (User.Identity?.IsAuthenticated == true &&
            (User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Staff")))
        {
            var customerDebtCredits = await _debtCreditService.GetCustomerDebtCreditSummaryAsync();
            ViewBag.CustomerDebtCredits = customerDebtCredits;
        }

        return View();
    }

    [Authorize(Roles = "Admin,Manager,Staff")]
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
        if (User.Identity?.IsAuthenticated == true &&
            (User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Staff")))
        {
            var customerDebtCredits = await _debtCreditService.GetCustomerDebtCreditSummaryAsync();
            return View(customerDebtCredits);
        }
        
        return RedirectToAction("Dashboard");
    }

    // Debug action to check currency display order
    public async Task<IActionResult> DebugCurrencyOrder()
    {
        var pools = await _poolService.GetAllPoolsAsync();
        var currencies = pools.Select(p => new { 
            Code = p.Currency?.Code,
            Name = p.Currency?.PersianName,
            DisplayOrder = p.Currency?.DisplayOrder
        }).ToList();
        
        return Json(currencies);
    }

    // Temporary action to update currency display order
    public async Task<IActionResult> UpdateCurrencyDisplayOrder()
    {
        try
        {
            // Get all currencies
            var currencies = await _context.Currencies
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
            
            // Update display orders
            foreach (var currency in currencies)
            {
                switch (currency.Code)
                {
                    case "IRR":
                        currency.DisplayOrder = 1;
                        break;
                    case "OMR":
                        currency.DisplayOrder = 2;
                        break;
                    case "AED":
                        currency.DisplayOrder = 3;
                        break;
                    case "USD":
                        currency.DisplayOrder = 4;
                        break;
                    case "EUR":
                        currency.DisplayOrder = 5;
                        break;
                    case "TRY":
                        currency.DisplayOrder = 6;
                        break;
                }
            }
            
            await _context.SaveChangesAsync();
            
            return Json(new { success = true, message = "Currency DisplayOrder values updated successfully!" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error: {ex.Message}" });
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Help()
    {
        return View();
    }

    public IActionResult FormatTest()
    {
        return View();
    }

    // GET: Home/ShareableLinks
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<IActionResult> ShareableLinks()
    {
        var links = await _context.ShareableLinks
            .Include(sl => sl.Customer)
            .OrderByDescending(sl => sl.CreatedAt)
            .ToListAsync();
        
        return View(links);
    }

    // POST: Home/GenerateShareableLink
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<IActionResult> GenerateShareableLink(int customerId, ShareableLinkType linkType, int expirationDays = 7)
    {
        try
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
            {
                TempData["ErrorMessage"] = "مشتری یافت نشد.";
                return RedirectToAction("ShareableLinks");
            }

            var currentUser = User.Identity?.Name ?? "Admin";
            var description = linkType switch
            {
                ShareableLinkType.CustomerReport => "لینک اشتراک گزارش مشتری",
                _ => "لینک اشتراک"
            };

            var shareableLink = await _shareableLinkService.GenerateLinkAsync(
                customerId,
                linkType,
                expirationDays,
                description,
                currentUser);

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var fullUrl = shareableLink.GetShareableUrl(baseUrl);

            TempData["SuccessMessage"] = $"لینک اشتراک با موفقیت ایجاد شد. لینک تا {expirationDays} روز آینده معتبر است.";
            TempData["ShareableUrl"] = fullUrl;

            return RedirectToAction("ShareableLinks");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating shareable link for customer {CustomerId}", customerId);
            TempData["ErrorMessage"] = "خطا در ایجاد لینک اشتراک.";
            return RedirectToAction("ShareableLinks");
        }
    }

    // POST: Home/DeactivateShareableLink
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<IActionResult> DeactivateShareableLink(int linkId)
    {
        try
        {
            var success = await _shareableLinkService.DeactivateLinkAsync(linkId, User.Identity?.Name);
            if (success)
            {
                TempData["SuccessMessage"] = "لینک اشتراک با موفقیت غیرفعال شد.";
            }
            else
            {
                TempData["ErrorMessage"] = "لینک یافت نشد.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating shareable link {LinkId}", linkId);
            TempData["ErrorMessage"] = "خطا در غیرفعال کردن لینک.";
        }

        return RedirectToAction("ShareableLinks");
    }

    // GET: Home/GetCustomers (for AJAX)
    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<IActionResult> GetCustomers()
    {
        var customers = await _context.Customers
            .Where(c => c.IsActive && !c.IsSystem)
            .Select(c => new { id = c.Id, fullName = c.FullName })
            .OrderBy(c => c.fullName)
            .ToListAsync();

        return Json(customers);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
