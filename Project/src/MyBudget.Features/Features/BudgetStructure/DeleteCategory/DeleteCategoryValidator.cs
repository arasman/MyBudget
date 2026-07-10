using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.DeleteCategory;

public sealed class DeleteCategoryValidator : AbstractValidator<DeleteCategoryCommand>
{
    public DeleteCategoryValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CategoryGroupId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
