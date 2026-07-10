using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.CreatePeriod;

public static class CreatePeriodEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets/{id}/cycles/{cycleId}/periods", Handle)
            .WithTags("BudgetStructure")
            .WithName("CreatePeriod")
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid cycleId,
        CreatePeriodRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new CreatePeriodCommand(id, cycleId, body.Name, body.PeriodNumber, body.StartDate, body.EndDate);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "CYCLE_NOT_FOUND"          => Results.NotFound(new { error = result.Error }),
                "PERIOD_OUT_OF_CYCLE_RANGE"=> Results.UnprocessableEntity(new { error = result.Error }),
                "PERIOD_DATE_OVERLAP"      => Results.UnprocessableEntity(new { error = result.Error }),
                _                          => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity)
            };
        }

        return Results.Created($"/api/budgets/{id}/cycles/{cycleId}/periods/{result.Value}", new { id = result.Value });
    }
}

public sealed record CreatePeriodRequest(string Name, int PeriodNumber, DateOnly StartDate, DateOnly EndDate);
