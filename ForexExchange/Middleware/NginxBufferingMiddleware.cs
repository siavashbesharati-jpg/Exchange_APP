using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace ForexExchange.Middleware
{
    /// <summary>
    /// Middleware to disable nginx buffering for AJAX requests
    /// This ensures immediate response delivery for better UX
    /// </summary>
    public class NginxBufferingMiddleware
    {
        private readonly RequestDelegate _next;

        public NginxBufferingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Check if this is an AJAX request
            var isAjaxRequest = context.Request.Headers["X-Requested-With"].ToString() == "XMLHttpRequest";
            
            if (isAjaxRequest)
            {
                // Set headers before processing to ensure they're sent immediately
                context.Response.OnStarting(() =>
                {
                    // Disable nginx buffering for immediate response delivery
                    context.Response.Headers["X-Accel-Buffering"] = "no";
                    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                    context.Response.Headers["Pragma"] = "no-cache";
                    context.Response.Headers["Expires"] = "0";
                    return Task.CompletedTask;
                });
            }

            await _next(context);
        }
    }
}

