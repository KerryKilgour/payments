using Microsoft.EntityFrameworkCore;
using payments.Data;
using payments.Models;

namespace payments.Services;

public class PaymentQueue : IPaymentQueue
{
    private readonly PaymentDbContext _dbContext;

    public PaymentQueue(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnqueueAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.PaymentRequests.SingleOrDefaultAsync(p => p.ExternalId == request.ExternalId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        _dbContext.PaymentRequests.Add(request);
        _dbContext.PaymentQueueItems.Add(new PaymentQueueItem
        {
            Id = Guid.NewGuid(),
            RequestId = request.Id,
            EnqueuedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaymentQueueItem?> DequeueNextAsync(CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.PaymentQueueItems
            .OrderBy(q => q.EnqueuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return item;
    }

    public async Task MarkProcessedAsync(PaymentQueueItem item, CancellationToken cancellationToken = default)
    {
        item.ProcessedAt = DateTime.UtcNow;
        _dbContext.PaymentQueueItems.Update(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
