using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using payments.Data;
using payments.Models;
using payments.Services;
using Xunit;

namespace payments.tests.Services;

public class PaymentProcessorTests
{
    private PaymentDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PaymentDbContext(options);
    }

    private IPaymentQueue CreateMockQueue(PaymentDbContext dbContext)
    {
        return new PaymentQueue(dbContext);
    }

    private ILogger<PaymentProcessor> CreateMockLogger()
    {
        return new Mock<ILogger<PaymentProcessor>>().Object;
    }

    [Fact]
    public async Task ProcessNextAsync_SkipsWhenQueueIsEmpty()
    {
        var dbContext = CreateDbContext();
        var queue = CreateMockQueue(dbContext);
        var logger = CreateMockLogger();
        var processor = new PaymentProcessor(dbContext, queue, logger);

        await processor.ProcessNextAsync();

        var queueItems = await dbContext.PaymentQueueItems.ToListAsync();
        Assert.Empty(queueItems);
    }

    [Fact]
    public async Task ProcessNextAsync_CompletesPaymentSuccessfully()
    {
        var dbContext = CreateDbContext();
        var queue = CreateMockQueue(dbContext);
        var logger = CreateMockLogger();
        var processor = new PaymentProcessor(dbContext, queue, logger);

        var request = new PaymentRequest
        {
            Id = Guid.NewGuid(),
            ExternalId = "ext-123",
            Amount = 100,
            Currency = "USD",
            CardNumberHash = "hash",
            CardExpiry = "12/25",
            CardHolderName = "John Doe",
            Status = PaymentStatus.Pending,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await queue.EnqueueAsync(request);
        await processor.ProcessNextAsync();

        var updatedRequest = await dbContext.PaymentRequests.FindAsync(request.Id);
        Assert.NotNull(updatedRequest);
        Assert.Equal(PaymentStatus.Completed, updatedRequest.Status);
    }

    [Fact]
    public async Task ProcessNextAsync_SkipsAlreadyCompletedPayment()
    {
        var dbContext = CreateDbContext();
        var queue = CreateMockQueue(dbContext);
        var logger = CreateMockLogger();
        var processor = new PaymentProcessor(dbContext, queue, logger);

        var request = new PaymentRequest
        {
            Id = Guid.NewGuid(),
            ExternalId = "ext-456",
            Amount = 100,
            Currency = "USD",
            CardNumberHash = "hash",
            CardExpiry = "12/25",
            CardHolderName = "Jane Doe",
            Status = PaymentStatus.Completed,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await queue.EnqueueAsync(request);

        var queueItem = await queue.DequeueNextAsync();
        Assert.NotNull(queueItem);

        var initialProcessedAt = queueItem.ProcessedAt;

        await processor.ProcessNextAsync();

        var markedItem = await dbContext.PaymentQueueItems.FindAsync(queueItem.Id);
        Assert.NotNull(markedItem);
        Assert.NotNull(markedItem.ProcessedAt);
    }

    [Fact]
    public async Task ProcessNextAsync_RetriesFailedPayment()
    {
        var dbContext = CreateDbContext();
        var queue = CreateMockQueue(dbContext);
        var logger = CreateMockLogger();
        var processor = new PaymentProcessor(dbContext, queue, logger);

        var request = new PaymentRequest
        {
            Id = Guid.NewGuid(),
            ExternalId = "ext-large",
            Amount = 50000,
            Currency = "USD",
            CardNumberHash = "hash",
            CardExpiry = "12/25",
            CardHolderName = "Rich Client",
            Status = PaymentStatus.Pending,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await queue.EnqueueAsync(request);
        await processor.ProcessNextAsync();

        var updatedRequest = await dbContext.PaymentRequests.FindAsync(request.Id);
        Assert.NotNull(updatedRequest);
        Assert.Equal(1, updatedRequest.RetryCount);
        Assert.Equal(PaymentStatus.Pending, updatedRequest.Status);
    }

    [Fact]
    public async Task ProcessNextAsync_MovesToDeadLetterAfterMaxRetries()
    {
        var dbContext = CreateDbContext();
        var queue = CreateMockQueue(dbContext);
        var logger = CreateMockLogger();
        var processor = new PaymentProcessor(dbContext, queue, logger);

        var request = new PaymentRequest
        {
            Id = Guid.NewGuid(),
            ExternalId = "ext-fail",
            Amount = 50000,
            Currency = "USD",
            CardNumberHash = "hash",
            CardExpiry = "12/25",
            CardHolderName = "Failing Client",
            Status = PaymentStatus.Pending,
            RetryCount = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await queue.EnqueueAsync(request);

        for (int i = 0; i < 3; i++)
        {
            await processor.ProcessNextAsync();
        }

        var finalRequest = await dbContext.PaymentRequests.FindAsync(request.Id);
        var deadLetterItem = await dbContext.PaymentDeadLetters.FirstOrDefaultAsync(d => d.RequestId == request.Id);

        Assert.NotNull(finalRequest);
        Assert.Equal(PaymentStatus.DeadLetter, finalRequest.Status);
        Assert.NotNull(deadLetterItem);
        Assert.Contains("gateway failure", deadLetterItem.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessNextAsync_HandlesReferenceMissingPaymentRequest()
    {
        var dbContext = CreateDbContext();
        var queue = CreateMockQueue(dbContext);
        var logger = CreateMockLogger();
        var processor = new PaymentProcessor(dbContext, queue, logger);

        var orphanQueueItem = new PaymentQueueItem
        {
            Id = Guid.NewGuid(),
            RequestId = Guid.NewGuid(),
            EnqueuedAt = DateTime.UtcNow
        };

        dbContext.PaymentQueueItems.Add(orphanQueueItem);
        await dbContext.SaveChangesAsync();

        await processor.ProcessNextAsync();

        var markedItem = await dbContext.PaymentQueueItems.FindAsync(orphanQueueItem.Id);
        Assert.NotNull(markedItem);
        Assert.NotNull(markedItem.ProcessedAt);
    }
}
