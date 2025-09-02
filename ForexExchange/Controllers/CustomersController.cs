using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Models;
using ForexExchange.Services;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Manager,Staff")]
    public class CustomersController : Controller
    {
    private readonly ForexDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CustomersController> _logger;
    private readonly CustomerDebtCreditService _debtCreditService;

    public CustomersController(
        ForexDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<CustomersController> logger,
        CustomerDebtCreditService debtCreditService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _debtCreditService = debtCreditService;
    }        // GET: Customers
        public async Task<IActionResult> Index()
        {
            var customers = await _context.Customers
                .Where(c => c.IsActive && c.IsSystem == false)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(customers);
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.Orders.OrderByDescending(o => o.CreatedAt))
                    .ThenInclude(o => o.FromCurrency)
                .Include(c => c.Orders.OrderByDescending(o => o.CreatedAt))
                    .ThenInclude(o => o.ToCurrency)
                .Include(c => c.BuyTransactions.OrderByDescending(t => t.CreatedAt))
                    .ThenInclude(t => t.SellerCustomer)
                .Include(c => c.BuyTransactions.OrderByDescending(t => t.CreatedAt))
                    .ThenInclude(t => t.FromCurrency)
                .Include(c => c.BuyTransactions.OrderByDescending(t => t.CreatedAt))
                    .ThenInclude(t => t.ToCurrency)
                .Include(c => c.SellTransactions.OrderByDescending(t => t.CreatedAt))
                    .ThenInclude(t => t.BuyerCustomer)
                .Include(c => c.SellTransactions.OrderByDescending(t => t.CreatedAt))
                    .ThenInclude(t => t.FromCurrency)
                .Include(c => c.SellTransactions.OrderByDescending(t => t.CreatedAt))
                    .ThenInclude(t => t.ToCurrency)
                .Include(c => c.Receipts.OrderByDescending(r => r.UploadedAt))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // GET: Customers/Profile/5 - Comprehensive customer profile
        public async Task<IActionResult> Profile(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.Orders.OrderByDescending(o => o.CreatedAt))
                    .ThenInclude(o => o.FromCurrency)
                .Include(c => c.Orders.OrderByDescending(o => o.CreatedAt))
                    .ThenInclude(o => o.ToCurrency)
                .Include(c => c.BuyTransactions.OrderByDescending(t => t.CreatedAt))
                    .ThenInclude(t => t.SellerCustomer)
                .Include(c => c.BuyTransactions.OrderByDescending(t => t.CreatedAt))
                    .ThenInclude(t => t.FromCurrency)
                .Include(c => c.BuyTransactions.OrderByDescending(t => t.CreatedAt))
                    .ThenInclude(t => t.ToCurrency)
                .Include(c => c.SellTransactions.OrderByDescending(t => t.CreatedAt))
                    .ThenInclude(t => t.BuyerCustomer)
                .Include(c => c.SellTransactions.OrderByDescending(t => t.CreatedAt))
                    .ThenInclude(t => t.FromCurrency)
                .Include(c => c.SellTransactions.OrderByDescending(t => t.CreatedAt))
                    .ThenInclude(t => t.ToCurrency)
                .Include(c => c.Receipts.OrderByDescending(r => r.UploadedAt))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
            {
                return NotFound();
            }

            // Calculate customer statistics
            var stats = new CustomerProfileStats
            {
                TotalOrders = customer.Orders.Count,
                CompletedOrders = customer.Orders.Count(o => o.Status == OrderStatus.Completed),
                PendingOrders = customer.Orders.Count(o => o.Status == OrderStatus.Open),
                TotalTransactions = customer.BuyTransactions.Count + customer.SellTransactions.Count,
                CompletedTransactions = customer.BuyTransactions.Count(t => t.Status == TransactionStatus.Completed) +
                                     customer.SellTransactions.Count(t => t.Status == TransactionStatus.Completed),
                TotalReceipts = customer.Receipts.Count,
                VerifiedReceipts = customer.Receipts.Count(r => r.IsVerified),
                TotalVolumeInToman = customer.Orders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalInToman),
                RegistrationDays = (DateTime.Now - customer.CreatedAt).Days
            };

            // Calculate debt/credit information for this customer
            var customerDebtCredit = await _debtCreditService.GetCustomerDebtCreditAsync(customer.Id);

            ViewBag.CustomerStats = stats;
            ViewBag.CustomerDebtCredit = customerDebtCredit;
            return View(customer);
        }

        // GET: Customers/Create
        public IActionResult Create()
        {
            return View(new CustomerCreateViewModel());
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerCreateViewModel model)
        {
            // Remove any email validation errors from ModelState first
            ModelState.Remove("Email");
            
            // Custom email validation - validate format only if email is provided
            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                var emailAttribute = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
                if (!emailAttribute.IsValid(model.Email))
                {
                    ModelState.AddModelError("Email", "فرمت ایمیل صحیح نیست");
                }
            }

            if (ModelState.IsValid)
            {
                // Check if email exists only if email is provided
                if (!string.IsNullOrWhiteSpace(model.Email))
                {
                    var existingUser = await _userManager.FindByEmailAsync(model.Email!);
                    if (existingUser != null)
                    {
                        ModelState.AddModelError("Email", "کاربری با این ایمیل قبلاً ثبت شده است.");
                        return View(model);
                    }
                }

                // Check if phone number already exists (normalize first)
                string normalizedPhoneNumber = PhoneNumberService.NormalizePhoneNumber(model.PhoneNumber);
                
                // Validate normalized phone number
                if (!PhoneNumberService.IsValidNormalizedPhoneNumber(normalizedPhoneNumber))
                {
                    ModelState.AddModelError("PhoneNumber", "فرمت شماره تلفن صحیح نیست. لطفاً شماره تلفن معتبر وارد کنید.");
                    return View(model);
                }

                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.PhoneNumber == normalizedPhoneNumber && c.IsActive);

                if (existingCustomer != null)
                {
                    ModelState.AddModelError("PhoneNumber", "مشتری با این شماره تلفن قبلاً ثبت شده است.");
                    return View(model);
                }

                // Create Customer entity
                var customer = new Customer
                {
                    FullName = model.FullName,
                    Email = model.Email ?? string.Empty,
                    PhoneNumber = normalizedPhoneNumber, // Use normalized phone number
                    NationalId = model.NationalId ?? string.Empty,
                    Address = model.Address ?? string.Empty,
                    CreatedAt = DateTime.Now,
                    IsActive = model.IsActive
                };

                _context.Add(customer);
                await _context.SaveChangesAsync();

                // Create corresponding ApplicationUser
                var user = new ApplicationUser
                {
                    UserName = normalizedPhoneNumber, // Use normalized phone number as username
                    Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email,
                    PhoneNumber = normalizedPhoneNumber, // Use normalized phone number
                    FullName = model.FullName,
                    NationalId = model.NationalId ?? string.Empty,
                    Address = model.Address ?? string.Empty,
                    Role = UserRole.Customer,
                    IsActive = model.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    EmailConfirmed = !string.IsNullOrWhiteSpace(model.Email),
                    CustomerId = customer.Id
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Customer");
                    TempData["SuccessMessage"] = "مشتری و حساب کاربری با موفقیت ایجاد شد.";
                    return RedirectToAction(nameof(Details), new { id = customer.Id });
                }
                else
                {
                    // If user creation failed, remove the customer
                    _context.Remove(customer);
                    await _context.SaveChangesAsync();

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }
            return View(model);
        }

        // GET: Customers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            var model = new CustomerEditViewModel
            {
                Id = customer.Id,
                FullName = customer.FullName,
                Email = customer.Email,
                PhoneNumber = customer.PhoneNumber,
                NationalId = customer.NationalId,
                Address = customer.Address,
                IsActive = customer.IsActive
            };

            return View(model);
        }

        // POST: Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CustomerEditViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            // Remove any email validation errors from ModelState first
            ModelState.Remove("Email");
            
            // Custom email validation - validate format only if email is provided
            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                var emailAttribute = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
                if (!emailAttribute.IsValid(model.Email))
                {
                    ModelState.AddModelError("Email", "فرمت ایمیل صحیح نیست");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var customer = await _context.Customers.FindAsync(id);
                    if (customer == null)
                    {
                        return NotFound();
                    }

                    // Normalize phone number for validation and storage
                    string normalizedPhoneNumber = PhoneNumberService.NormalizePhoneNumber(model.PhoneNumber);
                    
                    // Validate normalized phone number
                    if (!PhoneNumberService.IsValidNormalizedPhoneNumber(normalizedPhoneNumber))
                    {
                        ModelState.AddModelError("PhoneNumber", "فرمت شماره تلفن صحیح نیست. لطفاً شماره تلفن معتبر وارد کنید.");
                        return View(model);
                    }

                    // Check if email changed and if new email exists (only if email is provided)
                    if (customer.Email != model.Email && !string.IsNullOrWhiteSpace(model.Email))
                    {
                        var existingUser = await _userManager.FindByEmailAsync(model.Email!);
                        if (existingUser != null && existingUser.CustomerId != customer.Id)
                        {
                            ModelState.AddModelError("Email", "کاربری با این ایمیل قبلاً ثبت شده است.");
                            return View(model);
                        }
                    }

                    // Check if phone number changed and if new normalized phone exists
                    if (customer.PhoneNumber != normalizedPhoneNumber)
                    {
                        var existingCustomer = await _context.Customers
                            .FirstOrDefaultAsync(c => c.PhoneNumber == normalizedPhoneNumber && c.IsActive && c.Id != id);

                        if (existingCustomer != null)
                        {
                            ModelState.AddModelError("PhoneNumber", "مشتری با این شماره تلفن قبلاً ثبت شده است.");
                            return View(model);
                        }
                    }

                    // Update customer entity
                    customer.FullName = model.FullName;
                    customer.Email = model.Email ?? string.Empty;
                    customer.PhoneNumber = normalizedPhoneNumber; // Use normalized phone number
                    customer.NationalId = model.NationalId ?? string.Empty;
                    customer.Address = model.Address ?? string.Empty;
                    customer.IsActive = model.IsActive;

                    _context.Update(customer);

                    // Update corresponding ApplicationUser
                    var user = await _userManager.Users.FirstOrDefaultAsync(u => u.CustomerId == customer.Id);
                    if (user != null)
                    {
                        user.UserName = normalizedPhoneNumber; // Use normalized phone number as username
                        user.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email;
                        user.PhoneNumber = normalizedPhoneNumber; // Use normalized phone number
                        user.FullName = model.FullName;
                        user.NationalId = model.NationalId ?? string.Empty;
                        user.Address = model.Address ?? string.Empty;
                        user.IsActive = model.IsActive;
                        user.EmailConfirmed = !string.IsNullOrWhiteSpace(model.Email);

                        await _userManager.UpdateAsync(user);

                        // Update password if provided
                        if (!string.IsNullOrEmpty(model.NewPassword))
                        {
                            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                            var passwordResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                            if (!passwordResult.Succeeded)
                            {
                                foreach (var error in passwordResult.Errors)
                                {
                                    ModelState.AddModelError(string.Empty, $"خطا در تغییر رمز عبور: {error.Description}");
                                }
                                return View(model);
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "اطلاعات مشتری با موفقیت به‌روزرسانی شد.";
                    return RedirectToAction(nameof(Details), new { id = customer.Id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerExists(model.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return View(model);
        }

        // GET: Customers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(m => m.Id == id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                // Soft delete - just mark as inactive
                customer.IsActive = false;
                _context.Update(customer);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "مشتری غیرفعال شد.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: API endpoint for customer search
        [HttpGet]
        public async Task<IActionResult> Search(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Json(new List<object>());
            }

            var customers = await _context.Customers
                .Where(c => c.IsActive &&
                           (c.FullName.Contains(term) || c.PhoneNumber.Contains(term)))
                .Select(c => new
                {
                    id = c.Id,
                    text = $"{c.FullName} - {c.PhoneNumber}"
                })
                .Take(10)
                .ToListAsync();

            return Json(customers);
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }
    }
}
