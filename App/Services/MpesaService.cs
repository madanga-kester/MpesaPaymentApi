using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MpesaPaymentApi.Models.Configuration;
using MpesaPaymentApi.Models.Dtos;

namespace MpesaPaymentApi.Services;

public class MpesaService : IMpesaService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly MpesaOptions _options;
    private readonly ILogger<MpesaService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public MpesaService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        IOptions<MpesaOptions> options,
        ILogger<MpesaService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("MpesaClient");
        _cache = memoryCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MpesaStkPushResponse> SendStkPushAsync(StkPushRequest request, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var password = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ShortCode}{_options.Passkey}{timestamp}"));

        var payload = new
        {
            BusinessShortCode = _options.ShortCode,
            Password = password,
            Timestamp = timestamp,
            TransactionType = "CustomerPayBillOnline",
            Amount = request.Amount,
            PartyA = request.PhoneNumber,
            PartyB = _options.ShortCode,
            PhoneNumber = request.PhoneNumber,
            CallBackURL = _options.CallbackUrl,
            AccountReference = request.AccountReference,
            TransactionDesc = request.TransactionDesc
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.PostAsync("/mpesa/stkpush/v1/processrequest", jsonContent, cancellationToken);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("M-Pesa API Error: {StatusCode} - {Response}", response.StatusCode, responseString);
            var error = JsonSerializer.Deserialize<MpesaErrorResponse>(responseString, JsonOptions);
            throw new Exception($"M-Pesa request failed: {error?.ErrorCode} - {error?.ErrorMessage}");
        }

        var result = JsonSerializer.Deserialize<MpesaStkPushResponse>(responseString, JsonOptions);
        return result ?? throw new Exception("Failed to deserialize M-Pesa response.");
    }

    public async Task<bool> ValidateCallbackAsync(MpesaCallbackPayload payload, CancellationToken cancellationToken = default)
    {
        if (payload?.Body?.StkCallback == null) return false;

        var callback = payload.Body.StkCallback;
        if (callback.ResultCode == 0)
        {
            _logger.LogInformation("M-Pesa Payment Successful. CheckoutID: {CheckoutID}", callback.CheckoutRequestID);
        }
        else
        {
            _logger.LogWarning("M-Pesa Payment Failed. CheckoutID: {CheckoutID} Reason: {Reason}", callback.CheckoutRequestID, callback.ResultDesc);
        }

        return true;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync("MpesaAccessToken", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(55);

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ConsumerKey}:{_options.ConsumerSecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await _httpClient.GetAsync("/oauth/v1/generate?grant_type=client_credentials", cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to get M-Pesa token: {response.StatusCode} - {responseString}");
            }

            var tokenResponse = JsonSerializer.Deserialize<MpesaTokenResponse>(responseString, JsonOptions);
            if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                throw new Exception("Invalid token response from M-Pesa.");
            }
            return tokenResponse.AccessToken;
        });
    }
}