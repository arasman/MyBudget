using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.CreateCategory;

public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CategoryGroupId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .MaximumLength(200).WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.DisplayOrder)
            .GreaterThan(0).WithErrorCode("FIELD_INVALID");
    }
}
