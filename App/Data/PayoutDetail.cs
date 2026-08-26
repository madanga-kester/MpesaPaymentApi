


using System;

namespace MpesaPaymentApi.Data;


public class PayoutDetail
{
    public int Id { get; set; }

    
    public string UserId { get; set; } = string.Empty;

    // "mpesa-phone" | "mpesa-till" | "mpesa-paybill" | "bank"
    public string Method { get; set; } = string.Empty;

    // M-Pesa phone payout
    public string? PhoneNumber { get; set; }

    // M-Pesa till payout
    public string? TillNumber { get; set; }

    // M-Pesa paybill payout
    public string? PaybillNumber { get; set; }
    public string? PaybillAccount { get; set; }

    // Bank payout ( B2C-automatable — settled manually/via batch)
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankBranchCode { get; set; }

    public bool IsVerified { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}