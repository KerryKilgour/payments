using System.ComponentModel.DataAnnotations;

namespace payments.Models;

public class PaymentDeadLetter
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid RequestId { get; set; }

    [Required]
    public string Reason { get; set; } = null!;

    [Required]
    public string Payload { get; set; } = null!;

    [Required]
    public DateTime CreatedAt { get; set; }
}
