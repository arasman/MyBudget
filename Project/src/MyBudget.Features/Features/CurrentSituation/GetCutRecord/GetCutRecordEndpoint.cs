using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.CurrentSituation.GetCutRecord;

public static class GetCutRecordEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/budgets/{id}/cut-records/{date}", Handle)
            .WithTags("CurrentSituation")
            .WithName("GetCutRecord")
            .Produces<GetCutRecordResponse>(StatusCodes.Status200OK)
            .RequireAuthorization("budget:read");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        string date,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var cutDate))
            return Results.BadRequest(new { error = "INVALID_DATE_FORMAT" });

        var query  = new GetCutRecordQuery(id, cutDate);
        var result = await mediator.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(result.Error, statusCode: StatusCodes.Status500InternalServerError);
    }
}
