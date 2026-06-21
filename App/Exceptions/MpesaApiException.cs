using System.Net;

namespace MpesaPaymentApi.Exceptions;

public class MpesaApiException : Exception
{
    public HttpStatusCode? UpstreamStatusCode { get; }

    public MpesaApiException(string message, HttpStatusCode? upstreamStatusCode = null)
        : base(message)
    {
        UpstreamStatusCode = upstreamStatusCode;
    }
}