using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.ReorderCategories;

public sealed class ReorderCategoriesValidator : AbstractValidator<ReorderCategoriesCommand>
{
    public ReorderCategoriesValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CategoryGroupId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.OrderedIds)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
