using System.ComponentModel.DataAnnotations;

namespace payments.DTO;

public class PaymentRequestDto
{
    [Required]
    public string ExternalId { get; set; } = null!;

    [Required]
    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }

    [Required]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Currency must be a 3-letter ISO code.")]
    public string Currency { get; set; } = null!;

    [Required]
    [CreditCard]
    public string CardNumber { get; set; } = null!;

    [Required]
    [RegularExpression("^(0[1-9]|1[0-2])\\/(?:[0-9]{2})$", ErrorMessage = "Expiry must be in MM/YY format.")]
    public string CardExpiry { get; set; } = null!;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string CardHolderName { get; set; } = null!;
}
