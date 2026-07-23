using MyBudget.Features.Features.Auth.UpdateLocale;
using Shouldly;

namespace MyBudget.Features.Tests.Features.Auth.UpdateLocale;

public sealed class UpdateLocaleValidatorTests
{
    private readonly UpdateLocaleValidator _sut = new();

    private static UpdateLocaleCommand ValidCommand() =>
        new("en");

    [Fact]
    public void ValidLocale_Passes()
    {
        _sut.Validate(ValidCommand()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyLocale_ProducesFieldRequiredError()
    {
        var result = _sut.Validate(ValidCommand() with { Locale = "" });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "FIELD_REQUIRED");
    }

    [Fact]
    public void NullLocale_ProducesFieldRequiredError()
    {
        var result = _sut.Validate(ValidCommand() with { Locale = null! });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "FIELD_REQUIRED");
    }
}
