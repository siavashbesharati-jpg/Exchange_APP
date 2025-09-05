using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    public enum DocumentType
    {
        [Display(Name = "نقدی")]
        Cash = 0,
        [Display(Name = "رسید بانکی ")]

        BankStatement = 1,
        [Display(Name = " حواله  ")]
        Havala = 2
    }

    public enum PayerType
    {
        Customer = 0,   // مشتری
        System = 1      // سیستم
    }

    /// <summary>
    /// Accounting Document (سند حسابداری) - replaces Receipt model
    /// Tracks all financial movements between customers, system, and bank accounts
    /// </summary>
    public class AccountingDocument
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Document Type - نوع سند")]
        public DocumentType Type { get; set; }

        [Required]
        [Display(Name = "Payer Type - نوع پرداخت کننده")]
        public PayerType PayerType { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Amount - مبلغ")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(3)]
        [Display(Name = "Currency - ارز")]
        public string CurrencyCode { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Customer - مشتری")]
        public int CustomerId { get; set; }

        [Display(Name = "Bank Account - حساب بانکی")]
        public int? BankAccountId { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Document Title - عنوان سند")]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Description - توضیحات")]
        public string? Description { get; set; }

        [Display(Name = "Document Date - تاریخ سند")]
        public DateTime DocumentDate { get; set; } = DateTime.Now;

        [Display(Name = "Created At - تاریخ ایجاد")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Is Verified - تأیید شده")]
        public bool IsVerified { get; set; } = false;

        [Display(Name = "Verified At - تاریخ تأیید")]
        public DateTime? VerifiedAt { get; set; }

        [StringLength(100)]
        [Display(Name = "Verified By - تأیید شده توسط")]
        public string? VerifiedBy { get; set; }

        // Optional: Reference number for external tracking
        [StringLength(50)]
        [Display(Name = "Reference Number - شماره مرجع")]
        public string? ReferenceNumber { get; set; }

        // File attachment (optional - for supporting documents)
        [StringLength(100)]
        [Display(Name = "File Name - نام فایل")]
        public string? FileName { get; set; }

        [StringLength(50)]
        [Display(Name = "Content Type - نوع محتوا")]
        public string? ContentType { get; set; }

        [Display(Name = "File Data - داده فایل")]
        public byte[]? FileData { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes - یادداشت‌ها")]
        public string? Notes { get; set; }

        // Navigation properties
        [Display(Name = "Customer - مشتری")]
        public Customer? Customer { get; set; }

        [Display(Name = "Bank Account - حساب بانکی")]
        public BankAccount? BankAccount { get; set; }

        // Helper properties for display
        public string PayerName
        {
            get
            {
                return PayerType switch
                {
                    PayerType.Customer => Customer?.FullName ?? "مشتری نامشخص",
                    PayerType.System => "سیستم",
                    _ => "نامشخص"
                };
            }
        }

        public string ReceiverName
        {
            get
            {
                return PayerType switch
                {
                    PayerType.Customer => "سیستم",
                    PayerType.System => Customer?.FullName ?? "مشتری نامشخص",
                    _ => "نامشخص"
                };
            }
        }

        public string FormattedAmount => $"{Amount:N0} {CurrencyCode}";
    }
}
