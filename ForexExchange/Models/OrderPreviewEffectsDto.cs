namespace ForexExchange.Models
{
    // DTO for previewing order effects
    public class OrderPreviewEffectsDto
    {
        public int CustomerId { get; set; }
        public string FromCurrencyCode { get; set; }
        public string ToCurrencyCode { get; set; }
        public decimal OrderFromAmount { get; set; }
        public decimal OrderToAmount { get; set; }
        public decimal OldCustomerBalanceFrom { get; set; }
        public decimal OldCustomerBalanceTo { get; set; }
        public decimal NewCustomerBalanceFrom { get; set; }
        public decimal NewCustomerBalanceTo { get; set; }
        public decimal OldPoolBalanceFrom { get; set; }
        public decimal OldPoolBalanceTo { get; set; }
        public decimal NewPoolBalanceFrom { get; set; }
        public decimal NewPoolBalanceTo { get; set; }
    }
}