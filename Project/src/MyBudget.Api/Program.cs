using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyBudget.Api.Middleware;
using MyBudget.Features.Extensions;
using MyBudget.Features.SharedKernel.Auth;
using MyBudget.Features.SharedKernel.Auth.Authorization;
using MyBudget.Features.SharedKernel.Persistence;
using Serilog;

// Register Dapper type handlers for DateOnly (Npgsql 10 maps PostgreSQL date as DateOnly)
DapperTypeHandlers.RegisterAll();

// 1. Serilog — must be first to capture all subsequent log output
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration));

// 2. Configure JSON serialization — use string enum names (e.g. "Expense" not 0)
builder.Services.ConfigureHttpJsonOptions(opts =>
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// 3. AddFeatures — registers EF, Mediator, Behaviours, Localization,
//    Email channel/background-service, OTel, ICacheService (Null), JwtOptions, BudgetAuthorizationHandler
builder.Services.AddFeatures(builder.Configuration);

// 3. Startup guard — fail fast if JWT:Key is missing or empty (SC-1)
var jwtOpts = builder.Configuration.GetSection("JWT").Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT section is not configured.");
if (string.IsNullOrWhiteSpace(jwtOpts.Key))
    throw new InvalidOperationException(
        "JWT__Key is not configured. Set it via User Secrets (dev) or environment variable (prod).");

// 4. JWT Bearer authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtOpts.Issuer,
            ValidAudience            = jwtOpts.Audience,
            IssuerSigningKey         = new SymmetricSecurityKey(
                                          Encoding.UTF8.GetBytes(jwtOpts.Key)),
            ClockSkew                = TimeSpan.Zero,
        };
    });

// 5. Authorization with per-budget policies
builder.Services.AddAuthorization(opts => opts.AddBudgetPolicies());

// 6. OpenAPI (Scalar UI)
builder.Services.AddOpenApi();

var app = builder.Build();

// 7. MigrateAsync — runs before any request is served (skipped in Testing; factory handles it)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// 8. UseRequestLocalization — before auth so error messages are already localized
app.UseRequestLocalization();

// 9. UseAuthentication + UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

// 10. CorrelationIdMiddleware — stamps X-Correlation-Id on every request
app.UseMiddleware<CorrelationIdMiddleware>();

// 11. ExceptionMiddleware — catches unhandled exceptions, returns ProblemDetails
app.UseMiddleware<ExceptionMiddleware>();

// 12. MapAllSliceEndpoints — reflection scans MyBudget.Features for static Map()
app.MapAllSliceEndpoints();

// 13. MapOpenApi — dev only (Scalar UI)
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

await app.RunAsync();

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program { }
