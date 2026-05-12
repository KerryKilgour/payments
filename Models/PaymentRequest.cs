using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace payments.Models;

public enum PaymentStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    DeadLetter
}

public class PaymentRequest
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string ExternalId { get; set; } = null!;

    [Required]
    public decimal Amount { get; set; }

    [Required]
    public string Currency { get; set; } = null!;

    [Required]
    public string CardNumberHash { get; set; } = null!;

    [Required]
    public string CardExpiry { get; set; } = null!;

    [Required]
    public string CardHolderName { get; set; } = null!;

    public PaymentStatus Status { get; set; }

    public int RetryCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
