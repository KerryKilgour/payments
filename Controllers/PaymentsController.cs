using Microsoft.AspNetCore.Mvc;
using payments.Data;
using payments.DTO;
using payments.Models;
using payments.Services;

namespace payments.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentDbContext _dbContext;
    private readonly IPaymentQueue _paymentQueue;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(PaymentDbContext dbContext, IPaymentQueue paymentQueue, ILogger<PaymentsController> logger)
    {
        _dbContext = dbContext;
        _paymentQueue = paymentQueue;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePayment([FromBody] PaymentRequestDto payload, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var existing = await _dbContext.PaymentRequests.SingleOrDefaultAsync(p => p.ExternalId == payload.ExternalId, cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation("Duplicate payment request ignored: {ExternalId}", payload.ExternalId);
            return Conflict(new { existing.Id, existing.Status, existing.RetryCount });
        }

        var request = new PaymentRequest
        {
            Id = Guid.NewGuid(),
            ExternalId = payload.ExternalId,
            Amount = payload.Amount,
            Currency = payload.Currency,
            CardNumberHash = HashCard(payload.CardNumber),
            CardExpiry = payload.CardExpiry,
            CardHolderName = payload.CardHolderName,
            Status = PaymentStatus.Pending,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _paymentQueue.EnqueueAsync(request, cancellationToken);

        return Accepted(new { request.Id, request.ExternalId, request.Status });
    }

    [HttpGet("{externalId}")]
    public async Task<IActionResult> GetPaymentStatus(string externalId, CancellationToken cancellationToken)
    {
        var request = await _dbContext.PaymentRequests.SingleOrDefaultAsync(p => p.ExternalId == externalId, cancellationToken);
        if (request is null)
        {
            return NotFound();
        }

        return Ok(new { request.Id, request.ExternalId, request.Status, request.RetryCount, request.UpdatedAt });
    }

    private static string HashCard(string cardNumber)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(cardNumber);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
