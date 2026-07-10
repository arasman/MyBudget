using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.GetCycleDetail;

public static class GetCycleDetailEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/budgets/{id}/cycles/{cycleId}", Handle)
            .WithTags("BudgetStructure")
            .WithName("GetCycleDetail")
            .Produces<CycleDetailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization("budget:read");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid cycleId,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetCycleDetailQuery(id, cycleId), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(new { error = result.Error });
    }
}
