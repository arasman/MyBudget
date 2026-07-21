using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.DeleteBudgetLine;

// TODO PR2a: route updated — periodId removed (REQ-BL-04)
public static class DeleteBudgetLineEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/budgets/{id}/lines/{lineId}", Handle)
            .WithTags("BudgetStructure")
            .WithName("DeleteBudgetLine")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid lineId,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new DeleteBudgetLineCommand(id, lineId);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            if (result.Error == "BUDGET_LINE_NOT_FOUND")
                return Results.NotFound(new { error = result.Error });
            return Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.NoContent();
    }
}
