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
        app.MapPost("/api/budgets/{id}/periods/{periodId}/lines", Handle)
            .WithTags("BudgetStructure")
            .WithName("CreateBudgetLine")
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid periodId,
        CreateBudgetLineRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd = new CreateBudgetLineCommand(
            id, periodId,
            body.CategoryGroupId, body.CategoryId,
            body.Name, body.LineType, body.IsRecurring,
            body.BudgetedAmount, body.Currency);

        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            if (result.Error == "PERIOD_CLOSED")
                return Results.Conflict(new { error = "PERIOD_CLOSED" });
            if (result.Error == "PERIOD_NOT_FOUND")
                return Results.NotFound(new { error = result.Error });
            return Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.Created(
            $"/api/budgets/{id}/periods/{periodId}/lines/{result.Value}",
            new { id = result.Value });
    }
}

public sealed record CreateBudgetLineRequest(
    Guid     CategoryGroupId,
    Guid?    CategoryId,
    string   Name,
    LineType LineType,
    bool     IsRecurring,
    decimal  BudgetedAmount,
    string   Currency);
