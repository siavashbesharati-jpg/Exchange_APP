using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    public class CustomerBalanceService : ICustomerBalanceService
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<CustomerBalanceService> _logger;

        public CustomerBalanceService(ForexDbContext context, ILogger<CustomerBalanceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CustomerBalance> GetCustomerBalanceAsync(int customerId, string currencyCode)
        {
            var balance = await _context.CustomerBalances
                .Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.CustomerId == customerId && b.CurrencyCode == currencyCode);

            if (balance == null)
            {
                // Create new balance with zero amount
                balance = new CustomerBalance
                {
                    CustomerId = customerId,
                    CurrencyCode = currencyCode,
                    Balance = 0,
                    LastUpdated = DateTime.Now
                };
                _context.CustomerBalances.Add(balance);
                await _context.SaveChangesAsync();
                
                // Re-query to get navigation properties
                balance = await _context.CustomerBalances
                    .Include(b => b.Customer)
                    .FirstAsync(b => b.Id == balance.Id);
            }

            return balance;
        }

        public async Task<List<CustomerBalance>> GetCustomerBalancesAsync(int customerId)
        {
            return await _context.CustomerBalances
                .Include(b => b.Customer)
                .Where(b => b.CustomerId == customerId)
                .OrderBy(b => b.CurrencyCode)
                .ToListAsync();
        }

        public async Task<CustomerBalanceSummary> GetCustomerBalanceSummaryAsync(int customerId)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
                throw new ArgumentException($"Customer with ID {customerId} not found");

            var balances = await GetCustomerBalancesAsync(customerId);

            // Calculate net balance in primary currency (IRR)
            var primaryBalance = balances.FirstOrDefault(b => b.CurrencyCode == "IRR")?.Balance ?? 0;

            return new CustomerBalanceSummary
            {
                CustomerId = customer.Id,
                CustomerName = customer.FullName,
                CustomerPhone = customer.PhoneNumber ?? "",
                CurrencyBalances = balances.Where(b => b.Balance != 0).ToList(),
                NetBalanceInPrimaryCurrency = primaryBalance,
                PrimaryCurrency = "IRR"
            };
        }

        public async Task<List<CustomerBalanceSummary>> GetAllCustomerBalanceSummariesAsync()
        {
            var customersWithBalances = await _context.CustomerBalances
                .Include(b => b.Customer)
                .Where(b => b.Balance != 0)
                .GroupBy(b => b.CustomerId)
                .Select(g => g.Key)
                .ToListAsync();

            var summaries = new List<CustomerBalanceSummary>();
            foreach (var customerId in customersWithBalances)
            {
                var summary = await GetCustomerBalanceSummaryAsync(customerId);
                summaries.Add(summary);
            }

            return summaries.OrderByDescending(s => Math.Abs(s.NetBalanceInPrimaryCurrency)).ToList();
        }

        public async Task UpdateCustomerBalanceAsync(int customerId, string currencyCode, decimal amount, string reason)
        {
            var balance = await GetCustomerBalanceAsync(customerId, currencyCode);
            
            balance.Balance += amount;
            balance.LastUpdated = DateTime.Now;
            balance.Notes = $"{reason} - {DateTime.Now:yyyy-MM-dd HH:mm}";

            _context.CustomerBalances.Update(balance);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated customer {CustomerId} balance in {Currency}: {Amount} ({Reason})", 
                customerId, currencyCode, amount, reason);
        }

        public async Task ProcessOrderCreationAsync(Order order)
        {
            // Customer pays FromCurrency amount (negative balance)
            await UpdateCustomerBalanceAsync(
                order.CustomerId, 
                order.FromCurrency.Code, 
                -order.Amount, 
                $"Order #{order.Id} - Pay {order.Amount} {order.FromCurrency.Code}"
            );

            // Customer receives ToCurrency amount (positive balance)
            await UpdateCustomerBalanceAsync(
                order.CustomerId, 
                order.ToCurrency.Code, 
                order.TotalAmount, 
                $"Order #{order.Id} - Receive {order.TotalAmount} {order.ToCurrency.Code}"
            );

            _logger.LogInformation("Processed order creation {OrderId} for customer {CustomerId}", 
                order.Id, order.CustomerId);
        }

        public async Task ProcessOrderEditAsync(Order oldOrder, Order newOrder)
        {
            // Reverse old order effects
            await UpdateCustomerBalanceAsync(
                oldOrder.CustomerId, 
                oldOrder.FromCurrency.Code, 
                oldOrder.Amount, 
                $"Reverse Order #{oldOrder.Id} edit"
            );

            await UpdateCustomerBalanceAsync(
                oldOrder.CustomerId, 
                oldOrder.ToCurrency.Code, 
                -oldOrder.TotalAmount, 
                $"Reverse Order #{oldOrder.Id} edit"
            );

            // Apply new order effects
            await ProcessOrderCreationAsync(newOrder);

            _logger.LogInformation("Processed order edit {OrderId} for customer {CustomerId}", 
                newOrder.Id, newOrder.CustomerId);
        }

        public async Task ProcessAccountingDocumentAsync(AccountingDocument document)
        {
            if (document.PayerType == PayerType.Customer)
            {
                // Customer pays - positive balance (reduces debt or increases credit)
                await UpdateCustomerBalanceAsync(
                    document.CustomerId,
                    document.CurrencyCode,
                    document.Amount,
                    $"Document #{document.Id} - {document.Title}"
                );
            }
            else if (document.PayerType == PayerType.System)
            {
                // System pays - negative balance for customer (increases debt or reduces credit)
                await UpdateCustomerBalanceAsync(
                    document.CustomerId,
                    document.CurrencyCode,
                    -document.Amount,
                    $"Document #{document.Id} - {document.Title}"
                );
            }

            _logger.LogInformation("Processed accounting document {DocumentId}", document.Id);
        }

        public async Task SetInitialBalanceAsync(int customerId, string currencyCode, decimal amount, string notes)
        {
            var balance = await GetCustomerBalanceAsync(customerId, currencyCode);
            
            balance.Balance = amount;
            balance.LastUpdated = DateTime.Now;
            balance.Notes = $"Initial balance set: {notes}";

            _context.CustomerBalances.Update(balance);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Set initial balance for customer {CustomerId} in {Currency}: {Amount}", 
                customerId, currencyCode, amount);
        }
    }
}
