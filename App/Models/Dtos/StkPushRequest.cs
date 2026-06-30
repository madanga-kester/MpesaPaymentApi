

using System.ComponentModel.DataAnnotations;

namespace MpesaPaymentApi.Models.Dtos;

public record StkPushRequest(
    [Required, RegularExpression(@"^(?:254|0)?(7|1)\d{8}$", ErrorMessage = "Invalid Kenyan phone number.")]
    string PhoneNumber,

    [Range(1, 150000, ErrorMessage = "Amount must be between 1 and 150000 KES.")]
    int Amount,

    [Required, StringLength(50, MinimumLength = 1)]
    string AccountReference,

    [Required, StringLength(100, MinimumLength = 1)]
    string TransactionDesc,

    string? RecipientFreelancerId = null);


