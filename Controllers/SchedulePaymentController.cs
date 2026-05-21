using Microsoft.AspNetCore.Mvc;
using payments.Data;
using payments.Models;

namespace payments.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchedulePaymentController : ControllerBase
{
    private readonly PaymentDbContext _dbContext;
    private readonly ILogger<SchedulePaymentController> _logger;

    public SchedulePaymentController(PaymentDbContext dbContext, ILogger<SchedulePaymentController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost("schedule")]
    public async Task<IActionResult> CreateSchedulePayment([FromBody] CreateSchedulePaymentRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // Validate that the payment request exists
        var paymentRequest = await _dbContext.PaymentRequests.FindAsync(new object[] { request.PaymentRequestId }, cancellationToken: cancellationToken);
        if (paymentRequest is null)
        {
            return NotFound(new { message = "Payment request not found" });
        }

        // Validate that the schedule exists
        var schedule = await _dbContext.Schedules.FindAsync(new object[] { request.ScheduleId }, cancellationToken: cancellationToken);
        if (schedule is null)
        {
            return NotFound(new { message = "Schedule not found" });
        }

        // Check for duplicate
        var existing = await _dbContext.SchedulePayments
            .FirstOrDefaultAsync(sp => sp.ScheduleId == request.ScheduleId && sp.PaymentRequestId == request.PaymentRequestId, cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation("Duplicate schedule payment ignored: ScheduleId={ScheduleId}, PaymentRequestId={PaymentRequestId}", request.ScheduleId, request.PaymentRequestId);
            return Conflict(new { existing.Id, existing.Status });
        }

        var schedulePayment = new SchedulePayment
        {
            Id = Guid.NewGuid(),
            ScheduleId = request.ScheduleId,
            PaymentRequestId = request.PaymentRequestId,
            NextScheduledDate = request.NextScheduledDate,
            Status = SchedulePaymentStatus.Pending,
            ExecutionCount = 0,
            MaxExecutions = request.MaxExecutions,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.SchedulePayments.Add(schedulePayment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Schedule payment created: Id={Id}, ScheduleId={ScheduleId}, PaymentRequestId={PaymentRequestId}", schedulePayment.Id, schedulePayment.ScheduleId, schedulePayment.PaymentRequestId);

        return CreatedAtAction(nameof(GetSchedulePayment), new { id = schedulePayment.Id }, new { schedulePayment.Id, schedulePayment.ScheduleId, schedulePayment.PaymentRequestId, schedulePayment.NextScheduledDate, schedulePayment.Status });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSchedulePayment(Guid id, CancellationToken cancellationToken)
    {
        var schedulePayment = await _dbContext.SchedulePayments
            .Include(sp => sp.Schedule)
            .Include(sp => sp.PaymentRequest)
            .FirstOrDefaultAsync(sp => sp.Id == id, cancellationToken);

        if (schedulePayment is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            schedulePayment.Id,
            schedulePayment.ScheduleId,
            schedulePayment.PaymentRequestId,
            Schedule = new { schedulePayment.Schedule.Id, schedulePayment.Schedule.Name, schedulePayment.Schedule.Frequency, schedulePayment.Schedule.Status },
            PaymentRequest = new { schedulePayment.PaymentRequest.Id, schedulePayment.PaymentRequest.ExternalId, schedulePayment.PaymentRequest.Amount, schedulePayment.PaymentRequest.Status },
            schedulePayment.NextScheduledDate,
            schedulePayment.LastExecutedDate,
            schedulePayment.Status,
            schedulePayment.ExecutionCount,
            schedulePayment.MaxExecutions,
            schedulePayment.CreatedAt,
            schedulePayment.UpdatedAt
        });
    }

    [HttpGet("schedule/{scheduleId}")]
    public async Task<IActionResult> GetSchedulePaymentsBySchedule(Guid scheduleId, CancellationToken cancellationToken)
    {
        var schedule = await _dbContext.Schedules.FindAsync(new object[] { scheduleId }, cancellationToken: cancellationToken);
        if (schedule is null)
        {
            return NotFound(new { message = "Schedule not found" });
        }

        var schedulePayments = await _dbContext.SchedulePayments
            .Where(sp => sp.ScheduleId == scheduleId)
            .Include(sp => sp.PaymentRequest)
            .ToListAsync(cancellationToken);

        return Ok(schedulePayments.Select(sp => new
        {
            sp.Id,
            sp.ScheduleId,
            sp.PaymentRequestId,
            PaymentRequest = new { sp.PaymentRequest.Id, sp.PaymentRequest.ExternalId, sp.PaymentRequest.Amount },
            sp.NextScheduledDate,
            sp.Status,
            sp.ExecutionCount
        }));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSchedulePayment(Guid id, CancellationToken cancellationToken)
    {
        var schedulePayment = await _dbContext.SchedulePayments.FindAsync(new object[] { id }, cancellationToken: cancellationToken);
        if (schedulePayment is null)
        {
            return NotFound();
        }

        _dbContext.SchedulePayments.Remove(schedulePayment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Schedule payment deleted: Id={Id}", id);

        return NoContent();
    }
}

public class CreateSchedulePaymentRequest
{
    public Guid ScheduleId { get; set; }
    public Guid PaymentRequestId { get; set; }
    public DateTime NextScheduledDate { get; set; }
    public int? MaxExecutions { get; set; }
}
