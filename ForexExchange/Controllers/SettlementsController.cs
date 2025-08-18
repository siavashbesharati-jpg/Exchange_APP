using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;

namespace ForexExchange.Controllers
{
    public class SettlementsController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ITransactionSettlementService _settlementService;
        private readonly ILogger<SettlementsController> _logger;

        public SettlementsController(
            ForexDbContext context,
            ITransactionSettlementService settlementService,
            ILogger<SettlementsController> logger)
        {
            _context = context;
            _settlementService = settlementService;
            _logger = logger;
        }

        // GET: Settlements
        public async Task<IActionResult> Index()
        {
            var pendingSettlements = await _settlementService.GetPendingSettlementsAsync();
            var allTransactions = await _context.Transactions
                .Include(t => t.BuyerCustomer)
                .Include(t => t.SellerCustomer)
                .Include(t => t.BuyOrder)
                .Include(t => t.SellOrder)
                .Include(t => t.Receipts)
                .OrderByDescending(t => t.CreatedAt)
                .Take(50)
                .ToListAsync();

            ViewBag.PendingSettlements = pendingSettlements;
            return View(allTransactions);
        }

        // GET: Settlements/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var transaction = await _context.Transactions
                .Include(t => t.BuyerCustomer)
                .Include(t => t.SellerCustomer)
                .Include(t => t.BuyOrder)
                .Include(t => t.SellOrder)
                .Include(t => t.Receipts.Where(r => r.IsVerified))
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null)
            {
                return NotFound();
            }

            // Calculate settlement details
            var calculation = await _settlementService.CalculateSettlementAsync(transaction);
            ViewBag.SettlementCalculation = calculation;

            return View(transaction);
        }

        // POST: Settlements/Initiate/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Initiate(int id)
        {
            try
            {
                var success = await _settlementService.InitiateSettlementAsync(id);
                if (success)
                {
                    TempData["SuccessMessage"] = "فرآیند تسویه با موفقیت آغاز شد.";
                }
                else
                {
                    TempData["ErrorMessage"] = "خطا در آغاز فرآیند تسویه.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating settlement for transaction {Id}", id);
                TempData["ErrorMessage"] = "خطا در آغاز فرآیند تسویه: " + ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Settlements/ConfirmBuyerPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmBuyerPayment(int transactionId, int receiptId)
        {
            try
            {
                var success = await _settlementService.ConfirmBuyerPaymentAsync(transactionId, receiptId);
                if (success)
                {
                    TempData["SuccessMessage"] = "پرداخت خریدار تأیید شد.";
                }
                else
                {
                    TempData["ErrorMessage"] = "خطا در تأیید پرداخت خریدار.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming buyer payment for transaction {TransactionId}", transactionId);
                TempData["ErrorMessage"] = "خطا در تأیید پرداخت: " + ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = transactionId });
        }

        // POST: Settlements/ConfirmSellerPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmSellerPayment(int transactionId, string bankReference, string notes)
        {
            try
            {
                var success = await _settlementService.ConfirmSellerPaymentAsync(transactionId, bankReference);
                if (success)
                {
                    // Update transaction notes if provided
                    if (!string.IsNullOrEmpty(notes))
                    {
                        var transaction = await _context.Transactions.FindAsync(transactionId);
                        if (transaction != null)
                        {
                            transaction.Notes += $" | {notes}";
                            await _context.SaveChangesAsync();
                        }
                    }

                    TempData["SuccessMessage"] = "انتقال فروشنده تأیید شد.";
                }
                else
                {
                    TempData["ErrorMessage"] = "خطا در تأیید انتقال فروشنده.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming seller payment for transaction {TransactionId}", transactionId);
                TempData["ErrorMessage"] = "خطا در تأیید انتقال: " + ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = transactionId });
        }

        // POST: Settlements/Complete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            try
            {
                var success = await _settlementService.CompleteTransactionAsync(id);
                if (success)
                {
                    TempData["SuccessMessage"] = "تراکنش با موفقیت تکمیل شد.";
                }
                else
                {
                    TempData["ErrorMessage"] = "خطا در تکمیل تراکنش.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing transaction {Id}", id);
                TempData["ErrorMessage"] = "خطا در تکمیل تراکنش: " + ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Settlements/Fail
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Fail(int transactionId, string reason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    TempData["ErrorMessage"] = "لطفاً دلیل ناموفق بودن تراکنش را وارد کنید.";
                    return RedirectToAction(nameof(Details), new { id = transactionId });
                }

                var success = await _settlementService.FailTransactionAsync(transactionId, reason);
                if (success)
                {
                    TempData["SuccessMessage"] = "تراکنش به عنوان ناموفق ثبت شد.";
                }
                else
                {
                    TempData["ErrorMessage"] = "خطا در ثبت ناموفق بودن تراکنش.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error failing transaction {TransactionId}", transactionId);
                TempData["ErrorMessage"] = "خطا در ثبت ناموفق بودن تراکنش: " + ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = transactionId });
        }

        // GET: Settlements/Queue
        public async Task<IActionResult> Queue()
        {
            var pendingSettlements = await _settlementService.GetPendingSettlementsAsync();
            return View(pendingSettlements);
        }

        // API endpoint for getting settlement calculation
        [HttpGet]
        public async Task<IActionResult> GetCalculation(int transactionId)
        {
            try
            {
                var transaction = await _context.Transactions.FindAsync(transactionId);
                if (transaction == null)
                {
                    return NotFound();
                }

                var calculation = await _settlementService.CalculateSettlementAsync(transaction);
                return Json(calculation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting settlement calculation for transaction {TransactionId}", transactionId);
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
