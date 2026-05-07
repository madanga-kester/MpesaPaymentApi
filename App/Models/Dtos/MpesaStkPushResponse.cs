using System.Text.Json.Serialization;

namespace MpesaPaymentApi.Models.Dtos;

public record MpesaStkPushResponse(
    [property: JsonPropertyName("MerchantRequestID")] string MerchantRequestID,
    [property: JsonPropertyName("CheckoutRequestID")] string CheckoutRequestID,
    [property: JsonPropertyName("ResponseCode")] string ResponseCode,
    [property: JsonPropertyName("ResponseDescription")] string ResponseDescription,
    [property: JsonPropertyName("CustomerMessage")] string CustomerMessage);