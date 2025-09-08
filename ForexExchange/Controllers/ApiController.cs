using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Controllers
{
    [Route("api")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly ForexDbContext _context;

        public ApiController(ForexDbContext context)
        {
            _context = context;
        }

        [HttpGet("customers")]
        public async Task<IActionResult> GetCustomers()
        {
            try
            {
                var customers = await _context.Customers
                    .Where(c => c.IsActive)
                    .Select(c => new { id = c.Id, fullName = c.FullName })
                    .OrderBy(c => c.fullName)
                    .ToListAsync();

                return Ok(customers);
            }
            catch (Exception)
            {
                return Ok(new List<object>());
            }
        }

        [HttpGet("currencies")]
        public async Task<IActionResult> GetCurrencies()
        {
            try
            {
                var currencies = await _context.Currencies
                    .Where(c => c.IsActive)
                    .Select(c => new { id = c.Id, name = c.Name, code = c.Code })
                    .OrderBy(c => c.name)
                    .ToListAsync();

                return Ok(currencies);
            }
            catch (Exception)
            {
                return Ok(new List<object>());
            }
        }

        [HttpGet("bankaccounts")]
        public async Task<IActionResult> GetBankAccounts()
        {
            try
            {
                var bankAccounts = await _context.BankAccounts
                    .Where(ba => ba.IsActive)
                    .Select(ba => new { 
                        id = ba.Id, 
                        bankName = ba.BankName, 
                        accountNumber = ba.AccountNumber,
                        currencyCode = ba.CurrencyCode
                    })
                    .OrderBy(ba => ba.bankName)
                    .ToListAsync();

                return Ok(bankAccounts);
            }
            catch (Exception)
            {
                return Ok(new List<object>());
            }
        }
    }
}
