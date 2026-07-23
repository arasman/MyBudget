using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyBudget.Features.SharedKernel.Persistence;

namespace MyBudget.Features.Features.Testing;

/// <summary>
/// Guarded reset endpoint — only registered in Testing and E2E environments.
/// Wipes and re-migrates the database so test harnesses start with a clean schema.
/// This route does NOT exist in Development or Production (the guard is at registration time).
/// </summary>
public static class TestResetEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        var env = app.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        if (!env.IsEnvironment("Testing") && !env.IsEnvironment("E2E"))
            return app;

        app.MapPost("/api/test/reset", Handle)
            .WithTags("Testing")
            .WithName("ResetTestDatabase")
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> Handle(AppDbContext db)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("DROP SCHEMA public CASCADE; CREATE SCHEMA public;");
            await db.Database.MigrateAsync();
            return Results.Ok();
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                title: "Database reset failed",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
