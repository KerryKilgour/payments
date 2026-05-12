using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using payments.Controllers;
using payments.Data;
using payments.DTO;
using payments.Models;
using payments.Services;
using Xunit;

namespace payments.tests.Controllers;

public class PaymentsControllerTests
{
    private PaymentDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PaymentDbContext(options);
    }

    private PaymentsController CreateController(PaymentDbContext dbContext, IPaymentQueue queue, ILogger<PaymentsController> logger)
    {
        return new PaymentsController(dbContext, queue, logger);
    }

    private ILogger<PaymentsController> CreateMockLogger()
    {
        return new Mock<ILogger<PaymentsController>>().Object;
    }

    [Fact]
    public async Task CreatePayment_AcceptsValidPaymentRequest()
    {
        var dbContext = CreateDbContext();
        var queue = new PaymentQueue(dbContext);
        var logger = CreateMockLogger();
        var controller = CreateController(dbContext, queue, logger);

        var dto = new PaymentRequestDto
        {
            ExternalId = "ext-123",
            Amount = 99.99m,
            Currency = "USD",
            CardNumber = "4532015112830366",
            CardExpiry = "12/25",
            CardHolderName = "John Doe"
        };

        var result = await controller.CreatePayment(dto);

        var acceptResult = Assert.IsType<AcceptedResult>(result);
        Assert.NotNull(acceptResult.Value);

        var savedRequest = await dbContext.PaymentRequests.SingleOrDefaultAsync(p => p.ExternalId == "ext-123");
        Assert.NotNull(savedRequest);
        Assert.Equal(PaymentStatus.Pending, savedRequest.Status);
    }

    [Fact]
    public async Task CreatePayment_EnqueuesPaymentForProcessing()
    {
        var dbContext = CreateDbContext();
        var queue = new PaymentQueue(dbContext);
        var logger = CreateMockLogger();
        var controller = CreateController(dbContext, queue, logger);

        var dto = new PaymentRequestDto
        {
            ExternalId = "ext-456",
            Amount = 50.00m,
            Currency = "EUR",
            CardNumber = "4532015112830366",
            CardExpiry = "06/27",
            CardHolderName = "Jane Smith"
        };

        await controller.CreatePayment(dto);

        var queueItem = await dbContext.PaymentQueueItems.FirstOrDefaultAsync();
        Assert.NotNull(queueItem);
    }

    [Fact]
    public async Task CreatePayment_HashesCardNumber()
    {
        var dbContext = CreateDbContext();
        var queue = new PaymentQueue(dbContext);
        var logger = CreateMockLogger();
        var controller = CreateController(dbContext, queue, logger);

        var dto = new PaymentRequestDto
        {
            ExternalId = "ext-789",
            Amount = 150.00m,
            Currency = "GBP",
            CardNumber = "4532015112830366",
            CardExpiry = "03/26",
            CardHolderName = "Bob Johnson"
        };

        await controller.CreatePayment(dto);

        var savedRequest = await dbContext.PaymentRequests.SingleOrDefaultAsync(p => p.ExternalId == "ext-789");
        Assert.NotNull(savedRequest);
        Assert.NotEqual("4532015112830366", savedRequest.CardNumberHash);
        Assert.NotEmpty(savedRequest.CardNumberHash);
    }

    [Fact]
    public async Task CreatePayment_ReturnConflictForDuplicateExternalId()
    {
        var dbContext = CreateDbContext();
        var queue = new PaymentQueue(dbContext);
        var logger = CreateMockLogger();
        var controller = CreateController(dbContext, queue, logger);

        var dto1 = new PaymentRequestDto
        {
            ExternalId = "dup-123",
            Amount = 100m,
            Currency = "USD",
            CardNumber = "4532015112830366",
            CardExpiry = "12/25",
            CardHolderName = "First User"
        };

        var dto2 = new PaymentRequestDto
        {
            ExternalId = "dup-123",
            Amount = 200m,
            Currency = "USD",
            CardNumber = "4532015112830366",
            CardExpiry = "12/25",
            CardHolderName = "Second User"
        };

        await controller.CreatePayment(dto1);
        var result = await controller.CreatePayment(dto2);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.NotNull(conflictResult.Value);
    }

    [Fact]
    public async Task CreatePayment_ReturnsValidationErrorForInvalidAmount()
    {
        var dbContext = CreateDbContext();
        var queue = new PaymentQueue(dbContext);
        var logger = CreateMockLogger();
        var controller = CreateController(dbContext, queue, logger);

        var dto = new PaymentRequestDto
        {
            ExternalId = "ext-invalid",
            Amount = 0m,
            Currency = "USD",
            CardNumber = "4532015112830366",
            CardExpiry = "12/25",
            CardHolderName = "John Doe"
        };

        var result = await controller.CreatePayment(dto);

        var validationResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(validationResult.Value);
    }

    [Fact]
    public async Task CreatePayment_ReturnsValidationErrorForInvalidCurrency()
    {
        var dbContext = CreateDbContext();
        var queue = new PaymentQueue(dbContext);
        var logger = CreateMockLogger();
        var controller = CreateController(dbContext, queue, logger);

        var dto = new PaymentRequestDto
        {
            ExternalId = "ext-bad-currency",
            Amount = 100m,
            Currency = "INVALID",
            CardNumber = "4532015112830366",
            CardExpiry = "12/25",
            CardHolderName = "John Doe"
        };

        var result = await controller.CreatePayment(dto);

        var validationResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(validationResult.Value);
    }

    [Fact]
    public async Task GetPaymentStatus_ReturnsPaymentStatus()
    {
        var dbContext = CreateDbContext();
        var queue = new PaymentQueue(dbContext);
        var logger = CreateMockLogger();
        var controller = CreateController(dbContext, queue, logger);

        var request = new PaymentRequest
        {
            Id = Guid.NewGuid(),
            ExternalId = "status-check",
            Amount = 75m,
            Currency = "USD",
            CardNumberHash = "hash123",
            CardExpiry = "12/25",
            CardHolderName = "Test User",
            Status = PaymentStatus.Completed,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.PaymentRequests.Add(request);
        await dbContext.SaveChangesAsync();

        var result = await controller.GetPaymentStatus("status-check");

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetPaymentStatus_ReturnsNotFoundForMissingPayment()
    {
        var dbContext = CreateDbContext();
        var queue = new PaymentQueue(dbContext);
        var logger = CreateMockLogger();
        var controller = CreateController(dbContext, queue, logger);

        var result = await controller.GetPaymentStatus("non-existent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetPaymentStatus_IncludesRetryCountInResponse()
    {
        var dbContext = CreateDbContext();
        var queue = new PaymentQueue(dbContext);
        var logger = CreateMockLogger();
        var controller = CreateController(dbContext, queue, logger);

        var request = new PaymentRequest
        {
            Id = Guid.NewGuid(),
            ExternalId = "retry-check",
            Amount = 100m,
            Currency = "USD",
            CardNumberHash = "hash123",
            CardExpiry = "12/25",
            CardHolderName = "Test User",
            Status = PaymentStatus.Pending,
            RetryCount = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.PaymentRequests.Add(request);
        await dbContext.SaveChangesAsync();

        var result = await controller.GetPaymentStatus("retry-check");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value as dynamic;
        Assert.NotNull(value);
        Assert.Equal(2, value.RetryCount);
    }
}
