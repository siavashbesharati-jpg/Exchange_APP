using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;

namespace ForexExchange.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ForexDbContext _context;
    private readonly ITransactionSettlementService _settlementService;

    public HomeController(ILogger<HomeController> logger, ForexDbContext context, ITransactionSettlementService settlementService)
    {
        _logger = logger;
        _context = context;
        _settlementService = settlementService;
    }

    public async Task<IActionResult> Index()
    {
        // Get current exchange rates (public information)
        var exchangeRates = await _context.ExchangeRates
            .Where(r => r.IsActive)
            .OrderBy(r => r.Currency)
            .ToListAsync();

        // Get open and partially filled orders (public information for transparency)
        var availableOrders = await _context.Orders
            .Include(o => o.Customer)
            .Where(o => o.Status == OrderStatus.Open || o.Status == OrderStatus.PartiallyFilled)
            .OrderByDescending(o => o.CreatedAt)
            .Take(20)
            .ToListAsync();

        // Basic statistics (public)
        var totalActiveOrders = await _context.Orders
            .CountAsync(o => o.Status == OrderStatus.Open || o.Status == OrderStatus.PartiallyFilled);
        var today = DateTime.Now.Date;
        var completedTransactionsToday = await _context.Transactions
            .CountAsync(t => t.Status == TransactionStatus.Completed && t.CreatedAt.Date == today);

        ViewBag.ExchangeRates = exchangeRates;
        ViewBag.AvailableOrders = availableOrders;
        ViewBag.TotalActiveOrders = totalActiveOrders;
        ViewBag.CompletedTransactionsToday = completedTransactionsToday;

        return View();
    }

    public IActionResult Dashboard()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
