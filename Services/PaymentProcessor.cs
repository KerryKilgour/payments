using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using payments.Data;
using payments.Models;

namespace payments.Services;

public class PaymentProcessor : IPaymentProcessor
{
    private const int MaxRetries = 3;
    private readonly PaymentDbContext _dbContext;
    private readonly IPaymentQueue _paymentQueue;
    private readonly ILogger<PaymentProcessor> _logger;

    public PaymentProcessor(PaymentDbContext dbContext, IPaymentQueue paymentQueue, ILogger<PaymentProcessor> logger)
    {
        _dbContext = dbContext;
        _paymentQueue = paymentQueue;
        _logger = logger;
    }

    public async Task ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var queueItem = await _paymentQueue.DequeueNextAsync(cancellationToken);
        if (queueItem is null)
        {
            return;
        }

        var request = await _dbContext.PaymentRequests.FindAsync(new object[] { queueItem.RequestId }, cancellationToken);
        if (request is null)
        {
            _logger.LogWarning("Queue item {QueueItemId} references missing PaymentRequest {RequestId}", queueItem.Id, queueItem.RequestId);
            await _paymentQueue.MarkProcessedAsync(queueItem, cancellationToken);
            return;
        }

        if (request.Status == PaymentStatus.Completed)
        {
            await _paymentQueue.MarkProcessedAsync(queueItem, cancellationToken);
            return;
        }

        request.Status = PaymentStatus.Processing;
        request.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await ExecutePaymentAsync(request, cancellationToken);
            request.Status = PaymentStatus.Completed;
            request.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _paymentQueue.MarkProcessedAsync(queueItem, cancellationToken);
        }
        catch (Exception ex)
        {
            request.RetryCount += 1;
            request.UpdatedAt = DateTime.UtcNow;

            if (request.RetryCount >= MaxRetries)
            {
                request.Status = PaymentStatus.DeadLetter;
                var payload = System.Text.Json.JsonSerializer.Serialize(request);
                _dbContext.PaymentDeadLetters.Add(new PaymentDeadLetter
                {
                    Id = Guid.NewGuid(),
                    RequestId = request.Id,
                    Reason = ex.Message,
                    Payload = payload,
                    CreatedAt = DateTime.UtcNow
                });
                _logger.LogError(ex, "Payment request {RequestId} moved to dead letter after {RetryCount} attempts", request.Id, request.RetryCount);
                await _paymentQueue.MarkProcessedAsync(queueItem, cancellationToken);
            }
            else
            {
                request.Status = PaymentStatus.Pending;
                _logger.LogWarning(ex, "Retry {RetryCount}/{MaxRetries} for payment request {RequestId}", request.RetryCount, MaxRetries, request.Id);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ExecutePaymentAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(200, cancellationToken);

        if (request.Amount > 10000)
        {
            throw new InvalidOperationException("Simulated payment gateway failure for amount above threshold.");
        }
    }
}
