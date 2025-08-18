namespace ForexExchange.Services
{
    public class OcrResult
    {
        public string RawText { get; set; } = string.Empty;
        public string? Amount { get; set; }
        public string? ReferenceId { get; set; }
        public string? Date { get; set; }
        public string? AccountNumber { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public interface IOcrService
    {
        Task<OcrResult> ProcessReceiptAsync(byte[] imageData);
        Task<OcrResult> ProcessBankStatementAsync(byte[] imageData);
    }
}
