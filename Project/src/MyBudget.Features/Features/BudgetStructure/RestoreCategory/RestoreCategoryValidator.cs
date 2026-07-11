using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.RestoreCategory;

public sealed class RestoreCategoryValidator : AbstractValidator<RestoreCategoryCommand>
{
    public RestoreCategoryValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CategoryGroupId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
