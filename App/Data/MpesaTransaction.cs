using System;

namespace MpesaPaymentApi.Data;

public class MpesaTransaction
{
    public int Id { get; set; }
    public string CheckoutRequestID { get; set; } = string.Empty;
    public string MerchantRequestID { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string AccountReference { get; set; } = string.Empty;
    public string TransactionDesc { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int? ResultCode { get; set; }
    public string? ResultDesc { get; set; }
    public string? MpesaReceiptNumber { get; set; }
    public DateTime? TransactionDate { get; set; }
    public DateTime? CallbackReceivedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}