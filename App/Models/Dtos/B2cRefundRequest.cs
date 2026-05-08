namespace MpesaPaymentApi.Models.Dtos;

public record B2cRefundRequest(
    string PhoneNumber,
    int Amount,
    string OriginalCheckoutRequestId,
    string Reason);