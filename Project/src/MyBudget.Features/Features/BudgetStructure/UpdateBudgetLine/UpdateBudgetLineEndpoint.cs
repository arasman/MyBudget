using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLine;

// TODO PR2a: update route to /api/budgets/{id}/lines/{lineId} (remove periodId)
public static class UpdateBudgetLineEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/budgets/{id}/lines/{lineId}", Handle)
            .WithTags("BudgetStructure")
            .WithName("UpdateBudgetLine")
            .Produces<Guid>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid lineId,
        UpdateBudgetLineRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd = new UpdateBudgetLineCommand(
            id, lineId,
            body.CategoryGroupId, body.CategoryId,
            body.Name, body.LineType,
            body.ValidFrom, body.ValidTo,
            body.BudgetedAmount, body.CurrencyId);

        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            if (result.Error == "BUDGET_LINE_NOT_FOUND")
                return Results.NotFound(new { error = result.Error });
            if (result.Error == "PERIOD_CLOSED")
                return Results.Conflict(new { error = "PERIOD_CLOSED" });
            return Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.Ok(new { id = result.Value });
    }
}

public sealed record UpdateBudgetLineRequest(
    Guid      CategoryGroupId,
    Guid?     CategoryId,
    string    Name,
    LineType  LineType,
    // Revision split — optional
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    decimal?  BudgetedAmount,
    Guid?     CurrencyId);
