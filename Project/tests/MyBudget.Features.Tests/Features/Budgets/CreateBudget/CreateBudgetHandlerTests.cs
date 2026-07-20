using Microsoft.Extensions.Logging.Abstractions;
using MyBudget.Features.Features.Budgets.CreateBudget;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.Budgets.CreateBudget;

public sealed class CreateBudgetHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CreateBudgetHandler _sut;

    public CreateBudgetHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new CreateBudgetHandler(_db, NullLogger<CreateBudgetHandler>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ActiveDuplicate_Returns_BUDGET_NAME_DUPLICATE()
    {
        var userId = await SeedUserAsync();
        _db.Budgets.Add(Budget.Create("My Budget", userId));
        await _db.SaveChangesAsync();

        var cmd    = new CreateBudgetCommand("My Budget", userId);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_NAME_DUPLICATE");
    }

    [Fact]
    public async Task SoftDeletedDuplicate_Returns_BUDGET_NAME_DUPLICATE()
    {
        var userId = await SeedUserAsync();
        var budget = Budget.Create("My Budget", userId);
        budget.SoftDelete();
        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();

        var cmd    = new CreateBudgetCommand("My Budget", userId);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_NAME_DUPLICATE");
    }

    [Fact]
    public async Task UniqueName_Succeeds()
    {
        var userId = await SeedUserAsync();

        var cmd    = new CreateBudgetCommand("Vacation Fund", userId);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.BudgetId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task SameName_DifferentUser_Succeeds()
    {
        var userId1 = await SeedUserAsync("user1@example.com");
        var userId2 = await SeedUserAsync("user2@example.com");

        _db.Budgets.Add(Budget.Create("My Budget", userId1));
        await _db.SaveChangesAsync();

        var cmd    = new CreateBudgetCommand("My Budget", userId2);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    private async Task<Guid> SeedUserAsync(string email = "test@example.com")
    {
        var user = User.Create(email, "hash", "Test", "User");
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user.Id;
    }
}
