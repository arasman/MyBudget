using System.Diagnostics;

namespace MyBudget.Features.SharedKernel.Telemetry;

/// <summary>
/// Shared OpenTelemetry ActivitySource for all slice handlers.
/// Used by LoggingBehaviour to create spans per request.
/// </summary>
public static class SliceActivitySource
{
    public const string Name = "MyBudget.Features";

    public static readonly ActivitySource Source = new(Name, "1.0.0");
}
