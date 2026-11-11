using Application.Common;
using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace WebApi.BackgroundServices;

/// <summary>
/// Runs once a day (00:05 Vietnam time) and on startup.
/// - Semester whose end date is today becomes Closed.
/// - Examination whose end date is today becomes Closed.
/// - Examination session whose start date has passed becomes ENDED.
/// </summary>
public sealed class StatusUpdateBackgroundService : BackgroundService
{
    private static readonly TimeSpan RunAt = new(0, 5, 0);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StatusUpdateBackgroundService> _logger;

    public StatusUpdateBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<StatusUpdateBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateStatusesAsync(stoppingToken);
                await Task.Delay(DelayUntilNextRun(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Daily status update failed; retrying in 10 minutes.");

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private static TimeSpan DelayUntilNextRun()
    {
        var now = VietnamTime.Now;
        var next = now.Date.Add(RunAt);
        if (next <= now)
            next = next.AddDays(1);

        return next - now;
    }

    private async Task UpdateStatusesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var now = VietnamTime.Now;
        var today = DateOnly.FromDateTime(now);
        var changed = 0;

        var semesters = await db.Semesters
            .Where(s => s.EndDate == today && s.Status != SemesterStatus.Closed)
            .ToListAsync(ct);

        foreach (var semester in semesters)
        {
            semester.Status = SemesterStatus.Closed;
            semester.UpdatedDay = now;
            changed++;
        }

        var examinations = await db.Examinations
            .Where(e => e.EndDate == today
                        && e.Status != ExaminationStatus.Closed
                        && e.Status != ExaminationStatus.Cancelled
                        && e.Status != ExaminationStatus.Completed)
            .ToListAsync(ct);

        foreach (var examination in examinations)
        {
            examination.Status = ExaminationStatus.Closed;
            examination.UpdatedDay = now;
            changed++;
        }

        var sessions = await db.ExaminationSessions
            .Where(s => s.StartDate < today
                        && s.Status != ExaminationSessionStatus.ENDED
                        && s.Status != ExaminationSessionStatus.CANCELLED)
            .ToListAsync(ct);

        foreach (var session in sessions)
        {
            session.Status = ExaminationSessionStatus.ENDED;
            session.UpdatedDay = now;
            changed++;
        }

        if (changed == 0)
        {
            _logger.LogInformation(
                "Daily status update: nothing to change for {Today}.", today);
            return;
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Daily status update: {Count} record(s) updated for {Today}.", changed, today);
    }
}
