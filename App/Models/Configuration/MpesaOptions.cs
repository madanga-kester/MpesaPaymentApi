namespace MpesaPaymentApi.Models.Configuration;

public class MpesaOptions
{
    public string BaseUrl { get; set; } = "https://sandbox.safaricom.co.ke";
    public string ConsumerKey { get; set; } = string.Empty;
    public string ConsumerSecret { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public string Passkey { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
}