using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    public class CustomerDebtCredit
    {
        public int CustomerId { get; set; }

        [Display(Name = "نام مشتری")]
        public string CustomerName { get; set; } = string.Empty;

        [Display(Name = "شماره تلفن")]
        public string CustomerPhone { get; set; } = string.Empty;

        [Display(Name = "تراز خالص")]
        public decimal NetBalance { get; set; }

        [Display(Name = "ارز اصلی")]
        public string PrimaryCurrency { get; set; } = "IRR";

        [Display(Name = "تعداد معاملات فعال")]
        public int ActiveOrderCount { get; set; }

        [Display(Name = "جزئیات ارزی")]
        public List<CurrencyBalance> CurrencyBalances { get; set; } = new List<CurrencyBalance>();
    }

    public class CurrencyBalance
    {
        [Display(Name = "کد ارز")]
        public string CurrencyCode { get; set; } = string.Empty;

        [Display(Name = "نام ارز")]
        public string CurrencyName { get; set; } = string.Empty;

        [Display(Name = "موجودی")]
        public decimal Balance { get; set; }

        [Display(Name = "مقدار بدهی")]
        public decimal DebtAmount { get; set; }

        [Display(Name = "مقدار بستانکاری")]
        public decimal CreditAmount { get; set; }
    }
}
