using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CustomerTransactionsController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CustomerTransactionsController> _logger;

        public CustomerTransactionsController(
            ForexDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<CustomerTransactionsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: CustomerTransactions
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.CustomerId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var transactions = await _context.Transactions
                .Include(t => t.BuyOrder)
                .Include(t => t.SellOrder)
                .Include(t => t.BuyerCustomer)
                .Include(t => t.SellerCustomer)
                .Include(t => t.FromCurrency)
                .Include(t => t.ToCurrency)
                .Where(t => t.BuyerCustomerId == user.CustomerId || t.SellerCustomerId == user.CustomerId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            ViewBag.CurrentCustomerId = user.CustomerId;
            return View(transactions);
        }

        // GET: CustomerTransactions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user?.CustomerId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var transaction = await _context.Transactions
                .Include(t => t.BuyOrder)
                .Include(t => t.SellOrder)
                .Include(t => t.BuyerCustomer)
                .Include(t => t.SellerCustomer)
                .Include(t => t.FromCurrency)
                .Include(t => t.ToCurrency)
                .Include(t => t.Receipts)
                .FirstOrDefaultAsync(t => t.Id == id && 
                    (t.BuyerCustomerId == user.CustomerId || t.SellerCustomerId == user.CustomerId));

            if (transaction == null)
            {
                return NotFound();
            }

            ViewBag.CurrentCustomerId = user.CustomerId;
            ViewBag.IsBuyer = transaction.BuyerCustomerId == user.CustomerId;
            ViewBag.IsSeller = transaction.SellerCustomerId == user.CustomerId;
            
            return View(transaction);
        }

        // POST: CustomerTransactions/ConfirmPayment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.CustomerId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var transaction = await _context.Transactions
                .Include(t => t.BuyerCustomer)
                .Include(t => t.SellerCustomer)
                .Include(t => t.FromCurrency)
                .Include(t => t.ToCurrency)
                .FirstOrDefaultAsync(t => t.Id == id && t.BuyerCustomerId == user.CustomerId);

            if (transaction == null)
            {
                TempData["ErrorMessage"] = "تراکنش مورد نظر یافت نشد یا شما مجاز به انجام این عمل نیستید.";
                return RedirectToAction(nameof(Index));
            }

            if (transaction.Status != TransactionStatus.PaymentUploaded)
            {
                TempData["ErrorMessage"] = "این تراکنش در وضعیت مناسبی برای تأیید پرداخت نیست.";
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                transaction.Status = TransactionStatus.ReceiptConfirmed;
                transaction.CompletedAt = DateTime.Now;
                
                await _context.SaveChangesAsync();

                // Add notification for seller
                var notification = new Notification
                {
                    CustomerId = transaction.SellerCustomerId,
                    Title = "تأیید پرداخت خریدار",
                    Message = $"خریدار تراکنش #{transaction.Id} پرداخت خود را تأیید کرده است. لطفاً نسبت به انتقال ارز اقدام نمایید.",
                    Type = NotificationType.TransactionStatusChanged,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "پرداخت شما با موفقیت تأیید شد. فروشنده اطلاع‌رسانی شده است.";
                _logger.LogInformation($"Customer {user.CustomerId} confirmed payment for transaction {id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error confirming payment for transaction {id} by customer {user.CustomerId}");
                TempData["ErrorMessage"] = "خطایی در تأیید پرداخت رخ داد. لطفاً مجدداً تلاش کنید.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: CustomerTransactions/ConfirmTransfer/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmTransfer(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.CustomerId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var transaction = await _context.Transactions
                .Include(t => t.BuyerCustomer)
                .Include(t => t.SellerCustomer)
                .Include(t => t.FromCurrency)
                .Include(t => t.ToCurrency)
                .FirstOrDefaultAsync(t => t.Id == id && t.SellerCustomerId == user.CustomerId);

            if (transaction == null)
            {
                TempData["ErrorMessage"] = "تراکنش مورد نظر یافت نشد یا شما مجاز به انجام این عمل نیستید.";
                return RedirectToAction(nameof(Index));
            }

            if (transaction.Status != TransactionStatus.ReceiptConfirmed)
            {
                TempData["ErrorMessage"] = "این تراکنش در وضعیت مناسبی برای تأیید انتقال نیست.";
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                transaction.Status = TransactionStatus.Completed;
                transaction.CompletedAt = DateTime.Now;
                
                await _context.SaveChangesAsync();

                // Add notification for buyer
                var notification = new Notification
                {
                    CustomerId = transaction.BuyerCustomerId,
                    Title = "تکمیل تراکنش",
                    Message = $"تراکنش #{transaction.Id} با موفقیت تکمیل شد. ارز مورد نظر به حساب شما منتقل شده است.",
                    Type = NotificationType.TransactionStatusChanged,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "انتقال ارز با موفقیت تأیید شد. تراکنش تکمیل شده است.";
                _logger.LogInformation($"Customer {user.CustomerId} confirmed transfer for transaction {id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error confirming transfer for transaction {id} by customer {user.CustomerId}");
                TempData["ErrorMessage"] = "خطایی در تأیید انتقال رخ داد. لطفاً مجدداً تلاش کنید.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
