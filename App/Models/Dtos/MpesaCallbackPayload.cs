using System.Text.Json.Serialization;

namespace MpesaPaymentApi.Models.Dtos;

public record MpesaCallbackPayload(
    [property: JsonPropertyName("Body")] MpesaCallbackBody Body);