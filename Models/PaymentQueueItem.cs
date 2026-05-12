using System.ComponentModel.DataAnnotations;

namespace payments.Models;

public class PaymentQueueItem
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid RequestId { get; set; }

    [Required]
    public DateTime EnqueuedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}
