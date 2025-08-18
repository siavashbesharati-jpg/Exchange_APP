using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    public class ExchangeRate
    {
        public int Id { get; set; }
        
        [Required]
        public CurrencyType Currency { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,4)")]
        public decimal BuyRate { get; set; }  // نرخ خرید
        
        [Required]
        [Column(TypeName = "decimal(18,4)")]
        public decimal SellRate { get; set; } // نرخ فروش
        
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        
        [Required]
        [StringLength(50)]
        public string UpdatedBy { get; set; } = "System";
        
        public bool IsActive { get; set; } = true;
    }
}
