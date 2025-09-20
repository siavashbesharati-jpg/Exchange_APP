using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ForexExchange.Services
{
    /// <summary>
    /// Custom user ID provider for SignalR to use user ID instead of username
    /// </summary>
    public class CustomUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            // Use the user's ID from the NameIdentifier claim
            return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}