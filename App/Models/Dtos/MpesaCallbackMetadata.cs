using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace MpesaPaymentApi.Models.Dtos;

public record MpesaCallbackMetadata(
    [property: JsonPropertyName("Item")] List<MpesaMetadataItem> Item);