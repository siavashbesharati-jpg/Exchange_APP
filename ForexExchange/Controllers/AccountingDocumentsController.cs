
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using Microsoft.AspNetCore.Authorization;
using ForexExchange.Services;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Services.Notifications;

namespace ForexExchange.Controllers
{
    [Authorize]
    public class AccountingDocumentsController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ICustomerBalanceService _customerBalanceService;
        private readonly IBankAccountBalanceService _bankAccountBalanceService;
        private readonly IOcrService _ocrService;
        private readonly AdminNotificationService _adminNotificationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationHub _notificationHub;
        private readonly ICentralFinancialService _centralFinancialService;
        private readonly ILogger<AccountingDocumentsController> _logger;

        public AccountingDocumentsController(
            ForexDbContext context,
            ICustomerBalanceService customerBalanceService,
            IBankAccountBalanceService bankAccountBalanceService,
            IOcrService ocrService,
            AdminNotificationService adminNotificationService,
            UserManager<ApplicationUser> userManager,
            INotificationHub notificationHub,
            ICentralFinancialService centralFinancialService,
            ILogger<AccountingDocumentsController> logger)
        {
            _context = context;
            _customerBalanceService = customerBalanceService;
            _bankAccountBalanceService = bankAccountBalanceService;
            _ocrService = ocrService;
            _adminNotificationService = adminNotificationService;
            _userManager = userManager;
            _notificationHub = notificationHub;
            _centralFinancialService = centralFinancialService;
            _logger = logger;
        }

        // GET: AccountingDocuments
        public async Task<IActionResult> Index(string sortOrder, string currentFilter, string searchString, int? customerFilter, DocumentType? typeFilter, bool? statusFilter)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["IdSortParm"] = String.IsNullOrEmpty(sortOrder) ? "id_desc" : "";
            ViewData["TitleSortParm"] = sortOrder == "title" ? "title_desc" : "title";
            ViewData["TypeSortParm"] = sortOrder == "type" ? "type_desc" : "type";
            ViewData["CustomerSortParm"] = sortOrder == "customer" ? "customer_desc" : "customer";
            ViewData["AmountSortParm"] = sortOrder == "amount" ? "amount_desc" : "amount";
            ViewData["DateSortParm"] = sortOrder == "date" ? "date_desc" : "date";

            if (searchString != null)
            {
                currentFilter = searchString;
            }
            else
            {
                searchString = currentFilter;
            }

            ViewData["CurrentFilter"] = searchString;
            ViewData["CustomerFilter"] = customerFilter;
            ViewData["TypeFilter"] = typeFilter;
            ViewData["StatusFilter"] = statusFilter;

            var documents = from d in _context.AccountingDocuments
                           .Include(d => d.PayerCustomer)
                           .Include(d => d.ReceiverCustomer)
                           .Include(d => d.PayerBankAccount)
                           .Include(d => d.ReceiverBankAccount)
                           select d;

            // Apply filters
            if (!String.IsNullOrEmpty(searchString))
            {
                // Search by document ID
                if (int.TryParse(searchString, out int documentId))
                {
                    documents = documents.Where(d => d.Id == documentId);
                }
                else
                {
                    // If not a valid integer, return no results
                    documents = documents.Where(d => false);
                }
            }

            if (customerFilter.HasValue)
            {
                documents = documents.Where(d => d.PayerCustomerId == customerFilter || d.ReceiverCustomerId == customerFilter);
            }

            if (typeFilter.HasValue)
            {
                documents = documents.Where(d => d.Type == typeFilter);
            }

            if (statusFilter.HasValue)
            {
                documents = documents.Where(d => d.IsVerified == statusFilter);
            }

            // Apply sorting
            switch (sortOrder)
            {
                case "id_desc":
                    documents = documents.OrderByDescending(d => d.Id);
                    break;
                case "title":
                    documents = documents.OrderBy(d => d.Title);
                    break;
                case "title_desc":
                    documents = documents.OrderByDescending(d => d.Title);
                    break;
                case "type":
                    documents = documents.OrderBy(d => d.Type);
                    break;
                case "type_desc":
                    documents = documents.OrderByDescending(d => d.Type);
                    break;
                case "customer":
                    documents = documents.OrderBy(d => d.Customer != null ? d.Customer.FullName : "");
                    break;
                case "customer_desc":
                    documents = documents.OrderByDescending(d => d.Customer != null ? d.Customer.FullName : "");
                    break;
                case "amount":
                    documents = documents.OrderBy(d => d.Amount);
                    break;
                case "amount_desc":
                    documents = documents.OrderByDescending(d => d.Amount);
                    break;
                case "date":
                    documents = documents.OrderBy(d => d.DocumentDate);
                    break;
                case "date_desc":
                    documents = documents.OrderByDescending(d => d.DocumentDate);
                    break;
                default:
                    documents = documents.OrderByDescending(d => d.CreatedAt);
                    break;
            }

            return View(await documents.ToListAsync());
        }

        // GET: AccountingDocuments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var accountingDocument = await _context.AccountingDocuments
                .Include(a => a.PayerCustomer)
                .Include(a => a.ReceiverCustomer)
                .Include(a => a.PayerBankAccount)
                .Include(a => a.ReceiverBankAccount)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (accountingDocument == null)
            {
                return NotFound();
            }

            return View(accountingDocument);
        }

        // GET: AccountingDocuments/CustomerStatement/5
        public async Task<IActionResult> CustomerStatement(int? customerId, int? documentId = null)
        {
            if (customerId == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == customerId);

            if (customer == null)
            {
                return NotFound();
            }

            // Get all accounting documents for this customer
            var documents = await _context.AccountingDocuments
                .Include(a => a.PayerCustomer)
                .Include(a => a.ReceiverCustomer)
                .Include(a => a.PayerBankAccount)
                .Include(a => a.ReceiverBankAccount)
                .Where(a => a.PayerCustomerId == customerId || a.ReceiverCustomerId == customerId)
                .OrderByDescending(a => a.DocumentDate)
                .ToListAsync();

            // Get customer balance
            var balances = await _customerBalanceService.GetCustomerBalancesAsync(customerId.Value);

            var viewModel = new CustomerStatementViewModel
            {
                Customer = customer,
                Documents = documents,
                Balances = balances,
                StatementDate = DateTime.Now
            };

            // Pass document ID for back navigation
            if (documentId.HasValue)
            {
                ViewBag.DocumentId = documentId.Value;
            }

            return View(viewModel);
        }

        // GET: AccountingDocuments/Upload
        public IActionResult Upload()
        {
            ViewData["Customers"] = _context.Customers.Where(c => c.IsActive && c.IsSystem == false ).ToList();
            ViewData["Currencies"] = _context.Currencies.Where(c => c.IsActive).ToList();
            ViewData["BankAccounts"] = _context.BankAccounts.ToList();
            return View();
        }

        // POST: AccountingDocuments/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(AccountingDocument accountingDocument, IFormFile documentFile)
        {
            // Remove validation error for documentFile since it's optional
            if (ModelState.ContainsKey("documentFile"))
            {
                ModelState.Remove("documentFile");
            }

            // File is now optional, but if provided, validate it
            if (documentFile != null && documentFile.Length > 0)
            {
                // Validate file size (max 10MB)
                if (documentFile.Length > 10 * 1024 * 1024)
                {
                    ModelState.AddModelError("documentFile", "حجم فایل نمی‌تواند بیشتر از 10 مگابایت باشد.");
                    TempData["ErrorMessage"] = "حجم فایل نمی‌تواند بیشتر از 10 مگابایت باشد.";
                    ViewData["Customers"] = _context.Customers.Where(c => c.IsActive && c.IsSystem == false).ToList();
                    ViewData["Currencies"] = _context.Currencies.Where(c => c.IsActive).ToList();
                    ViewData["BankAccounts"] = _context.BankAccounts.ToList();
                    return View(accountingDocument);
                }

                // Validate file type
                var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
                var fileExtension = Path.GetExtension(documentFile.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("documentFile", "فرمت فایل مجاز نیست. فرمت‌های مجاز: PDF, JPG, PNG, DOC, DOCX");
                    TempData["ErrorMessage"] = "فرمت فایل مجاز نیست. فرمت‌های مجاز: PDF, JPG, PNG, DOC, DOCX";
                    ViewData["Customers"] = _context.Customers.Where(c => c.IsActive && c.IsSystem == false).ToList();
                    ViewData["Currencies"] = _context.Currencies.Where(c => c.IsActive).ToList();
                    ViewData["BankAccounts"] = _context.BankAccounts.ToList();
                    return View(accountingDocument);
                }
            }

            // Validate bank account currency match
            // Check payer bank account
            if (accountingDocument.PayerBankAccountId.HasValue)
            {
                var payerBankAccount = await _context.BankAccounts.FindAsync(accountingDocument.PayerBankAccountId.Value);
                if (payerBankAccount != null && payerBankAccount.CurrencyCode != accountingDocument.CurrencyCode)
                {
                    ModelState.AddModelError("PayerBankAccountId", $"ارز حساب بانکی پرداخت کننده ({payerBankAccount.CurrencyCode}) با ارز سند ({accountingDocument.CurrencyCode}) مطابقت ندارد.");
                }
            }

            // Check receiver bank account
            if (accountingDocument.ReceiverBankAccountId.HasValue)
            {
                var receiverBankAccount = await _context.BankAccounts.FindAsync(accountingDocument.ReceiverBankAccountId.Value);
                if (receiverBankAccount != null && receiverBankAccount.CurrencyCode != accountingDocument.CurrencyCode)
                {
                    ModelState.AddModelError("ReceiverBankAccountId", $"ارز حساب بانکی دریافت کننده ({receiverBankAccount.CurrencyCode}) با ارز سند ({accountingDocument.CurrencyCode}) مطابقت ندارد.");
                }
            }

            if (ModelState.IsValid)
            {
                accountingDocument.CreatedAt = DateTime.Now;

                // Handle file upload only if a file is provided
                if (documentFile != null && documentFile.Length > 0)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await documentFile.CopyToAsync(memoryStream);
                        accountingDocument.FileData = memoryStream.ToArray();
                        accountingDocument.FileName = documentFile.FileName;
                        accountingDocument.ContentType = documentFile.ContentType;
                    }
                }

                _context.Add(accountingDocument);
                await _context.SaveChangesAsync();
                
                // Send notifications through central hub
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    await _notificationHub.SendAccountingDocumentNotificationAsync(accountingDocument, NotificationEventType.AccountingDocumentCreated, currentUser.Id);
                }
                
                TempData["SuccessMessage"] = "سند حسابداری با موفقیت ثبت شد.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["Customers"] = _context.Customers.Where(c => c.IsActive && c.IsSystem == false).ToList();
            ViewData["Currencies"] = _context.Currencies.Where(c => c.IsActive).ToList();
            ViewData["BankAccounts"] = _context.BankAccounts.ToList();
            return View(accountingDocument);
        }

        // GET: AccountingDocuments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var accountingDocument = await _context.AccountingDocuments.FindAsync(id);
            if (accountingDocument == null)
            {
                return NotFound();
            }

            ViewData["Customers"] = _context.Customers.Where(c => c.IsActive).ToList();
            ViewData["Currencies"] = _context.Currencies.Where(c => c.IsActive).ToList();
            ViewData["BankAccounts"] = _context.BankAccounts.ToList();
            return View(accountingDocument);
        }

        // POST: AccountingDocuments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AccountingDocument accountingDocument, IFormFile documentFile)
        {
            if (id != accountingDocument.Id)
            {
                return NotFound();
            }

            // Remove validation error for documentFile since it's optional
            if (ModelState.ContainsKey("documentFile"))
            {
                ModelState.Remove("documentFile");
            }

            // Validate bank account currency match
            // Check payer bank account
            if (accountingDocument.PayerBankAccountId.HasValue)
            {
                var payerBankAccount = await _context.BankAccounts.FindAsync(accountingDocument.PayerBankAccountId.Value);
                if (payerBankAccount != null && payerBankAccount.CurrencyCode != accountingDocument.CurrencyCode)
                {
                    ModelState.AddModelError("PayerBankAccountId", $"ارز حساب بانکی پرداخت کننده ({payerBankAccount.CurrencyCode}) با ارز سند ({accountingDocument.CurrencyCode}) مطابقت ندارد.");
                }
            }

            // Check receiver bank account
            if (accountingDocument.ReceiverBankAccountId.HasValue)
            {
                var receiverBankAccount = await _context.BankAccounts.FindAsync(accountingDocument.ReceiverBankAccountId.Value);
                if (receiverBankAccount != null && receiverBankAccount.CurrencyCode != accountingDocument.CurrencyCode)
                {
                    ModelState.AddModelError("ReceiverBankAccountId", $"ارز حساب بانکی دریافت کننده ({receiverBankAccount.CurrencyCode}) با ارز سند ({accountingDocument.CurrencyCode}) مطابقت ندارد.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Get the existing document to check for verification status changes
                    var existingDocument = await _context.AccountingDocuments
                        .AsNoTracking()
                        .FirstOrDefaultAsync(d => d.Id == id);

                    if (existingDocument == null)
                    {
                        return NotFound();
                    }

                    // Handle file upload if a new file is provided
                    if (documentFile != null && documentFile.Length > 0)
                    {
                        // Validate file size (10MB max)
                        if (documentFile.Length > 10 * 1024 * 1024)
                        {
                            ModelState.AddModelError("documentFile", "حجم فایل نمی‌تواند بیشتر از ۱۰ مگابایت باشد.");
                            ViewData["Customers"] = _context.Customers.Where(c => c.IsActive).ToList();
                            ViewData["Currencies"] = _context.Currencies.Where(c => c.IsActive).ToList();
                            ViewData["BankAccounts"] = _context.BankAccounts.ToList();
                            return View(accountingDocument);
                        }

                        // Validate file type
                        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
                        var fileExtension = Path.GetExtension(documentFile.FileName).ToLower();
                        if (!allowedExtensions.Contains(fileExtension))
                        {
                            ModelState.AddModelError("documentFile", "فرمت فایل مجاز نیست. فرمت‌های مجاز: PDF, JPG, PNG, DOC, DOCX");
                            ViewData["Customers"] = _context.Customers.Where(c => c.IsActive).ToList();
                            ViewData["Currencies"] = _context.Currencies.Where(c => c.IsActive).ToList();
                            ViewData["BankAccounts"] = _context.BankAccounts.ToList();
                            return View(accountingDocument);
                        }

                        // Read file data into byte array
                        using (var memoryStream = new MemoryStream())
                        {
                            await documentFile.CopyToAsync(memoryStream);
                            accountingDocument.FileData = memoryStream.ToArray();
                        }

                        // Update document properties
                        accountingDocument.FileName = documentFile.FileName;
                        accountingDocument.ContentType = documentFile.ContentType;
                    }

                    // Handle verification status changes
                    if (accountingDocument.IsVerified != existingDocument.IsVerified)
                    {
                        if (accountingDocument.IsVerified)
                        {
                            // Document is being verified
                            accountingDocument.VerifiedAt = DateTime.Now;
                            accountingDocument.VerifiedBy = User.Identity?.Name ?? "System";

                            // Update balances through centralized service (includes history recording)
                            await _centralFinancialService.ProcessAccountingDocumentAsync(accountingDocument);
                            // Note: Currency pools are NOT updated on document verification
                            // Currency pools are only affected by actual currency trading operations
                        }
                        else
                        {
                            // Document is being un-verified (admin action)
                            // Note: In a real system, you might want to reverse the balance changes
                            // For now, we'll just update the status
                            accountingDocument.VerifiedAt = null;
                            accountingDocument.VerifiedBy = null;
                        }
                    }

                    _context.Update(accountingDocument);
                    await _context.SaveChangesAsync();
                    
                    // Send appropriate notification based on what changed
                    if (accountingDocument.IsVerified && !existingDocument.IsVerified)
                    {
                        // Document was just confirmed
                        await _adminNotificationService.SendDocumentNotificationAsync(accountingDocument, "confirmed");
                    }
                    else
                    {
                        // Document was updated
                        await _adminNotificationService.SendDocumentNotificationAsync(accountingDocument, "updated");
                    }
                    
                    TempData["SuccessMessage"] = "سند حسابداری با موفقیت ویرایش شد.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AccountingDocumentExists(accountingDocument.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewData["Customers"] = _context.Customers.Where(c => c.IsActive).ToList();
            ViewData["Currencies"] = _context.Currencies.Where(c => c.IsActive).ToList();
            ViewData["BankAccounts"] = _context.BankAccounts.ToList();
            return View(accountingDocument);
        }

        // POST: AccountingDocuments/Confirm/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int id)
        {
            try
            {
                var accountingDocument = await _context.AccountingDocuments
                    .Include(a => a.PayerCustomer)
                    .Include(a => a.ReceiverCustomer)
                    .Include(a => a.PayerBankAccount)
                    .Include(a => a.ReceiverBankAccount)
                    .FirstOrDefaultAsync(a => a.Id == id);
                    
                if (accountingDocument == null)
                {
                    TempData["ErrorMessage"] = "سند حسابداری یافت نشد.";
                    return RedirectToAction(nameof(Index));
                }

                // Validate bank account currency match
                // Check payer bank account
                if (accountingDocument.PayerBankAccountId.HasValue && accountingDocument.PayerBankAccount != null)
                {
                    if (accountingDocument.PayerBankAccount.CurrencyCode != accountingDocument.CurrencyCode)
                    {
                        TempData["ErrorMessage"] = $"ارز حساب بانکی پرداخت کننده ({accountingDocument.PayerBankAccount.CurrencyCode}) با ارز سند ({accountingDocument.CurrencyCode}) مطابقت ندارد.";
                        return RedirectToAction("Details", new { id });
                    }
                }

                // Check receiver bank account
                if (accountingDocument.ReceiverBankAccountId.HasValue && accountingDocument.ReceiverBankAccount != null)
                {
                    if (accountingDocument.ReceiverBankAccount.CurrencyCode != accountingDocument.CurrencyCode)
                    {
                        TempData["ErrorMessage"] = $"ارز حساب بانکی دریافت کننده ({accountingDocument.ReceiverBankAccount.CurrencyCode}) با ارز سند ({accountingDocument.CurrencyCode}) مطابقت ندارد.";
                        return RedirectToAction("Details", new { id });
                    }
                }

                // Only process if not already verified
                if (!accountingDocument.IsVerified)
                {
                    accountingDocument.IsVerified = true;
                    accountingDocument.VerifiedAt = DateTime.Now;
                    accountingDocument.VerifiedBy = User.Identity?.Name ?? "System";

                    // Update balances through centralized service (includes history recording)
                    // This will now use the CORRECTED logic: Payer = +amount, Receiver = -amount
                    await _centralFinancialService.ProcessAccountingDocumentAsync(accountingDocument, User.Identity?.Name ?? "System");
                 
                    _context.Update(accountingDocument);
                    await _context.SaveChangesAsync();

                    // Send notifications through central hub
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser != null)
                    {
                        await _notificationHub.SendAccountingDocumentNotificationAsync(accountingDocument, NotificationEventType.AccountingDocumentVerified, currentUser.Id);
                    }

                    TempData["SuccessMessage"] = "سند حسابداری با موفقیت تأیید شد و ترازها بروزرسانی گردید.";
                }
                else
                {
                    TempData["InfoMessage"] = "این سند قبلاً تأیید شده است.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"خطا در تأیید سند: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: AccountingDocuments/ConfirmAll
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ConfirmAll()
        {
            try
            {
                var unverifiedDocuments = await _context.AccountingDocuments
                    .Include(a => a.PayerCustomer)
                    .Include(a => a.ReceiverCustomer)
                    .Include(a => a.PayerBankAccount)
                    .Include(a => a.ReceiverBankAccount)
                    .Where(a => !a.IsVerified)
                    .OrderBy(a => a.DocumentDate)
                    .ToListAsync();

                if (unverifiedDocuments.Count == 0)
                {
                    TempData["InfoMessage"] = "هیچ سند تایید نشده‌ای یافت نشد.";
                    return RedirectToAction(nameof(Index));
                }

                var confirmationLog = new List<string>();
                var successCount = 0;
                var errorCount = 0;

                using var transaction = await _context.Database.BeginTransactionAsync();

                foreach (var document in unverifiedDocuments)
                {
                    try
                    {
                        // Validate bank account currency match
                        bool hasError = false;
                        
                        if (document.PayerBankAccountId.HasValue && document.PayerBankAccount != null)
                        {
                            if (document.PayerBankAccount.CurrencyCode != document.CurrencyCode)
                            {
                                confirmationLog.Add($"❌ Document {document.Id}: Currency mismatch for payer bank account");
                                errorCount++;
                                hasError = true;
                            }
                        }

                        if (document.ReceiverBankAccountId.HasValue && document.ReceiverBankAccount != null)
                        {
                            if (document.ReceiverBankAccount.CurrencyCode != document.CurrencyCode)
                            {
                                confirmationLog.Add($"❌ Document {document.Id}: Currency mismatch for receiver bank account");
                                errorCount++;
                                hasError = true;
                            }
                        }

                        if (!hasError)
                        {
                            document.IsVerified = true;
                            document.VerifiedAt = DateTime.Now;
                            document.VerifiedBy = User.Identity?.Name ?? "System";

                            // Update balances through centralized service with CORRECTED logic
                            await _centralFinancialService.ProcessAccountingDocumentAsync(document, User.Identity?.Name ?? "System");
                            
                            _context.Update(document);
                            
                            confirmationLog.Add($"✅ Document {document.Id}: Confirmed successfully ({document.Amount:N2} {document.CurrencyCode})");
                            confirmationLog.Add($"   - Payer: Customer {document.PayerCustomerId} gets +{document.Amount}");
                            confirmationLog.Add($"   - Receiver: Customer {document.ReceiverCustomerId} gets -{document.Amount}");
                            successCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        confirmationLog.Add($"❌ Document {document.Id}: Error - {ex.Message}");
                        errorCount++;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var summary = new[]
                {
                    $"✅ تعداد اسناد تایید شده: {successCount}",
                    $"❌ تعداد اسناد با خطا: {errorCount}",
                    $"📄 کل اسناد پردازش شده: {unverifiedDocuments.Count}",
                    "",
                    "✅ همه اسناد با منطق صحیح پردازش شدند: پرداخت کننده = +مبلغ، دریافت کننده = -مبلغ"
                };

                TempData["Success"] = string.Join("<br/>", summary);
                TempData["ConfirmationLog"] = string.Join("\n", confirmationLog);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"خطا در تایید همه اسناد: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: AccountingDocuments/GetFile/5
        public async Task<IActionResult> GetFile(int id)
        {
            var document = await _context.AccountingDocuments
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null || document.FileData == null || string.IsNullOrEmpty(document.FileName))
            {
                return NotFound();
            }

            return File(document.FileData, document.ContentType ?? "application/octet-stream", document.FileName);
        }

        // POST: AccountingDocuments/ProcessOcr
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessOcr(IFormFile imageFile)
        {
            try
            {
                if (imageFile == null || imageFile.Length == 0)
                {
                    return Json(new { success = false, message = "فایل انتخاب نشده است." });
                }

                // Validate file type (only images)
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/bmp", "image/gif" };
                if (!allowedTypes.Contains(imageFile.ContentType.ToLower()))
                {
                    return Json(new { success = false, message = "فقط فایل‌های تصویری پشتیبانی می‌شوند." });
                }

                // Validate file size (max 10MB)
                if (imageFile.Length > 10 * 1024 * 1024)
                {
                    return Json(new { success = false, message = "حجم فایل نمی‌تواند بیشتر از 10 مگابایت باشد." });
                }

                // Convert to byte array
                byte[] imageData;
                using (var memoryStream = new MemoryStream())
                {
                    await imageFile.CopyToAsync(memoryStream);
                    imageData = memoryStream.ToArray();
                }

                // Process with OCR
                var ocrResult = await _ocrService.ProcessAccountingDocumentAsync(imageData);

                if (ocrResult.Success)
                {
                    return Json(new
                    {
                        success = true,
                        message = "OCR با موفقیت انجام شد.",
                        data = new
                        {
                            rawText = ocrResult.RawText,
                            amount = ocrResult.Amount,
                            referenceId = ocrResult.ReferenceId,
                            date = ocrResult.Date,
                            accountNumber = ocrResult.AccountNumber
                        }
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = ocrResult.ErrorMessage ?? "خطا در پردازش OCR"
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "خطای داخلی سرور: " + ex.Message
                });
            }
        }

        // POST: AccountingDocuments/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")] // Only admins can delete documents
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var document = await _context.AccountingDocuments
                    .Include(a => a.PayerCustomer)
                    .Include(a => a.ReceiverCustomer)
                    .Include(a => a.PayerBankAccount)
                    .Include(a => a.ReceiverBankAccount)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (document == null)
                {
                    TempData["ErrorMessage"] = "سند حسابداری یافت نشد.";
                    return RedirectToAction(nameof(Index));
                }

                // Use centralized service to delete with proper financial impact reversal
                var currentUser = await _userManager.GetUserAsync(User);
                await _centralFinancialService.DeleteAccountingDocumentAsync(document, currentUser?.UserName ?? "Admin");

                // Log admin activity
                var adminActivity = new AdminActivity
                {
                    AdminUserId = currentUser?.Id ?? "Unknown",
                    ActivityType = AdminActivityType.UserDeleted, // Using as closest match for document deletion
                    Description = $"Deleted Accounting Document #{document.Id} - {document.Title}",
                    Timestamp = DateTime.UtcNow,
                    IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString()
                };
                _context.AdminActivities.Add(adminActivity);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"سند حسابداری #{document.Id} با موفقیت حذف شد و تأثیرات مالی آن برگردانده شد.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting accounting document {id}");
                TempData["ErrorMessage"] = "خطا در حذف سند حسابداری. لطفاً دوباره تلاش کنید.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool AccountingDocumentExists(int id)
        {
            return _context.AccountingDocuments.Any(e => e.Id == id);
        }
    }
}
