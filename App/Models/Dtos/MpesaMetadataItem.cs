using System.Text.Json.Serialization;

namespace MpesaPaymentApi.Models.Dtos;

public record MpesaMetadataItem(
    [property: JsonPropertyName("Name")] string Name,
    [property: JsonPropertyName("Value")] object Value);