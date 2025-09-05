using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    public class CustomerDebtCreditService
    {
        private readonly ForexDbContext _context;

        public CustomerDebtCreditService(ForexDbContext context)
        {
            _context = context;
        }

    public async Task<List<CustomerDebtCredit>> GetCustomerDebtCreditSummaryAsync()
        {
            // Get all orders that have associated transactions (completed orders)
            var ordersWithTransactions = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                // .Include(o => o.Transactions)
                .ToListAsync();

            // Group by customer
            var customerGroups = ordersWithTransactions.GroupBy(o => o.CustomerId);

            var result = new List<CustomerDebtCredit>();

            foreach (var customerGroup in customerGroups)
            {
                var customer = customerGroup.First().Customer;
                var orders = customerGroup.ToList();

                // Start with initial balances per currency (can be +/-)
                var currencyBalances = await SeedInitialBalancesAsync(customer.Id);

                // Apply order-based deltas
                ApplyOrderDeltas(orders, currencyBalances);

                // Calculate net balance (using IRR as primary currency if available, otherwise first currency)
                var primaryCurrency = currencyBalances.FirstOrDefault(c => c.CurrencyCode == "IRR")?.CurrencyCode
                                    ?? currencyBalances.FirstOrDefault()?.CurrencyCode
                                    ?? "IRR";

                var netBalance = currencyBalances.FirstOrDefault(c => c.CurrencyCode == primaryCurrency)?.Balance ?? 0;

                var customerDebtCredit = new CustomerDebtCredit
                {
                    CustomerId = customer.Id,
                    CustomerName = customer.FullName,
                    CustomerPhone = customer.PhoneNumber ?? "",
                    NetBalance = netBalance,
                    PrimaryCurrency = primaryCurrency,
                    ActiveOrderCount = orders.Count,
                    CurrencyBalances = currencyBalances.OrderByDescending(c => Math.Abs(c.Balance)).ToList()
                };

                result.Add(customerDebtCredit);
            }

            // Sort by absolute net balance (highest first)
            return result.OrderByDescending(c => Math.Abs(c.NetBalance)).ToList();
        }

        private void ApplyOrderDeltas(List<Order> orders, List<CurrencyBalance> currencyBalances)
        {
            var dict = currencyBalances.ToDictionary(c => c.CurrencyCode, c => c);

            foreach (var order in orders)
            {
                var fromCurrency = order.FromCurrency.Code;
                var toCurrency = order.ToCurrency.Code;

                // Initialize entries if missing
                if (!dict.TryGetValue(fromCurrency, out var fromEntry))
                {
                    fromEntry = new CurrencyBalance
                    {
                        CurrencyCode = fromCurrency,
                        CurrencyName = order.FromCurrency.PersianName,
                        Balance = 0,
                        DebtAmount = 0,
                        CreditAmount = 0
                    };
                    dict[fromCurrency] = fromEntry;
                }

                if (!dict.TryGetValue(toCurrency, out var toEntry))
                {
                    toEntry = new CurrencyBalance
                    {
                        CurrencyCode = toCurrency,
                        CurrencyName = order.ToCurrency.PersianName,
                        Balance = 0,
                        DebtAmount = 0,
                        CreditAmount = 0
                    };
                    dict[toCurrency] = toEntry;
                }

                // Calculate amounts based on transactions
                decimal fromAmount, toAmount;

                // For orders with completed transactions, use actual transaction amounts
                // TODO: Replace with AccountingDocument-based tracking for new architecture
                /*
                var completedTransactions = order.Transactions
                    .Where(t => t.Status == TransactionStatus.Completed)
                    .ToList();

                if (completedTransactions.Any())
                {
                    // Use the sum of all completed transaction amounts
                    fromAmount = completedTransactions.Sum(t => t.Amount);
                    toAmount = completedTransactions.Sum(t => t.Amount * order.Rate);
                }
                else
                {
                    continue; // Skip orders without completed transactions
                }
                */
                // For now, use order amounts directly
                fromAmount = order.Amount;
                toAmount = order.TotalAmount;

                // Update balances
                // Customer owes the FromCurrency (debt)
                fromEntry.DebtAmount += fromAmount;
                fromEntry.Balance -= fromAmount;

                // Customer receives the ToCurrency (credit)
                toEntry.CreditAmount += toAmount;
                toEntry.Balance += toAmount;
            }
            // sync dict back to list (preserve order loosely)
            currencyBalances.Clear();
            currencyBalances.AddRange(dict.Values);
        }

        public async Task<CustomerDebtCredit?> GetCustomerDebtCreditAsync(int customerId)
        {
            // Load customer explicitly to support cases with only initial balances
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
            if (customer == null) return null;

            var orders = await _context.Orders
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                // .Include(o => o.Transactions)
                .Where(o => o.CustomerId == customerId)
                .ToListAsync();

            var currencyBalances = await SeedInitialBalancesAsync(customer.Id);

            // Apply order-based deltas if any
            if (orders.Any())
            {
                ApplyOrderDeltas(orders, currencyBalances);
            }

            // If no orders and no initial balances, return null (no financial data)
            if (!orders.Any() && !currencyBalances.Any())
                return null;

            var primaryCurrency = currencyBalances.FirstOrDefault(c => c.CurrencyCode == "IRR")?.CurrencyCode
                                ?? currencyBalances.FirstOrDefault()?.CurrencyCode
                                ?? "IRR";

            var netBalance = currencyBalances.FirstOrDefault(c => c.CurrencyCode == primaryCurrency)?.Balance ?? 0;

            return new CustomerDebtCredit
            {
                CustomerId = customer.Id,
                CustomerName = customer.FullName,
                CustomerPhone = customer.PhoneNumber ?? "",
                NetBalance = netBalance,
                PrimaryCurrency = primaryCurrency,
                ActiveOrderCount = orders.Count,
                CurrencyBalances = currencyBalances.OrderByDescending(c => Math.Abs(c.Balance)).ToList()
            };
        }

        private async Task<List<CurrencyBalance>> SeedInitialBalancesAsync(int customerId)
        {
            var list = new List<CurrencyBalance>();

            // TODO: Replace with CustomerBalance queries
            var balances = await _context.CustomerBalances
                .Where(b => b.CustomerId == customerId)
                .ToListAsync();

            if (!balances.Any()) return list;

            // Get currency names
            var currencyNames = await _context.Currencies
                .ToDictionaryAsync(c => c.Code, c => c.PersianName);

            foreach (var b in balances)
            {
                currencyNames.TryGetValue(b.CurrencyCode, out var persianName);
                
                list.Add(new CurrencyBalance
                {
                    CurrencyCode = b.CurrencyCode,
                    CurrencyName = persianName ?? b.CurrencyCode,
                    Balance = b.Balance,
                    DebtAmount = b.Balance < 0 ? Math.Abs(b.Balance) : 0,
                    CreditAmount = b.Balance > 0 ? b.Balance : 0
                });
            }

            return list;
        }
    }
}
