using System;
using System.Collections.Generic;

namespace ForexExchange.Models
{
    public class CustomerTransactionsStatementViewModel
    {
        public Customer Customer { get; set; } = null!;
        public List<Order> Orders { get; set; }
        public List<CurrencyPairStatistic> CurrencyPairStats { get; set; }
        public List<MonthlyStatistic> MonthlyStats { get; set; }
        public DateTime StatementDate { get; set; }
        
        public CustomerTransactionsStatementViewModel()
        {
            Orders = new List<Order>();
            CurrencyPairStats = new List<CurrencyPairStatistic>();
            MonthlyStats = new List<MonthlyStatistic>();
        }
    }

    public class CurrencyPairStatistic
    {
        public string FromCurrency { get; set; } = string.Empty;
        public string ToCurrency { get; set; } = string.Empty;
        public int TotalTransactions { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageRate { get; set; }
        public decimal MinRate { get; set; }
        public decimal MaxRate { get; set; }
        public decimal TotalValueInTargetCurrency { get; set; }
    }

    public class MonthlyStatistic
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int TransactionCount { get; set; }
        public decimal TotalVolume { get; set; }
        
        public string MonthName
        {
            get
            {
                var persianMonths = new[]
                {
                    "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
                    "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
                };
                
                // Simple conversion - in real app you'd use proper Persian calendar
                return Month <= 12 ? persianMonths[Month - 1] : Month.ToString();
            }
        }
    }
}
