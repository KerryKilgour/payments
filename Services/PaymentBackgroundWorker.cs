using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace payments.Services;

public class PaymentBackgroundWorker : BackgroundService
{
    private readonly IPaymentProcessor _paymentProcessor;
    private readonly ILogger<PaymentBackgroundWorker> _logger;

    public PaymentBackgroundWorker(IPaymentProcessor paymentProcessor, ILogger<PaymentBackgroundWorker> logger)
    {
        _paymentProcessor = paymentProcessor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Payment background worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _paymentProcessor.ProcessNextAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in payment background worker.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }

        _logger.LogInformation("Payment background worker stopping.");
    }
}
