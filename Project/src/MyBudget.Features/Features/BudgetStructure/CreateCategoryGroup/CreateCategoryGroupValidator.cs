using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.CreateCategoryGroup;

public sealed class CreateCategoryGroupValidator : AbstractValidator<CreateCategoryGroupCommand>
{
    public CreateCategoryGroupValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .MaximumLength(200).WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.DisplayOrder)
            .GreaterThan(0).WithErrorCode("FIELD_INVALID");
    }
}
