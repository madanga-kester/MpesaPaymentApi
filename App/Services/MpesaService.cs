using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MpesaPaymentApi.Data;
using MpesaPaymentApi.Models.Configuration;
using MpesaPaymentApi.Models.Dtos;

namespace MpesaPaymentApi.Services;

public class MpesaService : IMpesaService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly MpesaOptions _options;
    private readonly ILogger<MpesaService> _logger;
    private readonly AppDbContext _dbContext;
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
        ILogger<MpesaService> logger,
        AppDbContext dbContext)
    {
        _httpClient = httpClientFactory.CreateClient("MpesaClient");
        _cache = memoryCache;
        _options = options.Value;
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task<MpesaStkPushResponse> SendStkPushAsync(StkPushRequest request, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var password = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ShortCode}{_options.Passkey}{timestamp}"));

        var transaction = new MpesaTransaction
        {
            PhoneNumber = request.PhoneNumber,
            Amount = request.Amount,
            AccountReference = request.AccountReference,
            TransactionDesc = request.TransactionDesc,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.MpesaTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
            transaction.Status = "Failed";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogError("M-Pesa API Error: {StatusCode} - {Response}", response.StatusCode, responseString);
            var error = JsonSerializer.Deserialize<MpesaErrorResponse>(responseString, JsonOptions);
            throw new Exception($"M-Pesa request failed: {error?.ErrorCode} - {error?.ErrorMessage}");
        }

        var result = JsonSerializer.Deserialize<MpesaStkPushResponse>(responseString, JsonOptions);
        if (result == null) throw new Exception("Failed to deserialize M-Pesa response.");

        transaction.CheckoutRequestID = result.CheckoutRequestID;
        transaction.MerchantRequestID = result.MerchantRequestID;
        transaction.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    public async Task<bool> ValidateCallbackAsync(MpesaCallbackPayload payload, CancellationToken cancellationToken = default)
    {
        if (payload?.Body?.StkCallback == null) return false;

        var callback = payload.Body.StkCallback;
        var transaction = await _dbContext.MpesaTransactions
            .FirstOrDefaultAsync(t => t.CheckoutRequestID == callback.CheckoutRequestID, cancellationToken);

        if (transaction == null)
        {
            _logger.LogWarning("Callback received for unknown CheckoutRequestID: {CheckoutID}", callback.CheckoutRequestID);
            return true;
        }

        if (transaction.CallbackReceivedAt != null)
        {
            _logger.LogInformation("Duplicate callback ignored for CheckoutRequestID: {CheckoutID}", callback.CheckoutRequestID);
            return true;
        }

        transaction.ResultCode = callback.ResultCode;
        transaction.ResultDesc = callback.ResultDesc;
        transaction.CallbackReceivedAt = DateTime.UtcNow;
        transaction.UpdatedAt = DateTime.UtcNow;

        if (callback.ResultCode == 0 && callback.CallbackMetadata != null)
        {
            transaction.Status = "Success";
            foreach (var item in callback.CallbackMetadata.Item)
            {
                switch (item.Name)
                {
                    case "Amount":
                        transaction.Amount = Convert.ToDecimal(item.Value);
                        break;
                    case "MpesaReceiptNumber":
                        transaction.MpesaReceiptNumber = item.Value?.ToString();
                        break;
                    case "TransactionDate":
                        if (item.Value != null && long.TryParse(item.Value.ToString(), out long dateVal))
                        {
                            transaction.TransactionDate = DateTime.ParseExact(dateVal.ToString(), "yyyyMMddHHmmss", null);
                        }
                        break;
                }
            }
            _logger.LogInformation("M-Pesa Payment Successful. CheckoutID: {CheckoutID}, Receipt: {Receipt}",
                callback.CheckoutRequestID, transaction.MpesaReceiptNumber);
        }
        else
        {
            transaction.Status = callback.ResultCode == 1032 ? "Cancelled" : "Failed";
            _logger.LogWarning("M-Pesa Payment {Status}. CheckoutID: {CheckoutID} Reason: {Reason}",
                transaction.Status, callback.CheckoutRequestID, callback.ResultDesc);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
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
            if (tokenResponse?.AccessToken == null)
            {
                throw new Exception("Invalid token response from M-Pesa.");
            }
            return tokenResponse.AccessToken!; 

        });
    }






    public async Task<B2cRefundResponse> InitiateB2cRefundAsync(B2cRefundRequest request, CancellationToken cancellationToken = default)
    {
        // Verify the original transaction exists and was successful
        var originalTransaction = await _dbContext.MpesaTransactions
            .FirstOrDefaultAsync(t => t.CheckoutRequestID == request.OriginalCheckoutRequestId, cancellationToken);

        if (originalTransaction == null)
            throw new Exception($"Original transaction not found: {request.OriginalCheckoutRequestId}");

        if (originalTransaction.Status != "Success")
            throw new Exception($"Cannot refund non-successful transaction. Status: {originalTransaction.Status}");

        if (originalTransaction.Amount < request.Amount)
            throw new Exception($"Refund amount ({request.Amount}) exceeds original payment ({originalTransaction.Amount})");

        var token = await GetAccessTokenAsync(cancellationToken);
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        // B2C payload for Safaricom
        var payload = new
        {
            InitiatorName = "testapi", // Sandbox initiator name - change for production
            SecurityCredential = "SAFARICOM_SANDBOX_CREDENTIAL", // Sandbox static value - see note below
            CommandID = "BusinessPayment", // Or "SalaryPayment" / "PromotionPayment"
            Amount = request.Amount,
            PartyA = _options.ShortCode, // Your PayBill/Till
            PartyB = request.PhoneNumber, // Recipient phone
            Remarks = request.Reason,
            QueueTimeOutURL = _options.CallbackUrl,
            ResultURL = _options.CallbackUrl,
            Occasion = $"Refund-{request.OriginalCheckoutRequestId}"
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.PostAsync("/mpesa/b2c/v1/paymentrequest", jsonContent, cancellationToken);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("B2C API Error: {StatusCode} - {Response}", response.StatusCode, responseString);
            var error = JsonSerializer.Deserialize<MpesaErrorResponse>(responseString, JsonOptions);
            throw new Exception($"B2C refund failed: {error?.ErrorCode} - {error?.ErrorMessage}");
        }

        var result = JsonSerializer.Deserialize<B2cRefundResponse>(responseString, JsonOptions);
        if (result == null) throw new Exception("Failed to deserialize B2C response.");

        // Log the refund attempt
        _logger.LogInformation("B2C Refund initiated: Original={Original}, Recipient={Phone}, Amount={Amount}, ConversationID={ConvId}",
            request.OriginalCheckoutRequestId, request.PhoneNumber, request.Amount, result.ConversationID);

        return result;
    }
}