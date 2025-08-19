using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;
using System.Text.Json;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Manager,Staff")]
    public class ReceiptsController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly IOcrService _ocrService;
        private readonly ILogger<ReceiptsController> _logger;

        public ReceiptsController(ForexDbContext context, IOcrService ocrService, ILogger<ReceiptsController> logger)
        {
            _context = context;
            _ocrService = ocrService;
            _logger = logger;
        }

        // GET: Receipts
        public async Task<IActionResult> Index()
        {
            var receipts = await _context.Receipts
                .Include(r => r.Customer)
                .Include(r => r.Order)
                .Include(r => r.Transaction)
                .OrderByDescending(r => r.UploadedAt)
                .ToListAsync();

            return View(receipts);
        }

        // GET: Receipts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var receipt = await _context.Receipts
                .Include(r => r.Customer)
                .Include(r => r.Order)
                .Include(r => r.Transaction)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (receipt == null)
            {
                return NotFound();
            }

            return View(receipt);
        }

        // GET: Receipts/Upload
        public async Task<IActionResult> Upload(int? orderId, int? transactionId)
        {
            ViewBag.Customers = await _context.Customers.Where(c => c.IsActive).ToListAsync();
            ViewBag.OpenOrders = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.Status != OrderStatus.Cancelled)
                .ToListAsync();

            // Flag to indicate if coming from settlement details (transaction-specific upload)
            ViewBag.IsTransactionSpecific = transactionId.HasValue;

            if (orderId.HasValue)
            {
                ViewBag.SelectedOrderId = orderId.Value;
                var order = await _context.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == orderId.Value);
                ViewBag.SelectedOrder = order;
            }

            if (transactionId.HasValue)
            {
                ViewBag.SelectedTransactionId = transactionId.Value;
                var transaction = await _context.Transactions
                    .Include(t => t.BuyerCustomer)
                    .Include(t => t.SellerCustomer)
                    .Include(t => t.BuyOrder)
                    .ThenInclude(o => o.Customer)
                    .Include(t => t.SellOrder)
                    .ThenInclude(o => o.Customer)
                    .FirstOrDefaultAsync(t => t.Id == transactionId.Value);
                
                if (transaction != null)
                {
                    ViewBag.SelectedTransaction = transaction;
                    
                    // Set the customer based on transaction type
                    // For receipt uploads from settlements, we typically use the buyer customer
                    ViewBag.SelectedCustomerId = transaction.BuyerCustomerId;
                    ViewBag.SelectedCustomer = transaction.BuyerCustomer;
                    
                    // Set the buy order as the primary order for the receipt
                    ViewBag.SelectedOrderId = transaction.BuyOrderId;
                    ViewBag.SelectedOrder = transaction.BuyOrder;
                }
            }

            return View();
        }

        // POST: Receipts/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile receiptFile, int customerId, int? orderId, int? transactionId, ReceiptType type, string? manualAmount, string? manualReference, bool skipOcr = false)
        {
            try
            {
                if (receiptFile == null || receiptFile.Length == 0)
                {
                    ModelState.AddModelError("receiptFile", "لطفاً یک فایل تصویر انتخاب کنید.");
                    await LoadUploadViewData(orderId, transactionId);
                    return View();
                }

                // Validate file type
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                if (!allowedTypes.Contains(receiptFile.ContentType.ToLower()))
                {
                    ModelState.AddModelError("receiptFile", "فقط فایل‌های تصویری (JPG, PNG, GIF) مجاز هستند.");
                    await LoadUploadViewData(orderId, transactionId);
                    return View();
                }

                // Convert to byte array
                byte[] imageData;
                using (var memoryStream = new MemoryStream())
                {
                    await receiptFile.CopyToAsync(memoryStream);
                    imageData = memoryStream.ToArray();
                }

                // Create receipt record
                var receipt = new Receipt
                {
                    CustomerId = customerId,
                    OrderId = orderId,
                    TransactionId = transactionId,
                    Type = type,
                    FileName = receiptFile.FileName,
                    ContentType = receiptFile.ContentType,
                    ImageData = imageData,
                    UploadedAt = DateTime.Now,
                    IsVerified = false
                };

                // Process OCR if not skipped
                if (!skipOcr)
                {
                    try
                    {
                        var ocrResult = await _ocrService.ProcessReceiptAsync(imageData);
                        receipt.ExtractedText = ocrResult.RawText;
                        receipt.OcrText = ocrResult.RawText; // For view compatibility
                        receipt.ParsedAmount = ocrResult.Amount;
                        receipt.ParsedReferenceId = ocrResult.ReferenceId;
                        receipt.ParsedDate = ocrResult.Date;
                        receipt.ParsedAccountNumber = ocrResult.AccountNumber;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "OCR processing failed for receipt upload");
                        receipt.Notes = "خطا در پردازش OCR: " + ex.Message;
                    }
                }
                else
                {
                    // Use manual data if OCR is skipped
                    receipt.ParsedAmount = manualAmount;
                    receipt.ParsedReferenceId = manualReference;
                    receipt.Notes = "اطلاعات به صورت دستی وارد شده است.";
                }

                _context.Add(receipt);
                await _context.SaveChangesAsync();

                // Update transaction status when receipt is uploaded
                if (transactionId.HasValue)
                {
                    var transaction = await _context.Transactions.FindAsync(transactionId.Value);
                    if (transaction != null && transaction.Status == TransactionStatus.Pending)
                    {
                        transaction.Status = TransactionStatus.PaymentUploaded;
                        _context.Update(transaction);
                        await _context.SaveChangesAsync();
                    }
                }

                TempData["SuccessMessage"] = "رسید با موفقیت آپلود شد.";
                return RedirectToAction(nameof(Details), new { id = receipt.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading receipt");
                ModelState.AddModelError("", "خطا در آپلود رسید: " + ex.Message);
                await LoadUploadViewData(orderId, transactionId);
                return View();
            }
        }

        // POST: Receipts/Verify/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id, string? verifiedAmount, string? verifiedReference, string? notes)
        {
            var receipt = await _context.Receipts.FindAsync(id);
            if (receipt == null)
            {
                return NotFound();
            }

            receipt.IsVerified = true;
            receipt.VerifiedAt = DateTime.Now;
            receipt.ParsedAmount = verifiedAmount ?? receipt.ParsedAmount;
            receipt.ParsedReferenceId = verifiedReference ?? receipt.ParsedReferenceId;
            receipt.Notes = notes;

            _context.Update(receipt);
            await _context.SaveChangesAsync();

            // Update related transaction status if applicable
            if (receipt.TransactionId.HasValue)
            {
                var transaction = await _context.Transactions.FindAsync(receipt.TransactionId.Value);
                if (transaction != null && transaction.Status == TransactionStatus.PaymentUploaded)
                {
                    // Keep transaction in PaymentUploaded status until explicit confirmation
                    // Don't auto-progress to ReceiptConfirmed here
                    _logger.LogInformation($"Receipt {id} verified for transaction {transaction.Id}. Awaiting payment confirmation.");
                }
            }

            TempData["SuccessMessage"] = "رسید تأیید شد.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Receipts/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var receipt = await _context.Receipts
                .Include(r => r.Customer)
                .Include(r => r.Order)
                .Include(r => r.Transaction)
                .FirstOrDefaultAsync(r => r.Id == id);
            
            if (receipt == null)
            {
                return NotFound();
            }

            ViewBag.Customers = await _context.Customers.Where(c => c.IsActive).ToListAsync();
            ViewBag.Orders = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.Status != OrderStatus.Cancelled)
                .ToListAsync();
            ViewBag.Transactions = await _context.Transactions
                .Include(t => t.BuyerCustomer)
                .Include(t => t.SellerCustomer)
                .ToListAsync();

            return View(receipt);
        }

        // POST: Receipts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Receipt receipt, IFormFile? newReceiptFile)
        {
            if (id != receipt.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingReceipt = await _context.Receipts.FindAsync(id);
                    if (existingReceipt == null)
                    {
                        return NotFound();
                    }

                    // Update editable fields
                    existingReceipt.CustomerId = receipt.CustomerId;
                    existingReceipt.OrderId = receipt.OrderId;
                    existingReceipt.TransactionId = receipt.TransactionId;
                    existingReceipt.Type = receipt.Type;
                    existingReceipt.ParsedAmount = receipt.ParsedAmount;
                    existingReceipt.ParsedReferenceId = receipt.ParsedReferenceId;
                    existingReceipt.ParsedDate = receipt.ParsedDate;
                    existingReceipt.ParsedAccountNumber = receipt.ParsedAccountNumber;
                    existingReceipt.Notes = receipt.Notes;

                    // Handle new file upload if provided
                    if (newReceiptFile != null && newReceiptFile.Length > 0)
                    {
                        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                        if (allowedTypes.Contains(newReceiptFile.ContentType.ToLower()))
                        {
                            using (var memoryStream = new MemoryStream())
                            {
                                await newReceiptFile.CopyToAsync(memoryStream);
                                existingReceipt.ImageData = memoryStream.ToArray();
                                existingReceipt.FileName = newReceiptFile.FileName;
                                existingReceipt.ContentType = newReceiptFile.ContentType;
                            }
                        }
                    }

                    _context.Update(existingReceipt);
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = "رسید با موفقیت بروزرسانی شد.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReceiptExists(receipt.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            ViewBag.Customers = await _context.Customers.Where(c => c.IsActive).ToListAsync();
            ViewBag.Orders = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.Status != OrderStatus.Cancelled)
                .ToListAsync();
            ViewBag.Transactions = await _context.Transactions
                .Include(t => t.BuyerCustomer)
                .Include(t => t.SellerCustomer)
                .ToListAsync();

            return View(receipt);
        }

        // GET: Receipts/Image/5
        public async Task<IActionResult> Image(int id)
        {
            var receipt = await _context.Receipts.FindAsync(id);
            if (receipt == null || receipt.ImageData == null)
            {
                return NotFound();
            }

            return File(receipt.ImageData, receipt.ContentType);
        }

        // POST: Receipts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var receipt = await _context.Receipts.FindAsync(id);
            if (receipt != null)
            {
                _context.Receipts.Remove(receipt);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "رسید حذف شد.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadUploadViewData(int? orderId, int? transactionId)
        {
            ViewBag.Customers = await _context.Customers.Where(c => c.IsActive).ToListAsync();
            ViewBag.OpenOrders = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.Status != OrderStatus.Cancelled)
                .ToListAsync();

            // Flag to indicate if coming from settlement details (transaction-specific upload)
            ViewBag.IsTransactionSpecific = transactionId.HasValue;

            if (orderId.HasValue)
            {
                ViewBag.SelectedOrderId = orderId.Value;
                var order = await _context.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == orderId.Value);
                ViewBag.SelectedOrder = order;
            }

            if (transactionId.HasValue)
            {
                ViewBag.SelectedTransactionId = transactionId.Value;
                var transaction = await _context.Transactions
                    .Include(t => t.BuyerCustomer)
                    .Include(t => t.SellerCustomer)
                    .Include(t => t.BuyOrder)
                    .ThenInclude(o => o.Customer)
                    .Include(t => t.SellOrder)
                    .ThenInclude(o => o.Customer)
                    .FirstOrDefaultAsync(t => t.Id == transactionId.Value);
                
                if (transaction != null)
                {
                    ViewBag.SelectedTransaction = transaction;
                    
                    // Set the customer based on transaction type
                    // For receipt uploads from settlements, we typically use the buyer customer
                    ViewBag.SelectedCustomerId = transaction.BuyerCustomerId;
                    ViewBag.SelectedCustomer = transaction.BuyerCustomer;
                    
                    // Set the buy order as the primary order for the receipt
                    ViewBag.SelectedOrderId = transaction.BuyOrderId;
                    ViewBag.SelectedOrder = transaction.BuyOrder;
                }
            }
        }

        private bool ReceiptExists(int id)
        {
            return _context.Receipts.Any(e => e.Id == id);
        }
    }
}
