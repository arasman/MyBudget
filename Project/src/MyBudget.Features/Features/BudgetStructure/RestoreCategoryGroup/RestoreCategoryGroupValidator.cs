using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.RestoreCategoryGroup;

public sealed class RestoreCategoryGroupValidator : AbstractValidator<RestoreCategoryGroupCommand>
{
    public RestoreCategoryGroupValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CategoryGroupId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
