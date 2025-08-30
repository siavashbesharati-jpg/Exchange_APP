using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Models;

namespace ForexExchange.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time notifications
    /// هاب SignalR برای اعلان‌های بلادرنگ
    /// </summary>
    public class NotificationHub : Hub
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationHub(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        /// <summary>
        /// Called when a client connects to the hub
        /// فراخوانی هنگام اتصال کلاینت به هاب
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            if (Context.User?.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(Context.User);
                if (user != null)
                {
                    // Add user to their personal group for targeted notifications
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{user.Id}");

                    // Add admin users to admin group
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
                    }
                }
            }

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a client disconnects from the hub
        /// فراخوانی هنگام قطع اتصال کلاینت از هاب
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (Context.User?.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(Context.User);
                if (user != null)
                {
                    // Remove user from their personal group
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{user.Id}");

                    // Remove from admin groups
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admins");
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Join a specific notification group
        /// پیوستن به گروه اعلان خاص
        /// </summary>
        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Leave a specific notification group
        /// ترک گروه اعلان خاص
        /// </summary>
        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Send a test notification to the current user
        /// ارسال اعلان آزمایشی به کاربر فعلی
        /// </summary>
        public async Task SendTestNotification()
        {
            if (Context.User?.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(Context.User);
                if (user != null)
                {
                    await Clients.Caller.SendAsync("ReceiveNotification", new
                    {
                        title = "Test Notification",
                        message = "This is a test notification to verify real-time functionality.",
                        type = "info",
                        timestamp = DateTime.Now
                    });
                }
            }
        }
    }
}
