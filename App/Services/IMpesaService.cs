

using MpesaPaymentApi.Models.Dtos;
using System.Threading;
using System.Threading.Tasks;

namespace MpesaPaymentApi.Services;

public interface IMpesaService
{
    Task<MpesaStkPushResponse> SendStkPushAsync(StkPushRequest request, string? userId, string? originClientId, CancellationToken cancellationToken = default);
    Task<bool> ValidateCallbackAsync(MpesaCallbackPayload payload, CancellationToken cancellationToken = default);
    Task<B2cRefundResponse> InitiateB2cRefundAsync(B2cRefundRequest request, string? originClientId, CancellationToken cancellationToken = default);

    Task<PayoutDetailResponse?> GetPayoutSettingsAsync(string freelancerId, CancellationToken cancellationToken = default);
    Task<PayoutDetailResponse> SavePayoutSettingsAsync(string freelancerId, PayoutDetailRequest request, CancellationToken cancellationToken = default);
}