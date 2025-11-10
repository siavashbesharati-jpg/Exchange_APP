using Microsoft.AspNetCore.Http;

namespace ForexExchange.Extensions
{
    /// <summary>
    /// Extension methods for HttpResponse to handle nginx buffering
    /// </summary>
    public static class HttpResponseExtensions
    {
        /// <summary>
        /// Disables nginx response buffering to allow immediate response delivery.
        /// This is especially important for AJAX requests where the client needs immediate feedback.
        /// </summary>
        /// <param name="response">The HTTP response</param>
        public static void DisableNginxBuffering(this HttpResponse response)
        {
            // X-Accel-Buffering: no tells nginx to disable buffering and send response immediately
            response.Headers["X-Accel-Buffering"] = "no";
            
            // Also set cache control headers to prevent any caching
            response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            response.Headers["Pragma"] = "no-cache";
            response.Headers["Expires"] = "0";
        }
    }
}

