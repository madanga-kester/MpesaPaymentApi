using System.ComponentModel.DataAnnotations;

namespace MpesaPaymentApi.Models.Dtos;

public record B2cRefundRequest(
    [Required, RegularExpression(@"^(?:254|0)?(7|1)\d{8}$", ErrorMessage = "Invalid Kenyan phone number.")]
    string PhoneNumber,

    [Range(1, 150000, ErrorMessage = "Refund amount must be between 1 and 150000 KES.")]
    int Amount,

    [Required]
    string OriginalCheckoutRequestId,

    [Required, StringLength(200, MinimumLength = 1)]
    string Reason);