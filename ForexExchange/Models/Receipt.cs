using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    public enum ReceiptType
    {
        PaymentReceipt = 0,    // رسید پرداخت
        BankStatement = 1      // گردش حساب
    }
    
    public class Receipt
    {
        public int Id { get; set; }
        
        [Required]
        public int CustomerId { get; set; }
        
        public int? OrderId { get; set; }
        
        public int? TransactionId { get; set; }
        
        [Required]
        public ReceiptType Type { get; set; }
        
        [Required]
        [StringLength(100)]
        public string FileName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string ContentType { get; set; } = string.Empty;
        
        [Required]
        public byte[] ImageData { get; set; } = new byte[0];
        
        // OCR extracted data
        public string? ExtractedText { get; set; }
        public string? OcrText { get; set; } // Full OCR text
        public string? ParsedAmount { get; set; }
        public string? ParsedReferenceId { get; set; }
        public string? ParsedDate { get; set; }
        public string? ParsedAccountNumber { get; set; }
        
        public DateTime UploadedAt { get; set; } = DateTime.Now;
        
        public bool IsVerified { get; set; } = false;
        public DateTime? VerifiedAt { get; set; }
        
        [StringLength(500)]
        public string? Notes { get; set; }
        
        // Navigation properties
        public Customer Customer { get; set; } = null!;
        public Order? Order { get; set; }
        public Transaction? Transaction { get; set; }
    }
}
