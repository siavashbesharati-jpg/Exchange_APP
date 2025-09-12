using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    /// <summary>
    /// Bank Account Balance History - Event Sourcing for Bank Account Balances
    /// تاریخچه موجودی حساب بانکی - منبع رویدادها برای موجودی حساب‌های بانکی
    /// 
    /// CRITICAL: This maintains EXACT same calculation logic as existing BankAccount balance system
    /// </summary>
    [Table("BankAccountBalanceHistory")]
    public class BankAccountBalanceHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [Display(Name = "Bank Account ID - شناسه حساب بانکی")]
        public int BankAccountId { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Transaction Type - نوع تراکنش")]
        public string TransactionType { get; set; } = string.Empty; // 'Document', 'ManualEdit'

        [Display(Name = "Reference ID - شناسه مرجع")]
        public int? ReferenceId { get; set; } // DocumentId

        [Required]
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Balance Before - موجودی قبل")]
        public decimal BalanceBefore { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Transaction Amount - مقدار تراکنش")]
        public decimal TransactionAmount { get; set; } // +/- amount

        [Required]
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Balance After - موجودی بعد")]
        public decimal BalanceAfter { get; set; }

        [StringLength(500)]
        [Display(Name = "Description - توضیحات")]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Transaction Date - تاریخ تراکنش")]
        public DateTime TransactionDate { get; set; }

        [Required]
        [Display(Name = "Created At - تاریخ ایجاد")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        [Display(Name = "Created By - ایجاد شده توسط")]
        public string? CreatedBy { get; set; }

        // Navigation properties
        public virtual BankAccount BankAccount { get; set; } = null!;

        /// <summary>
        /// Validates that BalanceAfter = BalanceBefore + TransactionAmount
        /// اعتبارسنجی که موجودی بعد = موجودی قبل + مقدار تراکنش
        /// </summary>
        public bool IsCalculationValid()
        {
            return Math.Abs((BalanceBefore + TransactionAmount) - BalanceAfter) < 0.0001m;
        }

        /// <summary>
        /// Auto-calculate BalanceAfter from BalanceBefore + TransactionAmount
        /// محاسبه خودکار موجودی بعد از موجودی قبل + مقدار تراکنش
        /// </summary>
        public void CalculateBalanceAfter()
        {
            BalanceAfter = BalanceBefore + TransactionAmount;
        }

        /// <summary>
        /// Determines if this is a debit or credit transaction
        /// تعیین اینکه آیا این تراکنش بدهکار یا بستانکار است
        /// </summary>
        public bool IsCredit()
        {
            return TransactionAmount > 0;
        }

        /// <summary>
        /// Gets the absolute amount for display purposes
        /// دریافت مقدار مطلق برای اهداف نمایش
        /// </summary>
        public decimal GetAbsoluteAmount()
        {
            return Math.Abs(TransactionAmount);
        }
    }
}
