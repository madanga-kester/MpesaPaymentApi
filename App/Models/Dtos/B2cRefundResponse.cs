using System.Text.Json.Serialization;

namespace MpesaPaymentApi.Models.Dtos;

public record B2cRefundResponse(
    [property: JsonPropertyName("ConversationID")] string ConversationID,
    [property: JsonPropertyName("OriginatorConversationID")] string OriginatorConversationID,
    [property: JsonPropertyName("ResponseCode")] string ResponseCode,
    [property: JsonPropertyName("ResponseDescription")] string ResponseDescription);