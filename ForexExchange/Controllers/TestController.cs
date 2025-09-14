using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly ForexDbContext _context;

        public TestController(ForexDbContext context)
        {
            _context = context;
        }

        [HttpGet("balance-status")]
        public async Task<IActionResult> GetBalanceStatus()
        {
            try
            {
                var result = new
                {
                    CustomerBalances = await _context.CustomerBalances
                        .Include(cb => cb.Customer)
                        .Where(cb => cb.CustomerId == 32 || cb.CustomerId == 30)
                        .Select(cb => new
                        {
                            CustomerId = cb.CustomerId,
                            CustomerName = cb.Customer.FullName,
                            CurrencyCode = cb.CurrencyCode,
                            Balance = cb.Balance,
                            LastUpdated = cb.LastUpdated,
                            Notes = cb.Notes
                        })
                        .OrderBy(cb => cb.CustomerId)
                        .ThenBy(cb => cb.CurrencyCode)
                        .ToListAsync(),

                    CurrencyPools = await _context.CurrencyPools
                        .Include(cp => cp.Currency)
                        .Select(cp => new
                        {
                            CurrencyCode = cp.CurrencyCode,
                            CurrencyName = cp.Currency.Name,
                            Balance = cp.Balance,
                            LastUpdated = cp.LastUpdated,
                            Notes = cp.Notes
                        })
                        .OrderBy(cp => cp.CurrencyCode)
                        .ToListAsync(),

                    LatestHistory = await _context.CustomerBalanceHistory
                        .Where(h => (h.CustomerId == 32 || h.CustomerId == 30) && !h.IsDeleted)
                        .OrderByDescending(h => h.CreatedAt)
                        .Take(10)
                        .Select(h => new
                        {
                            h.Id,
                            h.CustomerId,
                            h.CurrencyCode,
                            h.TransactionAmount,
                            h.BalanceAfter,
                            h.CreatedAt,
                            h.Description,
                            h.TransactionType
                        })
                        .ToListAsync(),

                    DeletedHistory = await _context.CustomerBalanceHistory
                        .Where(h => (h.CustomerId == 32 || h.CustomerId == 30) && h.IsDeleted)
                        .OrderByDescending(h => h.DeletedAt)
                        .Take(10)
                        .Select(h => new
                        {
                            h.Id,
                            h.CustomerId,
                            h.CurrencyCode,
                            h.TransactionAmount,
                            h.BalanceAfter,
                            h.CreatedAt,
                            h.DeletedAt,
                            h.DeletedBy,
                            h.Description,
                            h.TransactionType
                        })
                        .ToListAsync()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}