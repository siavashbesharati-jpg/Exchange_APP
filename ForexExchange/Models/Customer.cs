using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    public class Customer
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;
        
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;
        
        [StringLength(20)]
        public string NationalId { get; set; } = string.Empty;
        
        [StringLength(200)]
        public string Address { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Transaction> BuyTransactions { get; set; } = new List<Transaction>();
        public ICollection<Transaction> SellTransactions { get; set; } = new List<Transaction>();
        public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
    }
}
