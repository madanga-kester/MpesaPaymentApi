using System.Text.Json.Serialization;

namespace MpesaPaymentApi.Models.Dtos;

public record MpesaStkCallback(
    [property: JsonPropertyName("MerchantRequestID")] string MerchantRequestID,
    [property: JsonPropertyName("CheckoutRequestID")] string CheckoutRequestID,
    [property: JsonPropertyName("ResultCode")] int ResultCode,
    [property: JsonPropertyName("ResultDesc")] string ResultDesc,
    [property: JsonPropertyName("CallbackMetadata")] MpesaCallbackMetadata? CallbackMetadata);