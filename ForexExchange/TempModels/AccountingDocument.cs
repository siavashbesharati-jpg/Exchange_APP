using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class AccountingDocument
{
    public int Id { get; set; }

    public int Type { get; set; }

    public int PayerType { get; set; }

    public int? PayerCustomerId { get; set; }

    public int? PayerBankAccountId { get; set; }

    public int ReceiverType { get; set; }

    public int? ReceiverCustomerId { get; set; }

    public int? ReceiverBankAccountId { get; set; }

    public int Amount { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime DocumentDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public int IsVerified { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public string? VerifiedBy { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? FileName { get; set; }

    public string? ContentType { get; set; }

    public byte[]? FileData { get; set; }

    public string? Notes { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public int IsDeleted { get; set; }

    public int IsFrozen { get; set; }

    public virtual BankAccount? PayerBankAccount { get; set; }

    public virtual Customer? PayerCustomer { get; set; }

    public virtual BankAccount? ReceiverBankAccount { get; set; }

    public virtual Customer? ReceiverCustomer { get; set; }
}
