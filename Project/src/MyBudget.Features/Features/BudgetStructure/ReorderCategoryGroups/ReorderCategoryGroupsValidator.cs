using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.ReorderCategoryGroups;

public sealed class ReorderCategoryGroupsValidator : AbstractValidator<ReorderCategoryGroupsCommand>
{
    public ReorderCategoryGroupsValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.OrderedIds)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
