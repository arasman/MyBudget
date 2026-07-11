using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLine;

public static class UpdateBudgetLineEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/budgets/{id}/periods/{periodId}/lines/{lineId}", Handle)
            .WithTags("BudgetStructure")
            .WithName("UpdateBudgetLine")
            .Produces<Guid>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid periodId,
        Guid lineId,
        UpdateBudgetLineRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd = new UpdateBudgetLineCommand(
            id, periodId, lineId,
            body.CategoryGroupId, body.CategoryId,
            body.Name, body.LineType, body.IsRecurring,
            body.BudgetedAmount, body.CurrencyId);

        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            if (result.Error == "PERIOD_CLOSED")
                return Results.Conflict(new { error = "PERIOD_CLOSED" });
            if (result.Error == "BUDGET_LINE_NOT_FOUND")
                return Results.NotFound(new { error = result.Error });
            return Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.Ok(new { id = result.Value });
    }
}

public sealed record UpdateBudgetLineRequest(
    Guid     CategoryGroupId,
    Guid?    CategoryId,
    string   Name,
    LineType LineType,
    bool     IsRecurring,
    decimal  BudgetedAmount,
    Guid?    CurrencyId);
