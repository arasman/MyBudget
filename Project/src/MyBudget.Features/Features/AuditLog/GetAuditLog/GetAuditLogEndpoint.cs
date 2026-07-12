using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.AuditLog.GetAuditLog;

public static class GetAuditLogEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/budgets/{id}/audit-log", Handle)
            .WithTags("AuditLog")
            .WithName("GetAuditLog")
            .Produces<PagedResult<AuditLogItem>>(StatusCodes.Status200OK)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid            id,
        IMediator       mediator,
        CancellationToken ct,
        int             page       = 1,
        int             pageSize   = 20,
        string?         entityName = null,
        string?         action     = null,
        DateTimeOffset? from       = null,
        DateTimeOffset? to         = null)
    {
        var query  = new GetAuditLogQuery(id, page, pageSize, entityName, action, from, to);
        var result = await mediator.Send(query, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(result.Error, statusCode: StatusCodes.Status500InternalServerError);
    }
}
