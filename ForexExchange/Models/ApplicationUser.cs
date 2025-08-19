using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = "";
        
        [StringLength(20)]
        public string? NationalId { get; set; }
        
        [StringLength(200)]
        public string? Address { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;
        
        public UserRole Role { get; set; } = UserRole.Customer;
        
        // Link to Customer entity if this is a customer user
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }
    }
    
    public enum UserRole
    {
        Customer = 0,
        Admin = 1,
        Operator = 2,
        Manager = 3
    }
}
