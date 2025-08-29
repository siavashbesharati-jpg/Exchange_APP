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
            // Get all active orders (Open or PartiallyFilled)
            var activeOrders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .Where(o => o.Status == OrderStatus.Open || o.Status == OrderStatus.PartiallyFilled)
                .ToListAsync();

            // Group by customer
            var customerGroups = activeOrders.GroupBy(o => o.CustomerId);

            var result = new List<CustomerDebtCredit>();

            foreach (var customerGroup in customerGroups)
            {
                var customer = customerGroup.First().Customer;
                var orders = customerGroup.ToList();

                // Calculate currency balances
                var currencyBalances = CalculateCurrencyBalances(orders);

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

        private List<CurrencyBalance> CalculateCurrencyBalances(List<Order> orders)
        {
            var currencyBalances = new Dictionary<string, CurrencyBalance>();

            foreach (var order in orders)
            {
                var fromCurrency = order.FromCurrency.Code;
                var toCurrency = order.ToCurrency.Code;

                // Initialize currency balances if not exists
                if (!currencyBalances.ContainsKey(fromCurrency))
                {
                    currencyBalances[fromCurrency] = new CurrencyBalance
                    {
                        CurrencyCode = fromCurrency,
                        CurrencyName = order.FromCurrency.PersianName,
                        Balance = 0,
                        DebtAmount = 0,
                        CreditAmount = 0
                    };
                }

                if (!currencyBalances.ContainsKey(toCurrency))
                {
                    currencyBalances[toCurrency] = new CurrencyBalance
                    {
                        CurrencyCode = toCurrency,
                        CurrencyName = order.ToCurrency.PersianName,
                        Balance = 0,
                        DebtAmount = 0,
                        CreditAmount = 0
                    };
                }

                // Calculate amounts based on order status
                decimal fromAmount, toAmount;

                if (order.Status == OrderStatus.Open)
                {
                    // Use full amount for open orders
                    fromAmount = order.Amount;
                    toAmount = order.Amount * order.Rate;
                }
                else if (order.Status == OrderStatus.PartiallyFilled)
                {
                    // Use filled amount for partially filled orders
                    fromAmount = order.FilledAmount;
                    toAmount = order.FilledAmount * order.Rate;
                }
                else
                {
                    continue; // Skip other statuses
                }

                // Update balances
                // Customer owes the FromCurrency (debt)
                currencyBalances[fromCurrency].DebtAmount += fromAmount;
                currencyBalances[fromCurrency].Balance -= fromAmount;

                // Customer receives the ToCurrency (credit)
                currencyBalances[toCurrency].CreditAmount += toAmount;
                currencyBalances[toCurrency].Balance += toAmount;
            }

            return currencyBalances.Values.ToList();
        }

        public async Task<CustomerDebtCredit?> GetCustomerDebtCreditAsync(int customerId)
        {
            var orders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .Where(o => o.CustomerId == customerId &&
                           (o.Status == OrderStatus.Open || o.Status == OrderStatus.PartiallyFilled))
                .ToListAsync();

            if (!orders.Any())
                return null;

            var customer = orders.First().Customer;
            var currencyBalances = CalculateCurrencyBalances(orders);

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
    }
}
