namespace ForexExchange.Models
{
    public class CustomerProfileStats
    {
        public int TotalOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int PendingOrders { get; set; }
        public int TotalTransactions { get; set; }
        public int CompletedTransactions { get; set; }
        public int TotalAccountingDocuments { get; set; }
        public int VerifiedAccountingDocuments { get; set; }
        public decimal TotalVolumeInToman { get; set; }
        public int RegistrationDays { get; set; }
        
        // Calculated properties
        public double CompletionRate => TotalOrders > 0 ? (double)CompletedOrders / TotalOrders * 100 : 0;
        public double VerificationRate => TotalAccountingDocuments > 0 ? (double)VerifiedAccountingDocuments / TotalAccountingDocuments * 100 : 0;
        public decimal AverageOrderValue => CompletedOrders > 0 ? TotalVolumeInToman / CompletedOrders : 0;
    }

    public class CustomerActivitySummary
    {
        public DateTime Date { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? ReferenceId { get; set; }
        public string ReferenceType { get; set; } = string.Empty;
    }
}
