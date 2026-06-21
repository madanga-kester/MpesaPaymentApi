using System.Threading.Channels;
namespace MpesaPaymentApi.Services;

public record MpesaCallbackJob(int TransactionId, string CheckoutRequestId);

/// <summary>
/// In-process bounded queue for post-callback side effects (notifications, receipts, etc.)
/// For multi-instance/horizontal scaling, replace this with a real broker
/// (Azure Service Bus, AWS SQS, RabbitMQ) so jobs survive instance restarts
/// and are shared across instances instead of being per-process.
/// </summary>
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
                // Example future work — replace with real post-payment side effects:
                // var notifier = scope.ServiceProvider.GetRequiredService<INotificationService>();
                // await notifier.SendPaymentReceiptAsync(job.TransactionId, stoppingToken);
                _logger.LogInformation("Processed background job for transaction {TransactionId}", job.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing background job for transaction {TransactionId}", job.TransactionId);
            }
        }
    }
}