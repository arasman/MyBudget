using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.UpdateCategory;

public sealed class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CategoryGroupId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .MaximumLength(200).WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.DisplayOrder)
            .GreaterThan(0).WithErrorCode("FIELD_INVALID");
    }
}
