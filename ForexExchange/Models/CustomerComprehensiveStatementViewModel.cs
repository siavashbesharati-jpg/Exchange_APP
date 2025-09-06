using System;
using System.Collections.Generic;

namespace ForexExchange.Models
{
    public class CustomerComprehensiveStatementViewModel
    {
        public Customer Customer { get; set; } = null!;
        public List<AccountingDocument> Documents { get; set; }
        public List<CustomerBalance> Balances { get; set; }
        public List<Order> Orders { get; set; }
        public CustomerDebtCredit? CustomerDebtCredit { get; set; }
        public CustomerProfileStats Stats { get; set; } = null!;
        public DateTime StatementDate { get; set; }
        
        public CustomerComprehensiveStatementViewModel()
        {
            Documents = new List<AccountingDocument>();
            Balances = new List<CustomerBalance>();
            Orders = new List<Order>();
        }
    }
}
