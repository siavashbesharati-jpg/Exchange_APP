using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ForexDbContext _context;

    public HomeController(ILogger<HomeController> logger, ForexDbContext context)
    {
        _logger = logger;
        _context = context;
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

        // Get statistics
        var totalOrders = await _context.Orders.CountAsync();
        var completedTransactions = await _context.Transactions
            .CountAsync(t => t.Status == TransactionStatus.Completed);
        var pendingOrders = await _context.Orders
            .CountAsync(o => o.Status == OrderStatus.Open);

        ViewBag.ExchangeRates = exchangeRates;
        ViewBag.RecentOrders = recentOrders;
        ViewBag.TotalOrders = totalOrders;
        ViewBag.CompletedTransactions = completedTransactions;
        ViewBag.PendingOrders = pendingOrders;

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
