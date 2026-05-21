using System.ComponentModel.DataAnnotations;

namespace payments.Models;

public enum ScheduleFrequency
{
    OneTime,
    Daily,
    Weekly,
    BiWeekly,
    Monthly,
    Quarterly,
    Annually
}

public enum ScheduleStatus
{
    Active,
    Inactive,
    Completed,
    Cancelled
}

public class Schedule
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    public ScheduleFrequency Frequency { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public ScheduleStatus Status { get; set; } = ScheduleStatus.Active;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation property
    public ICollection<SchedulePayment> SchedulePayments { get; set; } = new List<SchedulePayment>();
}
