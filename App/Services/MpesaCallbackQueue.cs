using System.Threading.Channels;
namespace MpesaPaymentApi.Services;

public record MpesaCallbackJob(int TransactionId, string CheckoutRequestId);

public class MpesaCallbackQueue
{
    private readonly Channel<MpesaCallbackJob> _channel =
        Channel.CreateBounded<MpesaCallbackJob>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    public ValueTask EnqueueAsync(MpesaCallbackJob job, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(job, ct);

    public IAsyncEnumerable<MpesaCallbackJob> DequeueAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}

public class MpesaCallbackQueueProcessor : BackgroundService
{
    private readonly MpesaCallbackQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MpesaCallbackQueueProcessor> _logger;

    public MpesaCallbackQueueProcessor(
        MpesaCallbackQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<MpesaCallbackQueueProcessor> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                
                _logger.LogInformation("Processed background job for transaction {TransactionId}", job.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing background job for transaction {TransactionId}", job.TransactionId);
            }
        }
    }
}