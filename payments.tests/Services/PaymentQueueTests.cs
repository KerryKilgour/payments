using Microsoft.EntityFrameworkCore;
using payments.Data;
using payments.Models;
using payments.Services;
using Xunit;

namespace payments.tests.Services;

public class PaymentQueueTests
{
    private PaymentDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PaymentDbContext(options);
    }

    [Fact]
    public async Task EnqueueAsync_AddsNewPaymentRequest()
    {
        var dbContext = CreateDbContext();
        var queue = new PaymentQueue(dbContext);

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

        var savedRequest = await dbContext.PaymentRequests.SingleOrDefaultAsync(p => p.ExternalId == "ext-123");
        var queueItem = await dbContext.PaymentQueueItems.SingleOrDefaultAsync(q => q.RequestId == request.Id);

        Assert.NotNull(savedRequest);
        Assert.NotNull(queueItem);
        Assert.Equal(request.Amount, savedRequest.Amount);
    }

    [Fact]
    public async Task EnqueueAsync_IgnoresDuplicateExternalId()
    {
        var dbContext = CreateDbContext();
        var queue = new PaymentQueue(dbContext);

        var request1 = new PaymentRequest
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

        await queue.EnqueueAsync(request1);

        var request2 = new PaymentRequest
        {
            Id = Guid.NewGuid(),
            ExternalId = "ext-123",
            Amount = 200,
            Currency = "USD",
            CardNumberHash = "hash2",
            CardExpiry = "12/26",
            CardHolderName = "Jane Doe",
            Status = PaymentStatus.Pending,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await queue.EnqueueAsync(request2);

        var queueItems = await dbContext.PaymentQueueItems.ToListAsync();
        var savedRequests = await dbContext.PaymentRequests.ToListAsync();

        Assert.Single(queueItems);
        Assert.Single(savedRequests);
        Assert.Equal(100, savedRequests[0].Amount);
    }

    [Fact]
    public async Task DequeueNextAsync_ReturnsOldestQueueItem()
    {
        var dbContext = CreateDbContext();
        var queue = new PaymentQueue(dbContext);

        var request1 = new PaymentRequest
        {
            Id = Guid.NewGuid(),
            ExternalId = "ext-001",
            Amount = 100,
            Currency = "USD",
            CardNumberHash = "hash1",
            CardExpiry = "12/25",
            CardHolderName = "John",
            Status = PaymentStatus.Pending,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var request2 = new PaymentRequest
        {
            Id = Guid.NewGuid(),
            ExternalId = "ext-002",
            Amount = 200,
            Currency = "USD",
            CardNumberHash = "hash2",
            CardExpiry = "12/25",
            CardHolderName = "Jane",
            Status = PaymentStatus.Pending,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await queue.EnqueueAsync(request1);
        await Task.Delay(10);
        await queue.EnqueueAsync(request2);

        var dequeued = await queue.DequeueNextAsync();

        Assert.NotNull(dequeued);
        Assert.Equal(request1.Id, dequeued.RequestId);
    }

    [Fact]
    public async Task MarkProcessedAsync_UpdatesProcessedAt()
    {
        var dbContext = CreateDbContext();
        var queue = new PaymentQueue(dbContext);

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
        var queueItem = await queue.DequeueNextAsync();

        Assert.NotNull(queueItem);
        Assert.Null(queueItem.ProcessedAt);

        await queue.MarkProcessedAsync(queueItem);

        var updatedItem = await dbContext.PaymentQueueItems.FindAsync(queueItem.Id);
        Assert.NotNull(updatedItem);
        Assert.NotNull(updatedItem.ProcessedAt);
    }
}
