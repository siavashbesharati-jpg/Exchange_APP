using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    /// <summary>
    /// Bank Account Balance Summary Model - مدل خلاصه موجودی حساب بانکی
    /// Provides a summary view of bank account balances
    /// نمای خلاصه‌ای از موجودی حساب‌های بانکی ارائه می‌دهد
    /// </summary>
    public class BankAccountBalanceSummary
    {
        /// <summary>
        /// Bank account ID
        /// شناسه حساب بانکی
        /// </summary>
        public int BankAccountId { get; set; }

        /// <summary>
        /// Bank name
        /// نام بانک
        /// </summary>
        [Display(Name = "Bank Name - نام بانک")]
        public string BankName { get; set; } = string.Empty;

        /// <summary>
        /// Account number
        /// شماره حساب
        /// </summary>
        [Display(Name = "Account Number - شماره حساب")]
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// Account holder name
        /// نام صاحب حساب
        /// </summary>
        [Display(Name = "Account Holder - نام صاحب حساب")]
        public string AccountHolderName { get; set; } = string.Empty;

        /// <summary>
        /// Currency balances (simplified to single balance per account)
        /// موجودی ارزی (ساده‌سازی شده به موجودی واحد در هر حساب)
        /// </summary>
        public List<BankAccountBalance> CurrencyBalances { get; set; } = new List<BankAccountBalance>();

        /// <summary>
        /// Total balance in IRR (simplified calculation)
        /// مجموع موجودی به تومان (محاسبه ساده‌سازی شده)
        /// </summary>
        [Display(Name = "Total Balance (IRR) - مجموع موجودی (تومان)")]
        public decimal TotalBalanceInIRR { get; set; }
    }
}
