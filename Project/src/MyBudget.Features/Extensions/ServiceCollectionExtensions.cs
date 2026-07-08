using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.Behaviours;
using MyBudget.Features.SharedKernel.Caching;
using MyBudget.Features.SharedKernel.Email;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Telemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MyBudget.Features.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFeatures(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var assembly = typeof(ServiceCollectionExtensions).Assembly;

        // Mediator with pipeline behaviours (order: Validation → Logging → Caching → Handler)
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });

        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CachingBehaviour<,>));

        // FluentValidation — scan all validators from Features assembly
        services.AddValidatorsFromAssembly(assembly);

        // EF Core — Npgsql provider (connection string comes from User Secrets / env vars)
        services.AddDbContext<AppDbContext>(opts =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection is not configured. " +
                    "Use dotnet user-secrets or set the environment variable.");

            opts.UseNpgsql(connectionString);
        });

        // Dapper connection factory (keyed as "postgres")
        services.AddSingleton<ConnectionFactory>();

        // Caching — NullCacheService at foundation (ADR-005)
        services.AddSingleton<ICacheService, NullCacheService>();

        // Email — channel-based fire-and-forget
        services.AddSingleton<EmailChannel>();
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<EmailChannel>());
        services.AddHostedService<EmailBackgroundService>();

        // Localization
        services.AddLocalization();

        // OpenTelemetry
        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyBudget.Api"))
                    .AddSource(SliceActivitySource.Name)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter();
            });

        return services;
    }
}
