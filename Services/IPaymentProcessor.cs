using payments.Models;

namespace payments.Services;

public interface IPaymentProcessor
{
    Task ProcessNextAsync(CancellationToken cancellationToken = default);
}
