using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.Features.BudgetStructure.CreateBudgetLine;

public static class CreateBudgetLineEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets/{id}/lines", Handle)
            .WithTags("BudgetStructure")
            .WithName("CreateBudgetLine")
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        CreateBudgetLineRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd = new CreateBudgetLineCommand(
            id,
            body.CategoryGroupId, body.CategoryId,
            body.Name, body.LineType,
            body.StartDate, body.EndDate,
            body.InitialAmount, body.CurrencyId);

        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "BUDGET_NOT_FOUND"           => Results.NotFound(new { error = result.Error }),
                "BUDGET_LINE_NAME_DUPLICATE" => Results.UnprocessableEntity(new { error = result.Error }),
                _ => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity),
            };
        }

        return Results.Created(
            $"/api/budgets/{id}/lines/{result.Value}",
            new { id = result.Value });
    }
}

public sealed record CreateBudgetLineRequest(
    Guid      CategoryGroupId,
    Guid?     CategoryId,
    string    Name,
    LineType  LineType,
    DateOnly  StartDate,
    DateOnly? EndDate,
    decimal   InitialAmount,
    Guid?     CurrencyId);
