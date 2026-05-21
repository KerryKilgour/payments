using Microsoft.AspNetCore.Mvc;
using payments.Data;
using payments.Models;

namespace payments.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScheduleController : ControllerBase
{
    private readonly PaymentDbContext _dbContext;
    private readonly ILogger<ScheduleController> _logger;

    public ScheduleController(PaymentDbContext dbContext, ILogger<ScheduleController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateSchedule([FromBody] CreateScheduleRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Frequency = request.Frequency,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = ScheduleStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Schedules.Add(schedule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Schedule created: Id={Id}, Name={Name}, Frequency={Frequency}", schedule.Id, schedule.Name, schedule.Frequency);

        return CreatedAtAction(nameof(GetSchedule), new { id = schedule.Id }, new
        {
            schedule.Id,
            schedule.Name,
            schedule.Description,
            schedule.Frequency,
            schedule.StartDate,
            schedule.EndDate,
            schedule.Status,
            schedule.CreatedAt,
            schedule.UpdatedAt
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSchedule(Guid id, CancellationToken cancellationToken)
    {
        var schedule = await _dbContext.Schedules
            .Include(s => s.SchedulePayments)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (schedule is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            schedule.Id,
            schedule.Name,
            schedule.Description,
            schedule.Frequency,
            schedule.StartDate,
            schedule.EndDate,
            schedule.Status,
            SchedulePaymentCount = schedule.SchedulePayments.Count,
            schedule.CreatedAt,
            schedule.UpdatedAt
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetSchedules([FromQuery] ScheduleStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Schedules.Include(s => s.SchedulePayments).AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        var schedules = await query.ToListAsync(cancellationToken);

        return Ok(schedules.Select(s => new
        {
            s.Id,
            s.Name,
            s.Description,
            s.Frequency,
            s.StartDate,
            s.EndDate,
            s.Status,
            SchedulePaymentCount = s.SchedulePayments.Count,
            s.CreatedAt,
            s.UpdatedAt
        }));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSchedule(Guid id, [FromBody] UpdateScheduleRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var schedule = await _dbContext.Schedules.FindAsync(new object[] { id }, cancellationToken: cancellationToken);
        if (schedule is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            schedule.Name = request.Name;
        }

        if (request.Description != null)
        {
            schedule.Description = request.Description;
        }

        if (request.Frequency.HasValue)
        {
            schedule.Frequency = request.Frequency.Value;
        }

        if (request.EndDate.HasValue)
        {
            schedule.EndDate = request.EndDate.Value;
        }

        if (request.Status.HasValue)
        {
            schedule.Status = request.Status.Value;
        }

        schedule.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Schedule updated: Id={Id}", id);

        return Ok(new
        {
            schedule.Id,
            schedule.Name,
            schedule.Description,
            schedule.Frequency,
            schedule.StartDate,
            schedule.EndDate,
            schedule.Status,
            schedule.UpdatedAt
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSchedule(Guid id, CancellationToken cancellationToken)
    {
        var schedule = await _dbContext.Schedules
            .Include(s => s.SchedulePayments)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (schedule is null)
        {
            return NotFound();
        }

        _dbContext.Schedules.Remove(schedule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Schedule deleted: Id={Id}, PaymentsDeleted={Count}", id, schedule.SchedulePayments.Count);

        return NoContent();
    }
}

public class CreateScheduleRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public ScheduleFrequency Frequency { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class UpdateScheduleRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public ScheduleFrequency? Frequency { get; set; }
    public DateTime? EndDate { get; set; }
    public ScheduleStatus? Status { get; set; }
}
