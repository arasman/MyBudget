using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using MyBudget.Features.Features.Budgets.RenameBudget;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using NSubstitute;
using Shouldly;

namespace MyBudget.Features.Tests.Features.Budgets.RenameBudget;

/// <summary>
/// Unit tests for uniqueness checks added by REQ-BUDGET-UNIQUE-1.
/// Only failure paths are tested here because the success path invokes
/// ConnectionFactory.CreateConnection() (Dapper cache eviction) which
/// requires a live Npgsql connection; that path is covered by integration tests.
/// </summary>
public sealed class RenameBudgetHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly RenameBudgetHandler _sut;

    public RenameBudgetHandlerTests()
    {
        _db = DbTestHelpers.CreateSqliteContext();

        // ConnectionFactory is sealed and uses Npgsql; stub via a fake IConfiguration
        // that returns an empty connection string so the factory can be constructed.
        // The cache-eviction Dapper call is never reached on the failure paths tested below.
        var fakeConfig = Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>();
        fakeConfig.GetSection("ConnectionStrings")["DefaultConnection"].Returns("Host=localhost");
        var factory = new ConnectionFactory(new FakeConnectionStringConfig());
        var cache   = new MemoryCache(new MemoryCacheOptions());
        _sut = new RenameBudgetHandler(_db, factory, cache, NullLogger<RenameBudgetHandler>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task DuplicateName_ActiveSibling_Returns_BUDGET_NAME_DUPLICATE()
    {
        var user = User.Create("test@example.com", "hash", "Test", "User");
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var target = Budget.Create("Budget A", user.Id);
        var other  = Budget.Create("Budget B", user.Id);
        _db.Budgets.AddRange(target, other);
        await _db.SaveChangesAsync();

        var cmd    = new RenameBudgetCommand(target.Id, "Budget B", user.Id);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_NAME_DUPLICATE");
    }

    [Fact]
    public async Task SoftDeletedSiblingDuplicate_Returns_BUDGET_NAME_DUPLICATE()
    {
        var user = User.Create("test2@example.com", "hash", "Test", "User");
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var target  = Budget.Create("Budget A", user.Id);
        var deleted = Budget.Create("Budget B", user.Id);
        deleted.SoftDelete();
        _db.Budgets.AddRange(target, deleted);
        await _db.SaveChangesAsync();

        var cmd    = new RenameBudgetCommand(target.Id, "Budget B", user.Id);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_NAME_DUPLICATE");
    }
}

/// <summary>
/// Returns a valid-looking connection string so ConnectionFactory can be constructed;
/// the actual connection is never opened in failure-path handler tests.
/// </summary>
file sealed class FakeConnectionStringConfig : Microsoft.Extensions.Configuration.IConfiguration
{
    private const string ConnStr = "Host=localhost;Database=fake;Username=fake;Password=fake";

    public string? this[string key]
    {
        get => key == "ConnectionStrings:DefaultConnection" ? ConnStr : null;
        set { }
    }

    public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key)
    {
        // ConnectionFactory calls GetConnectionString("DefaultConnection") which internally calls
        // GetSection("ConnectionStrings")["DefaultConnection"]. We return a stub section.
        if (key == "ConnectionStrings")
            return new FakeSection(ConnStr);
        return new FakeSection(null);
    }

    public System.Collections.Generic.IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => [];
    public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => NullChangeToken.Instance;
}

file sealed class FakeSection : Microsoft.Extensions.Configuration.IConfigurationSection
{
    private readonly string? _value;
    public FakeSection(string? value) => _value = value;

    public string? this[string key] { get => _value; set { } }
    public string Key   => "ConnectionStrings";
    public string Path  => "ConnectionStrings";
    public string? Value { get => _value; set { } }

    public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => new FakeSection(_value);
    public System.Collections.Generic.IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => [];
    public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => NullChangeToken.Instance;
}

file sealed class NullChangeToken : Microsoft.Extensions.Primitives.IChangeToken
{
    public static readonly NullChangeToken Instance = new();
    public bool ActiveChangeCallbacks => false;
    public bool HasChanged => false;
    public System.IDisposable RegisterChangeCallback(System.Action<object?> callback, object? state)
        => System.Threading.CancellationToken.None.Register(callback, state);
}
