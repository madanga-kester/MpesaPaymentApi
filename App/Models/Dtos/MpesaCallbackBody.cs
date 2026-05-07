using System.Text.Json.Serialization;

namespace MpesaPaymentApi.Models.Dtos;

public record MpesaCallbackBody(
    [property: JsonPropertyName("stkCallback")] MpesaStkCallback StkCallback);