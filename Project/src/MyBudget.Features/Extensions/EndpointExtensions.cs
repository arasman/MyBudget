using System.Reflection;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Extensions;

public static class EndpointExtensions
{
    /// <summary>
    /// Scans MyBudget.Features assembly for types with a public static Map(IEndpointRouteBuilder)
    /// method and invokes them. This auto-discovers all slice endpoints without manual registration.
    /// </summary>
    public static IEndpointRouteBuilder MapAllSliceEndpoints(this IEndpointRouteBuilder app)
    {
        var assembly = typeof(EndpointExtensions).Assembly;

        var endpointTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Select(t => new
            {
                Type = t,
                Method = t.GetMethod(
                    "Map",
                    BindingFlags.Public | BindingFlags.Static,
                    [typeof(IEndpointRouteBuilder)])
            })
            .Where(x => x.Method is not null)
            .ToList();

        foreach (var endpoint in endpointTypes)
        {
            endpoint.Method!.Invoke(null, [app]);
        }

        return app;
    }
}
