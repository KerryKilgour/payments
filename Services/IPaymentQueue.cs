using payments.Models;

namespace payments.Services;

public interface IPaymentQueue
{
    Task EnqueueAsync(PaymentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentQueueItem?> DequeueNextAsync(CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(PaymentQueueItem item, CancellationToken cancellationToken = default);
}
