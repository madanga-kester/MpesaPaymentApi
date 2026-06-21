namespace MpesaPaymentApi.Models.Configuration;

public class ClientAppOptions
{
    public List<ClientAppEntry> Apps { get; set; } = new();
}

public class ClientAppEntry
{
    public string ClientId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AllowedOrigin { get; set; } = string.Empty;
}