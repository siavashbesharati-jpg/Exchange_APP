using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    /// <summary>
    /// Bank Account Transaction Type Enum
    /// نوع تراکنش حساب بانکی
    /// </summary>
    public enum BankAccountTransactionType
    {
        [Display(Name = "Document - سند حسابداری")]
        Document = 1,
        
        [Display(Name = "ManualEdit - ویرایش دستی")]
        ManualEdit = 2
    }

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
        [Display(Name = "Transaction Type - نوع تراکنش")]
        public BankAccountTransactionType TransactionType { get; set; } = BankAccountTransactionType.Document;

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

        [StringLength(50)]
        [Display(Name = "Transaction Number - شماره تراکنش")]
        public string? TransactionNumber { get; set; }

        [Required]
        [Display(Name = "Transaction Date - تاریخ تراکنش")]
        public DateTime TransactionDate { get; set; }

        [Required]
        [Display(Name = "Created At - تاریخ ایجاد")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        [Display(Name = "Created By - ایجاد شده توسط")]
        public string? CreatedBy { get; set; }

        // NEW: Soft delete flags
        [Required]
        [Display(Name = "Is Deleted - حذف شده")]
        public bool IsDeleted { get; set; } = false;

        [Display(Name = "Deleted At - تاریخ حذف")]
        public DateTime? DeletedAt { get; set; }

        [StringLength(100)]
        [Display(Name = "Deleted By - حذف شده توسط")]
        public string? DeletedBy { get; set; }

        // NEW: Frozen flag for accounting purposes - excludes from calculations but preserves for audit
        [Required]
        [Display(Name = "Is Frozen - منجمد شده")]
        public bool IsFrozen { get; set; } = false;

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
