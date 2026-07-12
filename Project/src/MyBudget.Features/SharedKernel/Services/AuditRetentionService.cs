using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Persistence;

namespace MyBudget.Features.SharedKernel.Services;

/// <summary>
/// Background service that purges expired audit log records once per day.
/// Uses <see cref="IAuditRetentionPolicy"/> to determine the TTL.
/// Scoped <see cref="AppDbContext"/> is resolved per tick via <see cref="IServiceScopeFactory"/>.
/// </summary>
public sealed class AuditRetentionService : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory    _scopeFactory;
    private readonly IAuditRetentionPolicy   _retentionPolicy;
    private readonly ILogger<AuditRetentionService> _logger;
    private PeriodicTimer? _timer;
    private Task?          _runningTask;
    private CancellationTokenSource? _cts;

    public AuditRetentionService(
        IServiceScopeFactory    scopeFactory,
        IAuditRetentionPolicy   retentionPolicy,
        ILogger<AuditRetentionService> logger)
    {
        _scopeFactory    = scopeFactory;
        _retentionPolicy = retentionPolicy;
        _logger          = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts   = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _timer = new PeriodicTimer(TimeSpan.FromHours(24));
        _runningTask = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
            await _cts.CancelAsync();

        if (_runningTask is not null)
        {
            try { await _runningTask; }
            catch (OperationCanceledException) { /* expected */ }
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _cts?.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        if (_timer is null)
            return;

        while (await _timer.WaitForNextTickAsync(ct))
        {
            await ExecuteCleanupAsync(ct);
        }
    }

    /// <summary>
    /// Performs a single retention cleanup pass.
    /// Exposed as <c>public</c> so integration tests can invoke it directly
    /// without waiting for the 24-hour timer to fire.
    /// </summary>
    public async Task ExecuteCleanupAsync(CancellationToken ct = default)
    {
        var retentionDays = _retentionPolicy.GetRetentionDays();
        var cutoff        = DateTimeOffset.UtcNow.AddDays(-retentionDays);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var auditDeleted    = await db.AuditLogs
                .Where(a => a.Timestamp < cutoff)
                .ExecuteDeleteAsync(ct);

            var securityDeleted = await db.SecurityAuditLogs
                .Where(s => s.Timestamp < cutoff)
                .ExecuteDeleteAsync(ct);

            _logger.LogInformation(
                "Retention cleanup: deleted {AuditCount} AuditLog rows, {SecurityCount} SecurityAuditLog rows older than {Days} days",
                auditDeleted, securityDeleted, retentionDays);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Retention cleanup failed");
        }
    }
}
