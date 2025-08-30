using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Models;
using ForexExchange.Services;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ForexExchange.Controllers
{
    /// <summary>
    /// Admin Management Controller
    /// کنترلر مدیریت ادمین
    /// </summary>
    [Authorize]
    public class AdminManagementController : Controller
    {
        private readonly AdminActivityService _adminActivityService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ForexDbContext _context;

        public AdminManagementController(
            AdminActivityService adminActivityService,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ForexDbContext context)
        {
            _adminActivityService = adminActivityService;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        /// <summary>
        /// Admin Activity Log Index
        /// صفحه اصلی لاگ فعالیت‌های ادمین
        /// </summary>
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(
            string? adminUserId = null,
            AdminActivityType? activityType = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int page = 1,
            int pageSize = 50)
        {
            // Get current user
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            // Get activities (all admins can see all activities)
            var activities = await _adminActivityService.GetAllActivitiesAsync(
                adminUserId, activityType, fromDate, toDate, pageSize * page);

            // Get pagination data
            var totalActivities = activities.Count;
            var paginatedActivities = activities.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // Get all admin users for filter dropdown
            var adminUsers = new List<ApplicationUser>();
            var adminRole = await _roleManager.FindByNameAsync("Admin");

            if (adminRole != null)
            {
                var adminUserIds = _context.UserRoles
                    .Where(ur => ur.RoleId == adminRole.Id)
                    .Select(ur => ur.UserId)
                    .Distinct();
                adminUsers = await _context.Users
                    .Where(u => adminUserIds.Contains(u.Id))
                    .OrderBy(u => u.UserName)
                    .ToListAsync();
            }

            // Get activity statistics
            var stats = await _adminActivityService.GetActivityStatisticsAsync(fromDate, toDate);

            ViewBag.CurrentUser = currentUser;
            ViewBag.IsSuperAdmin = true; // All admins have full access now
            ViewBag.AdminUsers = adminUsers;
            ViewBag.Activities = paginatedActivities;
            ViewBag.TotalActivities = totalActivities;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalActivities / pageSize);
            ViewBag.ActivityStats = stats;
            ViewBag.FilterAdminUserId = adminUserId;
            ViewBag.FilterActivityType = activityType;
            ViewBag.FilterFromDate = fromDate;
            ViewBag.FilterToDate = toDate;

            return View();
        }

        /// <summary>
        /// Get activity details
        /// دریافت جزئیات فعالیت
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetActivityDetails(int id)
        {
            var activity = await _context.AdminActivities
                .FirstOrDefaultAsync(a => a.Id == id);

            if (activity == null)
                return NotFound();

            // Check permissions (all admins can see all activity details)
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Forbid();

            return Json(new
            {
                activity.Id,
                activity.AdminUserId,
                activity.AdminUsername,
                activity.ActivityType,
                activity.Description,
                activity.Details,
                activity.EntityType,
                activity.EntityId,
                activity.OldValue,
                activity.NewValue,
                activity.IsSuccess,
                activity.Timestamp,
                activity.IpAddress,
                activity.UserAgent
            });
        }

        /// <summary>
        /// Admin Dashboard
        /// داشبورد ادمین
        /// </summary>
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Dashboard()
        {
            // Get system statistics
            var totalUsers = await _userManager.Users.CountAsync();
            var totalAdmins = 0;

            var adminRole = await _roleManager.FindByNameAsync("Admin");

            if (adminRole != null)
            {
                totalAdmins = _context.UserRoles.Count(ur => ur.RoleId == adminRole.Id);
            }

            // Get recent activities
            var recentActivities = await _adminActivityService.GetAllActivitiesAsync(limit: 10);

            // Get activity statistics for last 30 days
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            var monthlyStats = await _adminActivityService.GetActivityStatisticsAsync(thirtyDaysAgo);

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalAdmins = totalAdmins;
            ViewBag.TotalSuperAdmins = 0; // No SuperAdmin role exists
            ViewBag.RecentActivities = recentActivities;
            ViewBag.MonthlyStats = monthlyStats;

            return View();
        }

        /// <summary>
        /// Manage Admin Users
        /// مدیریت کاربران ادمین
        /// </summary>
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ManageAdmins()
        {
            var adminRole = await _roleManager.FindByNameAsync("Admin");

            if (adminRole == null)
            {
                TempData["Error"] = "نقش ادمین یافت نشد.";
                return RedirectToAction("Dashboard");
            }

            var adminUserIds = _context.UserRoles
                .Where(ur => ur.RoleId == adminRole.Id)
                .Select(ur => ur.UserId)
                .Distinct();

            var adminUsers = await _context.Users
                .Where(u => adminUserIds.Contains(u.Id))
                .OrderBy(u => u.UserName)
                .ToListAsync();

            return View(adminUsers);
        }

        /// <summary>
        /// Create New Admin User
        /// ایجاد کاربر ادمین جدید
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAdmin(string userName, string email, string password, UserRole role)
        {
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
            {
                TempData["Error"] = "نام کاربری و رمز عبور الزامی هستند.";
                return RedirectToAction("ManageAdmins");
            }

            var user = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                EmailConfirmed = true,
                Role = role
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                // Add to Admin role for Identity (only Admin role exists)
                await _userManager.AddToRoleAsync(user, "Admin");

                // Log activity
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    await _adminActivityService.LogActivityAsync(
                        currentUser.Id,
                        currentUser.UserName ?? "Unknown",
                        AdminActivityType.UserCreated,
                        $"کاربر جدید ایجاد شد: {userName} با نقش {role}",
                        JsonSerializer.Serialize(new { UserId = user.Id, Role = role }),
                        "ApplicationUser",
                        null,
                        null,
                        user.Id
                    );
                }

                TempData["Success"] = $"کاربر {userName} با موفقیت ایجاد شد.";
            }
            else
            {
                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("ManageAdmins");
        }

        /// <summary>
        /// Change Admin User Role
        /// تغییر نقش کاربر ادمین
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeAdminRole(string userId, UserRole newRole)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "کاربر یافت نشد." });
                }

                TempData["Error"] = "کاربر یافت نشد.";
                return RedirectToAction("ManageAdmins");
            }

            // Prevent users from changing their own role
            var currentUserCheck = await _userManager.GetUserAsync(User);
            if (currentUserCheck != null && currentUserCheck.Id == userId)
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "نمی‌توانید نقش خود را تغییر دهید." });
                }

                TempData["Error"] = "نمی‌توانید نقش خود را تغییر دهید.";
                return RedirectToAction("ManageAdmins");
            }

            var oldRole = user.Role;

            // Update the user's role in the database
            user.Role = newRole;
            var updateResult = await _userManager.UpdateAsync(user);

            if (updateResult.Succeeded)
            {
                // Log activity
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    await _adminActivityService.LogActivityAsync(
                        currentUser.Id,
                        currentUser.UserName ?? "Unknown",
                        AdminActivityType.UserUpdated,
                        $"نقش کاربر {user.UserName} تغییر یافت از {oldRole} به {newRole}",
                        JsonSerializer.Serialize(new { UserId = user.Id, OldRole = oldRole, NewRole = newRole }),
                        "ApplicationUser",
                        null,
                        oldRole.ToString(),
                        newRole.ToString()
                    );
                }

                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = $"نقش کاربر {user.UserName} با موفقیت تغییر یافت." });
                }

                TempData["Success"] = $"نقش کاربر {user.UserName} با موفقیت تغییر یافت.";
            }
            else
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = string.Join(", ", updateResult.Errors.Select(e => e.Description)) });
                }

                TempData["Error"] = string.Join(", ", updateResult.Errors.Select(e => e.Description));
            }

            return RedirectToAction("ManageAdmins");
        }

        /// <summary>
        /// Delete Admin User
        /// حذف کاربر ادمین
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAdmin(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "کاربر یافت نشد." });
                }

                TempData["Error"] = "کاربر یافت نشد.";
                return RedirectToAction("ManageAdmins");
            }

            // Prevent users from deleting themselves
            var currentUserCheck = await _userManager.GetUserAsync(User);
            if (currentUserCheck != null && currentUserCheck.Id == userId)
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "نمی‌توانید خود را حذف کنید." });
                }

                TempData["Error"] = "نمی‌توانید خود را حذف کنید.";
                return RedirectToAction("ManageAdmins");
            }

            // Prevent deleting the last Admin
            var adminRole = await _roleManager.FindByNameAsync("Admin");
            if (adminRole != null)
            {
                var adminCount = _context.UserRoles.Count(ur => ur.RoleId == adminRole.Id);
                var isUserAdmin = await _userManager.IsInRoleAsync(user, "Admin");

                if (isUserAdmin && adminCount <= 1)
                {
                    // Check if this is an AJAX request
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "نمی‌توان آخرین ادمین را حذف کرد." });
                    }

                    TempData["Error"] = "نمی‌توان آخرین ادمین را حذف کرد.";
                    return RedirectToAction("ManageAdmins");
                }
            }

            var userName = user.UserName;
            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                // Log activity
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    await _adminActivityService.LogActivityAsync(
                        currentUser.Id,
                        currentUser.UserName ?? "Unknown",
                        AdminActivityType.UserDeleted,
                        $"کاربر ادمین حذف شد: {userName}",
                        JsonSerializer.Serialize(new { DeletedUserId = userId, DeletedUserName = userName }),
                        "ApplicationUser",
                        null
                    );
                }

                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = $"کاربر ادمین {userName} با موفقیت حذف شد." });
                }

                TempData["Success"] = $"کاربر ادمین {userName} با موفقیت حذف شد.";
            }
            else
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
                }

                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("ManageAdmins");
        }

        /// <summary>
        /// Export Admin Activities
        /// صادرات فعالیت‌های ادمین
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportActivities(
            string? adminUserId = null,
            AdminActivityType? activityType = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var activities = await _adminActivityService.GetAllActivitiesAsync(
                adminUserId, activityType, fromDate, toDate);

            // Log export activity
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                await _adminActivityService.LogDataExportAsync(
                    currentUser.Id,
                    currentUser.UserName ?? "Unknown",
                    "AdminActivities",
                    activities.Count
                );
            }

            // Create CSV content
            var csv = "Id,AdminUserId,AdminUsername,ActivityType,Description,Timestamp,IpAddress,IsSuccess\n";
            foreach (var activity in activities)
            {
                csv += $"{activity.Id},{activity.AdminUserId},{activity.AdminUsername},{activity.ActivityType},{activity.Description},{activity.Timestamp},{activity.IpAddress},{activity.IsSuccess}\n";
            }

            var fileName = $"admin_activities_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
        }
    }
}
