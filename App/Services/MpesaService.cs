

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MpesaPaymentApi.Data;
using MpesaPaymentApi.Exceptions;
using MpesaPaymentApi.Models.Configuration;
using MpesaPaymentApi.Models.Dtos;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MpesaPaymentApi.Services;


public class StalePendingTransactionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StalePendingTransactionService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(2);
    private readonly TimeSpan _staleAfter = TimeSpan.FromMinutes(2);

    public StalePendingTransactionService(
        IServiceScopeFactory scopeFactory,
        ILogger<StalePendingTransactionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[STALE-CHECK] Stale transaction monitor started. Interval={Interval}m, StaleAfter={StaleAfter}m",
            _interval.TotalMinutes, _staleAfter.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_interval, stoppingToken);

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var cutoff = DateTime.UtcNow - _staleAfter;
                var stale = await db.MpesaTransactions
                    .Where(t => t.Status == "Pending" && t.CreatedAt < cutoff)
                    .ToListAsync(stoppingToken);

                if (stale.Count == 0) continue;

                foreach (var t in stale)
                {
                    t.Status = "Timeout";
                    t.ResultCode = 1037;
                    t.ResultDesc = "STK push timed out — no callback received.";
                    t.UpdatedAt = DateTime.UtcNow;
                }

                await db.SaveChangesAsync(stoppingToken);
                _logger.LogWarning("[STALE-CHECK] Marked {Count} stale transaction(s) as Timeout.", stale.Count);
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STALE-CHECK] Error while processing stale transactions.");
            }
        }
    }
}

//  MpesaService 
public class MpesaService : IMpesaService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly MpesaOptions _options;
    private readonly ILogger<MpesaService> _logger;
    private readonly AppDbContext _dbContext;
    private readonly MpesaCallbackQueue _callbackQueue;
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
            AppDbContext dbContext,
            MpesaCallbackQueue callbackQueue)
    {
        _httpClient = httpClientFactory.CreateClient("MpesaClient");
        _cache = memoryCache;
        _options = options.Value;
        _logger = logger;
        _dbContext = dbContext;
        _callbackQueue = callbackQueue;
    }

    public async Task<MpesaStkPushResponse> SendStkPushAsync(StkPushRequest request, string? userId, string? originClientId, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var password = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ShortCode}{_options.Passkey}{timestamp}"));
        var normalizedPhone = NormalizePhoneNumber(request.PhoneNumber);

        var transaction = new MpesaTransaction
        {
            UserId = userId,
            OriginClientId = originClientId,
            PhoneNumber = normalizedPhone,
            Amount = request.Amount,
            AccountReference = request.AccountReference,
            TransactionDesc = request.TransactionDesc,
           
            RecipientFreelancerId = request.RecipientFreelancerId,
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
            PartyA = normalizedPhone,
            PartyB = _options.ShortCode,
            PhoneNumber = normalizedPhone,
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
            throw new MpesaApiException($"M-Pesa request failed: {error?.ErrorCode} - {error?.ErrorMessage}", response.StatusCode);
        }

        var result = JsonSerializer.Deserialize<MpesaStkPushResponse>(responseString, JsonOptions);
        if (result == null) throw new MpesaApiException("Failed to deserialize M-Pesa response.", response.StatusCode);

        if (!string.IsNullOrWhiteSpace(result.CheckoutRequestID))
        {
            transaction.CheckoutRequestID = result.CheckoutRequestID;
            transaction.MerchantRequestID = result.MerchantRequestID;
        }
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


        transaction.Status = callback.ResultCode switch
        {
            0 => "Success",    // Payment completed
            1032 => "Cancelled",  // User cancelled the STK prompt
            1037 => "Timeout",    // STK push timed out on Safaricom's side
            _ => "Failed"      // Any other error
        };

        if (callback.ResultCode == 0 && callback.CallbackMetadata != null)
        {
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
                            transaction.TransactionDate = DateTime.ParseExact(dateVal.ToString(), "yyyyMMddHHmmss", null);
                        break;
                }
            }
            _logger.LogInformation("M-Pesa Payment Successful. CheckoutID: {CheckoutID}, Receipt: {Receipt}",
                callback.CheckoutRequestID, transaction.MpesaReceiptNumber);
        }
        else
        {
            _logger.LogWarning("M-Pesa Payment {Status} (code={Code}). CheckoutID: {CheckoutID} Reason: {Reason}",
                transaction.Status, callback.ResultCode, callback.CheckoutRequestID, callback.ResultDesc);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _callbackQueue.EnqueueAsync(new MpesaCallbackJob(transaction.Id, callback.CheckoutRequestID), cancellationToken);


        if (transaction.Status == "Success" && !string.IsNullOrWhiteSpace(transaction.RecipientFreelancerId))
        {
            try
            {
                await SettleFreelancerAsync(transaction, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Freelancer settlement failed for transaction {TransactionId} (freelancer {FreelancerId}). " +
                    "Inbound payment was still recorded as Success — this needs manual review.",
                    transaction.Id, transaction.RecipientFreelancerId);
            }
        }

        return true;
    }

  
    private async Task SettleFreelancerAsync(MpesaTransaction transaction, CancellationToken cancellationToken)
    {
        var payoutDetail = await _dbContext.PayoutDetails
            .FirstOrDefaultAsync(p => p.UserId == transaction.RecipientFreelancerId, cancellationToken);

        if (payoutDetail == null)
        {
            _logger.LogWarning(
                "No payout details found for freelancer {FreelancerId}. Transaction {TransactionId} needs manual settlement.",
                transaction.RecipientFreelancerId, transaction.Id);

            _dbContext.FreelancerPayouts.Add(new FreelancerPayout
            {
                MpesaTransactionId = transaction.Id,
                FreelancerId = transaction.RecipientFreelancerId!,
                GrossAmount = transaction.Amount,
                PlatformFee = 0m,
                NetAmount = transaction.Amount,
                Method = "unknown",
                Status = "ManualReview",
                ResultDesc = "No payout method on file for this freelancer.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // Platform fee stubbed at 0% for now — adjusting  once pricing is decided.
        var platformFee = 0m;
        var netAmount = transaction.Amount - platformFee;

        var payout = new FreelancerPayout
        {
            MpesaTransactionId = transaction.Id,
            FreelancerId = payoutDetail.UserId,
            GrossAmount = transaction.Amount,
            PlatformFee = platformFee,
            NetAmount = netAmount,
            Method = payoutDetail.Method,
            Status = "Processing",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.FreelancerPayouts.Add(payout);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // flag for manual/batch processing.
        if (payoutDetail.Method == "bank")
        {
            payout.Status = "ManualReview";
            payout.ResultDesc = "Bank payout — requires manual bank transfer processing.";
            payout.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // M-Pesa methods (phone, till, paybill) settled via B2C.
        string payoutDestination = payoutDetail.Method switch
        {
            "mpesa-phone" => NormalizePhoneNumber(payoutDetail.PhoneNumber ?? string.Empty),
            "mpesa-till" => payoutDetail.TillNumber ?? string.Empty,
            "mpesa-paybill" => payoutDetail.PaybillNumber ?? string.Empty,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(payoutDestination))
        {
            payout.Status = "Failed";
            payout.ResultDesc = $"Payout method '{payoutDetail.Method}' is missing its destination value.";
            payout.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            var b2cResult = await SendB2cPaymentAsync(
                amount: netAmount,
                partyB: payoutDestination,
                remarks: $"Payout for {transaction.AccountReference ?? transaction.CheckoutRequestID}",
                occasion: $"Settlement-{transaction.Id}",
                cancellationToken: cancellationToken);

            payout.ConversationID = b2cResult.ConversationID;
            payout.OriginatorConversationID = b2cResult.OriginatorConversationID;
            payout.Status = "Processing"; 
            payout.ResultDesc = "B2C payout initiated; awaiting Safaricom result callback.";
            payout.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Freelancer payout initiated. Freelancer={FreelancerId}, Amount={Amount}, Method={Method}, ConversationID={ConvId}",
                payoutDetail.UserId, netAmount, payoutDetail.Method, b2cResult.ConversationID);
        }
        catch (Exception ex)
        {
            payout.Status = "Failed";
            payout.ResultDesc = ex.Message;
            _logger.LogError(ex,
                "B2C payout failed for freelancer {FreelancerId}, transaction {TransactionId}",
                payoutDetail.UserId, transaction.Id);
        }
        finally
        {
            payout.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)

    {
        var token = await _cache.GetOrCreateAsync("MpesaAccessToken", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(55);

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ConsumerKey}:{_options.ConsumerSecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await _httpClient.GetAsync("/oauth/v1/generate?grant_type=client_credentials", cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new MpesaApiException($"Failed to get M-Pesa token: {response.StatusCode} - {responseString}", response.StatusCode);

            var tokenResponse = JsonSerializer.Deserialize<MpesaTokenResponse>(responseString, JsonOptions);
            if (tokenResponse?.AccessToken == null)
                throw new MpesaApiException("Invalid token response from M-Pesa.", response.StatusCode);

            return tokenResponse.AccessToken;
        });

        if (string.IsNullOrWhiteSpace(token))
            throw new MpesaApiException("M-Pesa access token cache returned an empty value.");

        return token;
    }

    public async Task<B2cRefundResponse> InitiateB2cRefundAsync(B2cRefundRequest request, string? originClientId, CancellationToken cancellationToken = default)
    {
        var normalizedPhone = NormalizePhoneNumber(request.PhoneNumber);
        var originalTransaction = await _dbContext.MpesaTransactions
            .FirstOrDefaultAsync(t => t.CheckoutRequestID == request.OriginalCheckoutRequestId, cancellationToken);

        if (originalTransaction == null)
            throw new Exception($"Original transaction not found: {request.OriginalCheckoutRequestId}");

        if (originalTransaction.Status != "Success")
            throw new Exception($"Cannot refund non-successful transaction. Status: {originalTransaction.Status}");

        if (originalTransaction.Amount < request.Amount)
            throw new Exception($"Refund amount ({request.Amount}) exceeds original payment ({originalTransaction.Amount})");

      
        var result = await SendB2cPaymentAsync(
            amount: request.Amount,
            partyB: normalizedPhone,
            remarks: request.Reason,
            occasion: $"Refund-{request.OriginalCheckoutRequestId}",
            cancellationToken: cancellationToken);

        _logger.LogInformation("B2C Refund initiated: Original={Original}, Recipient={Phone}, Amount={Amount}, ConversationID={ConvId}, OriginClient={ClientId}",
                    request.OriginalCheckoutRequestId, request.PhoneNumber, request.Amount, result.ConversationID, originClientId);
        return result;
    }

  
    private async Task<B2cRefundResponse> SendB2cPaymentAsync(
        decimal amount,
        string partyB,
        string remarks,
        string occasion,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var payload = new
        {
            InitiatorName = _options.InitiatorName,
            SecurityCredential = _options.SecurityCredential,
            CommandID = "BusinessPayment",
            Amount = amount,
            PartyA = _options.ShortCode,
            PartyB = partyB,
            Remarks = remarks,
            QueueTimeOutURL = _options.CallbackUrl,
            ResultURL = _options.CallbackUrl,
            Occasion = occasion
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.PostAsync("/mpesa/b2c/v1/paymentrequest", jsonContent, cancellationToken);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("B2C API Error: {StatusCode} - {Response}", response.StatusCode, responseString);
            var error = JsonSerializer.Deserialize<MpesaErrorResponse>(responseString, JsonOptions);
            throw new MpesaApiException($"B2C payment failed: {error?.ErrorCode} - {error?.ErrorMessage}", response.StatusCode);
        }

        var result = JsonSerializer.Deserialize<B2cRefundResponse>(responseString, JsonOptions);
        if (result == null) throw new MpesaApiException("Failed to deserialize B2C response.", response.StatusCode);

        return result;
    }

 
    public async Task<PayoutDetailResponse?> GetPayoutSettingsAsync(string freelancerId, CancellationToken cancellationToken = default)
    {
        var detail = await _dbContext.PayoutDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == freelancerId, cancellationToken);

        if (detail == null) return null;

        return ToResponse(detail);
    }

    public async Task<PayoutDetailResponse> SavePayoutSettingsAsync(string freelancerId, PayoutDetailRequest request, CancellationToken cancellationToken = default)
    {
        var validMethods = new[] { "mpesa-phone", "mpesa-till", "mpesa-paybill", "bank" };
        if (!validMethods.Contains(request.Method))
            throw new ArgumentException($"Invalid payout method: '{request.Method}'");

        var detail = await _dbContext.PayoutDetails
            .FirstOrDefaultAsync(p => p.UserId == freelancerId, cancellationToken);

        if (detail == null)
        {
            detail = new PayoutDetail
            {
                UserId = freelancerId,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.PayoutDetails.Add(detail);
        }

        detail.Method = request.Method;
        detail.PhoneNumber = request.PhoneNumber;
        detail.TillNumber = request.TillNumber;
        detail.PaybillNumber = request.PaybillNumber;
        detail.PaybillAccount = request.PaybillAccount;
        detail.BankName = request.BankName;
        detail.BankAccountName = request.BankAccountName;
        detail.BankAccountNumber = request.BankAccountNumber;
        detail.BankBranchCode = request.BankBranchCode;
       
        detail.IsVerified = false;
        detail.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(detail);
    }

    private static PayoutDetailResponse ToResponse(PayoutDetail detail) => new()
    {
        Id = detail.Id,
        FreelancerId = detail.UserId,
        Method = detail.Method,
        PhoneNumber = detail.PhoneNumber,
        TillNumber = detail.TillNumber,
        PaybillNumber = detail.PaybillNumber,
        PaybillAccount = detail.PaybillAccount,
        BankName = detail.BankName,
        BankAccountName = detail.BankAccountName,
        BankAccountNumber = detail.BankAccountNumber,
        BankBranchCode = detail.BankBranchCode,
        IsVerified = detail.IsVerified,
        UpdatedAt = detail.UpdatedAt
    };

    private static string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Phone number cannot be empty", nameof(phoneNumber));

        var cleaned = phoneNumber.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");

        if (cleaned.StartsWith("07") || cleaned.StartsWith("01"))
            return "254" + cleaned.Substring(1);

        if (cleaned.StartsWith("254"))
            return cleaned;

        if (cleaned.StartsWith("7") || cleaned.StartsWith("1"))
            return "254" + cleaned;

        throw new ArgumentException(
            $"Invalid Kenyan phone number format: '{phoneNumber}'. Expected formats: 07XXXXXXXX, +2547XXXXXXXX, or 2547XXXXXXXX",
            nameof(phoneNumber));
    }
}