


using System;

namespace MpesaPaymentApi.Data;


public class FreelancerPayout
{
    public int Id { get; set; }

   
    public int MpesaTransactionId { get; set; }

    public string FreelancerId { get; set; } = string.Empty;

    public decimal GrossAmount { get; set; }
    public decimal PlatformFee { get; set; } = 0m; // stubbed at 0 for now — adjust  pricing is decided
    public decimal NetAmount { get; set; }

    public string Method { get; set; } = string.Empty; 
    
    public string Status { get; set; } = "Pending";

    public string? ConversationID { get; set; }            
    public string? OriginatorConversationID { get; set; }
    public string? ResultDesc { get; set; }
    public string? MpesaReceiptNumber { get; set; }         
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}