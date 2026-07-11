using Microsoft.Extensions.Hosting;

namespace MyBudget.Features.SharedKernel.Services;

/// <summary>
/// Background service that purges expired audit log records.
/// Full implementation added in PR5.
/// This stub exists so PR1 DI registration compiles.
/// </summary>
public sealed class AuditRetentionService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
