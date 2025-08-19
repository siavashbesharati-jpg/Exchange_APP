using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    public enum CurrencyType
    {
        Toman = 0,     // تومان
        USD = 1,       // دلار آمریکا
        EUR = 2,       // یورو
        AED = 3,       // درهم امارات
        OMR = 4,       // ریال عمان
        TRY = 5        // لیر ترکیه
    }
    
    public enum OrderType
    {
        Buy = 0,   // خرید
        Sell = 1   // فروش
    }
    
    public enum OrderStatus
    {
        Open = 0,          // باز
        Matched = 1,       // مچ شده
        PartiallyFilled = 2, // نیمه تکمیل
        Completed = 3,     // تکمیل شده
        Cancelled = 4      // لغو شده
    }
    
    public class Order
    {
        public int Id { get; set; }
        
        [Required]
        public int CustomerId { get; set; }
        
        [Required]
        public OrderType OrderType { get; set; }
        
        [Required]
        public CurrencyType Currency { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Rate { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalInToman { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal FilledAmount { get; set; } = 0;
        
        [Required]
        public OrderStatus Status { get; set; } = OrderStatus.Open;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        
        [StringLength(500)]
        public string? Notes { get; set; }
        
        // Navigation properties
        public Customer Customer { get; set; } = null!;
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
    }
}
