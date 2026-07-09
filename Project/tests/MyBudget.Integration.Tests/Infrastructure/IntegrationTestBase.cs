using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyBudget.Integration.Tests.Infrastructure;

[Collection("Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly IntegrationTestFactory Factory;
    protected readonly HttpClient Client;
    protected static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    protected IntegrationTestBase(IntegrationTestFactory factory)
    {
        Factory = factory;
        Client  = factory.CreateClient();
        Client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());
    }

    public async Task InitializeAsync() => await Factory.CleanDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Registers a user and returns the login response.</summary>
    protected async Task<LoginTestResponse> RegisterUserAsync(
        string email    = "test@example.com",
        string password = "Password1",
        string firstName = "Test",
        string lastName  = "User")
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email, password, firstName, lastName, preferredLocale = "en",
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginTestResponse>(JsonOpts);
        return body!;
    }

    /// <summary>Logs in and returns the login response.</summary>
    protected async Task<LoginTestResponse> LoginAsync(
        string email = "test@example.com", string password = "Password1")
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginTestResponse>(JsonOpts))!;
    }

    /// <summary>Attaches an access token to the default client headers.</summary>
    protected void AuthorizeClient(string accessToken)
    {
        Client.DefaultRequestHeaders.Remove("Authorization");
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
    }
}

public sealed record LoginTestResponse(
    string AccessToken,
    string RefreshToken,
    int    ExpiresIn,
    UserTestProfile User);

public sealed record UserTestProfile(
    Guid   Id,
    string Email,
    string FirstName,
    string LastName,
    string PreferredLocale);
