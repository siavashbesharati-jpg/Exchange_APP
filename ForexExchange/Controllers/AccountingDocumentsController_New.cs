// TODO: Reimplement as AccountingDocumentsController
// This controller should be replaced with AccountingDocumentsController that handles
// uploading and managing accounting documents (receipts, invoices, payments, etc.)

/*
 * Original ReceiptsController was removed as part of architectural change
 * from Transaction-based system to direct CustomerBalance management.
 * 
 * New AccountingDocumentsController should provide:
 * - Upload accounting documents with OCR processing
 * - Manage document verification and approval
 * - Update customer and bank account balances automatically
 * - Support multiple document types (receipts, invoices, payments, etc.)
 * 
 * Related models to use:
 * - AccountingDocument (replaces Receipt)
 * - CustomerBalance (direct balance management)
 * - BankAccountBalance (bank account balance tracking)
 */
