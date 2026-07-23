using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.Auth;

/// <summary>Integration tests for PATCH /api/auth/me/locale.</summary>
public sealed class UpdateLocaleTests : IntegrationTestBase
{
    public UpdateLocaleTests(IntegrationTestFactory factory) : base(factory) { }

    [Fact]
    public async Task ValidLocale_Returns204_AndUpdatesDatabase()
    {
        var login = await RegisterUserAsync("locale-valid@example.com");
        AuthorizeClient(login.AccessToken);

        var response = await Client.PatchAsJsonAsync("/api/auth/me/locale", new { locale = "es" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == "locale-valid@example.com");
        user.PreferredLocale.ShouldBe("es");
    }

    [Fact]
    public async Task UnsupportedLocale_Returns422_WithAuthLocaleUnsupportedDetail()
    {
        var login = await RegisterUserAsync("locale-unsupported@example.com");
        AuthorizeClient(login.AccessToken);

        var response = await Client.PatchAsJsonAsync("/api/auth/me/locale", new { locale = "fr" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("AUTH_LOCALE_UNSUPPORTED");
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        // No AuthorizeClient call — anonymous request
        var response = await Client.PatchAsJsonAsync("/api/auth/me/locale", new { locale = "es" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
