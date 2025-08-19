using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using ForexExchange.Models;

namespace ForexExchange.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ForexDbContext _context;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ForexDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
        }

        // GET: /Account/Register
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
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
                // Check if email already exists (only if email is provided)
                if (!string.IsNullOrWhiteSpace(model.Email))
                {
                    var existingUserByEmail = await _userManager.FindByEmailAsync(model.Email);
                    if (existingUserByEmail != null)
                    {
                        ModelState.AddModelError("Email", "کاربری با این ایمیل قبلاً ثبت نام کرده است.");
                        return View(model);
                    }
                }

                // Check if phone number already exists
                var existingUserByPhone = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber);
                if (existingUserByPhone != null)
                {
                    ModelState.AddModelError("PhoneNumber", "کاربری با این شماره تلفن قبلاً ثبت نام کرده است.");
                    return View(model);
                }

                // Check if a customer with this phone number already exists
                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.PhoneNumber == model.PhoneNumber && c.IsActive);
                if (existingCustomer != null)
                {
                    ModelState.AddModelError("PhoneNumber", "مشتری با این شماره تلفن در سیستم موجود است.");
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = model.PhoneNumber, // Always use phone number as username
                    Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email,
                    PhoneNumber = model.PhoneNumber,
                    FullName = model.FullName,
                    NationalId = model.NationalId,
                    Address = model.Address,
                    Role = UserRole.Customer,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    EmailConfirmed = !string.IsNullOrWhiteSpace(model.Email)
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Ensure Customer role exists
                    if (!await _roleManager.RoleExistsAsync("Customer"))
                    {
                        await _roleManager.CreateAsync(new IdentityRole("Customer"));
                    }

                    // Add user to Customer role
                    await _userManager.AddToRoleAsync(user, "Customer");

                    // Create corresponding Customer entity
                    var customer = new Customer
                    {
                        FullName = model.FullName,
                        PhoneNumber = model.PhoneNumber,
                        Email = model.Email ?? string.Empty,
                        NationalId = model.NationalId ?? string.Empty,
                        Address = model.Address ?? string.Empty,
                        CreatedAt = DateTime.Now,
                        IsActive = true
                    };

                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();

                    // Link the user to the customer
                    user.CustomerId = customer.Id;
                    await _userManager.UpdateAsync(user);

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        // GET: /Account/Login
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                // Find user by phone number
                var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber);
                if (user != null)
                {
                    var result = await _signInManager.PasswordSignInAsync(
                        user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

                    if (result.Succeeded)
                    {
                        return RedirectToLocal(returnUrl);
                    }
                    if (result.IsLockedOut)
                    {
                        ModelState.AddModelError(string.Empty, "حساب کاربری شما به دلیل تلاش‌های ناموفق زیاد قفل شده است.");
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "شماره تلفن یا رمز عبور اشتباه است.");
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "شماره تلفن یا رمز عبور اشتباه است.");
                }
            }

            return View(model);
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Profile
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var model = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                NationalId = user.NationalId,
                Address = user.Address,
                Role = user.Role.ToString()
            };

            return View(model);
        }

        // POST: /Account/Profile
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound();
                }

                user.FullName = model.FullName;
                user.NationalId = model.NationalId;
                user.Address = model.Address;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    ViewBag.Message = "پروفایل شما با موفقیت به‌روزرسانی شد.";
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }

            return View(model);
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }
    }
}