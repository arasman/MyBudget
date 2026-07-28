using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.CurrentSituation.DeleteCutRecord;

public static class DeleteCutRecordEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/budgets/{id}/cut-records/{date}", Handle)
            .WithTags("CurrentSituation")
            .WithName("DeleteCutRecord")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization("budget:operator");

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

        var cmd    = new DeleteCutRecordCommand(id, cutDate);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "CUT_RECORD_NOT_FOUND" => Results.NotFound(new { error = result.Error }),
                _                      => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity),
            };
        }

        return Results.NoContent();
    }
}
