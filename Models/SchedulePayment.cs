using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace payments.Models;

public enum SchedulePaymentStatus
{
    Pending,
    Scheduled,
    Processing,
    Completed,
    Failed,
    Skipped,
    Cancelled
}

public class SchedulePayment
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [ForeignKey(nameof(Schedule))]
    public Guid ScheduleId { get; set; }

    [Required]
    [ForeignKey(nameof(PaymentRequest))]
    public Guid PaymentRequestId { get; set; }

    [Required]
    public DateTime NextScheduledDate { get; set; }

    public DateTime? LastExecutedDate { get; set; }

    public SchedulePaymentStatus Status { get; set; } = SchedulePaymentStatus.Pending;

    public int ExecutionCount { get; set; } = 0;

    public int? MaxExecutions { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Schedule Schedule { get; set; } = null!;
    public PaymentRequest PaymentRequest { get; set; } = null!;
}
