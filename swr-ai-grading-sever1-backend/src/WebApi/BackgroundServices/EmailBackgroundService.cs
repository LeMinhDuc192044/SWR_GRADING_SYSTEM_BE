using Application.Common.Interfaces;
using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WebApi.BackgroundServices;

public sealed class EmailBackgroundService : BackgroundService
{
    private readonly IEmailBackgroundQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailBackgroundService> _logger;

    public EmailBackgroundService(
        IEmailBackgroundQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<EmailBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailBackgroundService started.");

        await foreach (var message in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                await emailService.SendEmailAsync(message.ToEmail, message.Subject, message.HtmlBody, stoppingToken);
                _logger.LogInformation("Background email sent successfully to {ToEmail}", message.ToEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send background email to {ToEmail}", message.ToEmail);
            }
        }
    }
}
