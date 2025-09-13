using System;
using System.Collections.Generic;

namespace ForexExchange.Models
{
    public class CustomerStatementViewModel
    {
        public Customer Customer { get; set; }
        public List<AccountingDocument> Documents { get; set; }
        public List<CustomerBalance> Balances { get; set; }
        public DateTime StatementDate { get; set; }
        
        public CustomerStatementViewModel()
        {
            Documents = new List<AccountingDocument>();
            Balances = new List<CustomerBalance>();
            Customer = new();
        }
    }
}
