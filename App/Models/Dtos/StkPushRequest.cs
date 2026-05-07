namespace MpesaPaymentApi.Models.Dtos;

public record StkPushRequest(string PhoneNumber, int Amount, string AccountReference, string TransactionDesc);