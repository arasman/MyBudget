using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.Auth;

/// <summary>
/// Integration tests for POST /api/auth/register.
/// Requires Docker Compose Postgres running on localhost:5432.
/// </summary>
public sealed class RegisterUserTests : IntegrationTestBase
{
    public RegisterUserTests(IntegrationTestFactory factory) : base(factory) { }

    [Fact]
    public async Task ValidPayload_Returns201_WithTokensAndUserProfile()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "new@example.com",
            password = "Password1",
            firstName = "Alice",
            lastName  = "Smith",
            preferredLocale = "en",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<LoginTestResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body.AccessToken.ShouldNotBeNullOrWhiteSpace();
        body.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        body.User.Email.ShouldBe("new@example.com");
    }

    [Fact]
    public async Task DuplicateEmail_Returns409_WithAuthEmailTaken()
    {
        await RegisterUserAsync("dup@example.com");

        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "dup@example.com",
            password = "Password1",
            firstName = "Bob",
            lastName  = "Jones",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("AUTH_EMAIL_TAKEN");
    }

    [Fact]
    public async Task WeakPassword_Returns422()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "weak@example.com",
            password = "abc123", // no uppercase
            firstName = "Test",
            lastName  = "User",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task MissingFirstName_Returns422()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "nofn@example.com",
            password = "Password1",
            firstName = "",
            lastName  = "User",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UnsupportedLocale_Returns422()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "fr@example.com",
            password = "Password1",
            firstName = "Test",
            lastName  = "User",
            preferredLocale = "fr",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SuccessfulRegister_CreatesUserBudgetAndMembership()
    {
        await RegisterUserAsync("db@example.com");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = db.Users.SingleOrDefault(u => u.Email == "db@example.com");
        user.ShouldNotBeNull();

        var budget = db.Budgets.SingleOrDefault(b => b.OwnerId == user.Id);
        budget.ShouldNotBeNull();

        var membership = db.BudgetMemberships.SingleOrDefault(m =>
            m.UserId == user.Id && m.BudgetId == budget.Id);
        membership.ShouldNotBeNull();
    }

    [Fact]
    public async Task SuccessfulRegister_CreatesRefreshTokenRow()
    {
        await RegisterUserAsync("rt@example.com");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user  = db.Users.Single(u => u.Email == "rt@example.com");
        var token = db.RefreshTokens.SingleOrDefault(t => t.UserId == user.Id);
        token.ShouldNotBeNull();
    }
}
