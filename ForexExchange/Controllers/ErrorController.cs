using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;

namespace ForexExchange.Controllers
{
    public class ErrorController : Controller
    {
        private readonly ILogger<ErrorController> _logger;

        public ErrorController(ILogger<ErrorController> logger)
        {
            _logger = logger;
        }

        [Route("Error/{statusCode}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            switch (statusCode)
            {
                case 404:
                    _logger.LogWarning("404 Error: Page not found - {RequestPath}", 
                        HttpContext.Request.Path);
                    return View("NotFound");
                
                case 403:
                    _logger.LogWarning("403 Error: Access denied - {RequestPath} - User: {User}", 
                        HttpContext.Request.Path, User?.Identity?.Name ?? "Anonymous");
                    return View("AccessDenied");
                
                case 500:
                    _logger.LogError("500 Error: Internal server error - {RequestPath}", 
                        HttpContext.Request.Path);
                    return View("ServerError");
                
                default:
                    _logger.LogWarning("HTTP {StatusCode} Error - {RequestPath}", 
                        statusCode, HttpContext.Request.Path);
                    return View("NotFound"); // Default to 404 page for other errors
            }
        }

        [Route("Error")]
        [AllowAnonymous]
        public IActionResult Error()
        {
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerFeature>();
            
            if (exceptionFeature != null)
            {
                _logger.LogError(exceptionFeature.Error, 
                    "Unhandled exception occurred - {RequestPath}", 
                    HttpContext.Request.Path);
            }

            return View("ServerError");
        }

        // Specific action for 404 errors
        [Route("NotFound")]
        public IActionResult NotFound()
        {
            Response.StatusCode = 404;
            return View();
        }

        // Specific action for 403 errors
        [Route("AccessDenied")]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = 403;
            return View();
        }

        // Specific action for 500 errors
        [Route("ServerError")]
        public IActionResult ServerError()
        {
            Response.StatusCode = 500;
            return View();
        }
    }
}