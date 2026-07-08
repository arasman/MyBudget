using Microsoft.EntityFrameworkCore;
using MyBudget.Api.Middleware;
using MyBudget.Features.Extensions;
using MyBudget.Features.SharedKernel.Persistence;
using Serilog;

// 1. Serilog — must be first to capture all subsequent log output
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration));

// 2. AddFeatures — registers EF, Mediator, Behaviours, Localization,
//    Email channel/background-service, OTel, ICacheService (Null)
builder.Services.AddFeatures(builder.Configuration);

// 3. AddAuthentication / AddAuthorization — stubs only (JWT wired in auth change)
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// 4. OpenAPI (Scalar UI)
builder.Services.AddOpenApi();

var app = builder.Build();

// 5. MigrateAsync — runs before any request is served (ADR-003)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// 6. UseRequestLocalization — before auth so error messages are already localized
app.UseRequestLocalization();

// 7. UseAuthentication + UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

// 8. CorrelationIdMiddleware — stamps X-Correlation-Id on every request
app.UseMiddleware<CorrelationIdMiddleware>();

// 9. ExceptionMiddleware — catches unhandled exceptions, returns ProblemDetails
app.UseMiddleware<ExceptionMiddleware>();

// 10. MapAllSliceEndpoints — reflection scans MyBudget.Features for static Map()
app.MapAllSliceEndpoints();

// 11. MapOpenApi — dev only (Scalar UI)
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

await app.RunAsync();
