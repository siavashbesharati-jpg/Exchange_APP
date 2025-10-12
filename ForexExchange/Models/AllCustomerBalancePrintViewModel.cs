using System.Collections.Generic;

namespace ForexExchange.Models
{
    public class AllCustomerBalancePrintViewModel
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public List<BalanceItem> Balances { get; set; } = new();

        public class BalanceItem
        {
            public string CurrencyCode { get; set; } = string.Empty;
            public decimal Balance { get; set; }
        }
    }
}
