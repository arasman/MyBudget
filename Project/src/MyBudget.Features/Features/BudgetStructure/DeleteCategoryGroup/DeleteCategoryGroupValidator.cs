using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.DeleteCategoryGroup;

public sealed class DeleteCategoryGroupValidator : AbstractValidator<DeleteCategoryGroupCommand>
{
    public DeleteCategoryGroupValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.GroupId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
