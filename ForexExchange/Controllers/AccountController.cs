using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;

namespace ForexExchange.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ForexDbContext _context;

        private readonly ISettingsService _settingsService;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ISettingsService settingsService,
            ForexDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
            _settingsService = settingsService;
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
                // Normalize phone number
                string normalizedPhoneNumber = PhoneNumberService.NormalizePhoneNumber(model.PhoneNumber);

                // Validate normalized phone number
                if (!PhoneNumberService.IsValidNormalizedPhoneNumber(normalizedPhoneNumber))
                {
                    ModelState.AddModelError("PhoneNumber", "فرمت شماره تلفن صحیح نیست. لطفاً شماره تلفن معتبر وارد کنید.");
                    return View(model);
                }

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

                // Check if normalized phone number already exists
                var existingUserByPhone = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == normalizedPhoneNumber || u.UserName == normalizedPhoneNumber);
                if (existingUserByPhone != null)
                {
                    ModelState.AddModelError("PhoneNumber", "کاربری با این شماره تلفن قبلاً ثبت نام کرده است.");
                    return View(model);
                }

                // Check if a customer with this normalized phone number already exists
                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.PhoneNumber == normalizedPhoneNumber && c.IsActive);
                if (existingCustomer != null)
                {
                    ModelState.AddModelError("PhoneNumber", "مشتری با این شماره تلفن در سیستم موجود است.");
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = normalizedPhoneNumber, // Use normalized phone number as username
                    Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email,
                    PhoneNumber = normalizedPhoneNumber, // Use normalized phone number
                    FullName = model.FullName,
                    NationalId = model.NationalId,
                    Address = model.Address,
                    Role = UserRole.Customer,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    EmailConfirmed = !string.IsNullOrWhiteSpace(model.Email),
                    LockoutEnabled = false
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
                        PhoneNumber = normalizedPhoneNumber, // Use normalized phone number for customer entity
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

                    // Add FullName and Role claims for navbar display and authorization
                    var claims = new List<System.Security.Claims.Claim> {
                        new System.Security.Claims.Claim("FullName", user.FullName ?? user.UserName),
                        new System.Security.Claims.Claim("Role", user.Role.ToString())
                    };
                    await _userManager.AddClaimsAsync(user, claims);
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Dashboard", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        // GET: /Account/Login
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            var setting = await _settingsService.GetSystemSettingsAsync();
            ViewData["WebsiteName"] = setting.WebsiteName;
            ViewBag.IsInDemoMode = setting.IsDemoMode;
            if (setting.IsDemoMode) // if we are in demo mode, then we can display the admin user name and passowrd 
            {
                var model = new LoginViewModel()
                {
                    PhoneNumber = "09120674032",
                    Password = "admindemo1"
                };
                return View(model);

            }
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            var setting = await _settingsService.GetSystemSettingsAsync();
            ViewBag.IsInDemoMode = setting.IsDemoMode;
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                // Normalize phone number for login
                string normalizedPhoneNumber = PhoneNumberService.NormalizePhoneNumber(model.PhoneNumber);

                // Find user by normalized phone number
                var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == normalizedPhoneNumber || u.UserName == normalizedPhoneNumber);

                if (user != null)
                {
                    Console.WriteLine($"+++++ User {user.PhoneNumber} found with normalized input: {normalizedPhoneNumber}");
                    // Ensure FullName and Role claims are present for navbar display and authorization
                    var userClaims = await _userManager.GetClaimsAsync(user);
                    var existingFullNameClaim = userClaims.FirstOrDefault(c => c.Type == "FullName");
                    var existingRoleClaim = userClaims.FirstOrDefault(c => c.Type == "Role");

                    var fullNameValue = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : (user.UserName ?? "کاربر");
                    var roleValue = user.Role.ToString();

                    if (existingFullNameClaim != null)
                    {
                        // Remove old claim and add updated one
                        await _userManager.RemoveClaimAsync(user, existingFullNameClaim);
                    }

                    if (existingRoleClaim != null)
                    {
                        // Remove old role claim and add updated one
                        await _userManager.RemoveClaimAsync(user, existingRoleClaim);
                    }

                    // Add updated claims
                    await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("FullName", fullNameValue));
                    await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("Role", roleValue));

                    var result = await _signInManager.PasswordSignInAsync(
                        user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

                    Console.WriteLine($"+++++ Login result is: {result}");

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
                    Console.WriteLine($"+++++ USER NOT FOUND - No user with phone: {model.PhoneNumber} (normalized: {normalizedPhoneNumber})");
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
            return RedirectToAction("Dashboard", "Home");
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

        // GET: /Account/AccessDenied
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return RedirectToAction("AccessDenied", "Error");
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Dashboard", "Home");
            }
        }
    }
}