using System.Text.Json.Serialization;

namespace MpesaPaymentApi.Models.Dtos;

public record MpesaErrorResponse(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("errorCode")] string ErrorCode,
    [property: JsonPropertyName("errorMessage")] string ErrorMessage);