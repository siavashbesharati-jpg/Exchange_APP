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

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true; public UserRole Role { get; set; } = UserRole.Customer;

        [StringLength(160)]
        public string? TotpSecret { get; set; }

        public DateTime? TotpSecretUpdatedAt { get; set; }

        // Link to Customer entity if this is a customer user
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }
    }

    public enum UserRole
    {
        [Display(Name = "توسعه دهنده")]
        Programmer = 0,
        [Display(Name = "مدیر")]
        Admin = 1,
        [Display(Name = "کارمند")]
        Operator = 2,
        [Display(Name = "کاربر")]
        Customer = 3,
    }
}
