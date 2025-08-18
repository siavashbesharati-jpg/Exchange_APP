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
        // Get current exchange rates
        var exchangeRates = await _context.ExchangeRates
            .Where(r => r.IsActive)
            .ToListAsync();

        // Get recent orders
        var recentOrders = await _context.Orders
            .Include(o => o.Customer)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .ToListAsync();

        // Get pending settlements
        var pendingSettlements = await _settlementService.GetPendingSettlementsAsync();

        // Get statistics
        var totalOrders = await _context.Orders.CountAsync();
        var completedTransactions = await _context.Transactions
            .CountAsync(t => t.Status == TransactionStatus.Completed);
        var pendingOrders = await _context.Orders
            .CountAsync(o => o.Status == OrderStatus.Open);
        var activeCustomers = await _context.Customers
            .CountAsync(c => c.IsActive);

        ViewBag.ExchangeRates = exchangeRates;
        ViewBag.RecentOrders = recentOrders;
        ViewBag.PendingSettlements = pendingSettlements;
        ViewBag.TotalOrders = totalOrders;
        ViewBag.CompletedTransactions = completedTransactions;
        ViewBag.PendingOrders = pendingOrders;
        ViewBag.ActiveCustomers = activeCustomers;

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
