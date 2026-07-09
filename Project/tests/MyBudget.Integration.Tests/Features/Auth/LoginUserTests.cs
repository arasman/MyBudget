using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.Auth;

/// <summary>Integration tests for POST /api/auth/login.</summary>
public sealed class LoginUserTests : IntegrationTestBase
{
    public LoginUserTests(IntegrationTestFactory factory) : base(factory) { }

    [Fact]
    public async Task ValidCredentials_Returns200_WithTokenPair()
    {
        await RegisterUserAsync("login@example.com");

        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email    = "login@example.com",
            password = "Password1",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginTestResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body.AccessToken.ShouldNotBeNullOrWhiteSpace();
        body.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task WrongPassword_Returns401()
    {
        await RegisterUserAsync("wrongpw@example.com");

        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email    = "wrongpw@example.com",
            password = "WrongPassword1",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnknownEmail_Returns401_SameShapeAsWrongPassword()
    {
        // No enumeration — same 401 for unknown email and wrong password
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email    = "ghost@example.com",
            password = "Password1",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SuccessfulLogin_UpdatesLastLoginAt()
    {
        await RegisterUserAsync("lastlogin@example.com");
        await LoginAsync("lastlogin@example.com");

        using var scope = Factory.Services.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.Single(u => u.Email == "lastlogin@example.com");
        user.LastLoginAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task SuccessfulLogin_AddsRefreshTokenRow()
    {
        await RegisterUserAsync("loginrt@example.com");

        var before = Factory.Services.CreateScope()
            .ServiceProvider.GetRequiredService<AppDbContext>()
            .RefreshTokens.Count();

        await LoginAsync("loginrt@example.com");

        using var scope = Factory.Services.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var after = db.RefreshTokens.Count();
        after.ShouldBeGreaterThan(before);
    }
}
