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
        [Authorize(Roles = "SuperAdmin,Admin")]
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

            // Check if user is SuperAdmin
            var isSuperAdmin = await _userManager.IsInRoleAsync(currentUser, "SuperAdmin");

            // If not SuperAdmin, only show their own activities
            if (!isSuperAdmin)
            {
                adminUserId = currentUser.Id;
            }

            // Get activities
            var activities = await _adminActivityService.GetAllActivitiesAsync(
                adminUserId, activityType, fromDate, toDate, pageSize * page);

            // Get pagination data
            var totalActivities = activities.Count;
            var paginatedActivities = activities.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // Get all admin users for filter dropdown (only for SuperAdmin)
            var adminUsers = new List<ApplicationUser>();
            if (isSuperAdmin)
            {
                var adminRole = await _roleManager.FindByNameAsync("Admin");
                var superAdminRole = await _roleManager.FindByNameAsync("SuperAdmin");

                if (adminRole != null)
                {
                var adminUserIds = _context.UserRoles
                    .Where(ur => ur.RoleId == adminRole.Id || (superAdminRole != null && ur.RoleId == superAdminRole.Id))
                    .Select(ur => ur.UserId)
                    .Distinct();                    adminUsers = await _context.Users
                        .Where(u => adminUserIds.Contains(u.Id))
                        .OrderBy(u => u.UserName)
                        .ToListAsync();
                }
            }

            // Get activity statistics
            var stats = await _adminActivityService.GetActivityStatisticsAsync(fromDate, toDate);

            ViewBag.CurrentUser = currentUser;
            ViewBag.IsSuperAdmin = isSuperAdmin;
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
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> GetActivityDetails(int id)
        {
            var activity = await _context.AdminActivities
                .FirstOrDefaultAsync(a => a.Id == id);

            if (activity == null)
                return NotFound();

            // Check permissions
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Forbid();

            var isSuperAdmin = await _userManager.IsInRoleAsync(currentUser, "SuperAdmin");

            if (!isSuperAdmin && activity.AdminUserId != currentUser.Id)
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
        /// Super Admin Dashboard
        /// داشبورد سوپر ادمین
        /// </summary>
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Dashboard()
        {
            // Get system statistics
            var totalUsers = await _userManager.Users.CountAsync();
            var totalAdmins = 0;
            var totalSuperAdmins = 0;

            var adminRole = await _roleManager.FindByNameAsync("Admin");
            var superAdminRole = await _roleManager.FindByNameAsync("SuperAdmin");

            if (adminRole != null)
            {
                totalAdmins = _context.UserRoles.Count(ur => ur.RoleId == adminRole.Id);
            }

            if (superAdminRole != null)
            {
                totalSuperAdmins = _context.UserRoles.Count(ur => ur.RoleId == superAdminRole.Id);
            }

            // Get recent activities
            var recentActivities = await _adminActivityService.GetAllActivitiesAsync(limit: 10);

            // Get activity statistics for last 30 days
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            var monthlyStats = await _adminActivityService.GetActivityStatisticsAsync(thirtyDaysAgo);

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalAdmins = totalAdmins;
            ViewBag.TotalSuperAdmins = totalSuperAdmins;
            ViewBag.RecentActivities = recentActivities;
            ViewBag.MonthlyStats = monthlyStats;

            return View();
        }

        /// <summary>
        /// Manage Admin Users
        /// مدیریت کاربران ادمین
        /// </summary>
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ManageAdmins()
        {
            var adminRole = await _roleManager.FindByNameAsync("Admin");
            var superAdminRole = await _roleManager.FindByNameAsync("SuperAdmin");

            if (adminRole == null || superAdminRole == null)
            {
                TempData["Error"] = "نقش‌های ادمین یافت نشدند.";
                return RedirectToAction("Dashboard");
            }

            var adminUserIds = _context.UserRoles
                .Where(ur => ur.RoleId == adminRole.Id || ur.RoleId == superAdminRole.Id)
                .Select(ur => ur.UserId)
                .Distinct();

            var adminUsers = await _context.Users
                .Where(u => adminUserIds.Contains(u.Id))
                .OrderBy(u => u.UserName)
                .ToListAsync();

            // Get roles for each user
            foreach (var user in adminUsers)
            {
                // Note: Roles property doesn't exist on ApplicationUser, using UserManager instead
                // user.Roles = await _userManager.GetRolesAsync(user);
            }

            return View(adminUsers);
        }

        /// <summary>
        /// Create New Admin User
        /// ایجاد کاربر ادمین جدید
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAdmin(string userName, string email, string password, string role)
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
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                // Add to role
                if (role == "SuperAdmin" || role == "Admin")
                {
                    await _userManager.AddToRoleAsync(user, role);
                }

                // Log activity
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    await _adminActivityService.LogActivityAsync(
                        currentUser.Id,
                        currentUser.UserName ?? "Unknown",
                        AdminActivityType.UserCreated,
                        $"کاربر ادمین جدید ایجاد شد: {userName} با نقش {role}",
                        JsonSerializer.Serialize(new { UserId = user.Id, Role = role }),
                        "ApplicationUser",
                        null,
                        null,
                        user.Id
                    );
                }

                TempData["Success"] = $"کاربر ادمین {userName} با موفقیت ایجاد شد.";
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
        [Authorize(Roles = "SuperAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeAdminRole(string userId, string newRole)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "کاربر یافت نشد.";
                return RedirectToAction("ManageAdmins");
            }

            // Get current roles
            var currentRoles = await _userManager.GetRolesAsync(user);
            var oldRole = currentRoles.FirstOrDefault();

            // Remove current roles
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            // Add new role
            if (newRole == "SuperAdmin" || newRole == "Admin")
            {
                await _userManager.AddToRoleAsync(user, newRole);
            }

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
                    oldRole,
                    newRole
                );
            }

            TempData["Success"] = $"نقش کاربر {user.UserName} با موفقیت تغییر یافت.";
            return RedirectToAction("ManageAdmins");
        }

        /// <summary>
        /// Delete Admin User
        /// حذف کاربر ادمین
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAdmin(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "کاربر یافت نشد.";
                return RedirectToAction("ManageAdmins");
            }

            // Prevent deleting the last SuperAdmin
            var superAdminRole = await _roleManager.FindByNameAsync("SuperAdmin");
            if (superAdminRole != null)
            {
                var superAdminCount = _context.UserRoles.Count(ur => ur.RoleId == superAdminRole.Id);
                var isUserSuperAdmin = await _userManager.IsInRoleAsync(user, "SuperAdmin");

                if (isUserSuperAdmin && superAdminCount <= 1)
                {
                    TempData["Error"] = "نمی‌توان آخرین سوپر ادمین را حذف کرد.";
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

                TempData["Success"] = $"کاربر ادمین {userName} با موفقیت حذف شد.";
            }
            else
            {
                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("ManageAdmins");
        }

        /// <summary>
        /// Export Admin Activities
        /// صادرات فعالیت‌های ادمین
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
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
