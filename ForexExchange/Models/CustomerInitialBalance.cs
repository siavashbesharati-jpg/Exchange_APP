using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    public class CustomerInitialBalance
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [Required]
        [StringLength(3)]
        public string CurrencyCode { get; set; } = "IRR";

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } = 0m; // can be negative or positive

        // Navigation
        public Customer Customer { get; set; } = null!;
    }
}
