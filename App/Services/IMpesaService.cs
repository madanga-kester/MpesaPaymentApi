using MpesaPaymentApi.Models.Dtos;
using System.Threading;
using System.Threading.Tasks;

namespace MpesaPaymentApi.Services;

public interface IMpesaService
{
    Task<MpesaStkPushResponse> SendStkPushAsync(StkPushRequest request, CancellationToken cancellationToken = default);
    Task<bool> ValidateCallbackAsync(MpesaCallbackPayload payload, CancellationToken cancellationToken = default);
}