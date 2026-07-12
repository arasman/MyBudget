using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.AuditLog.GetSecurityAuditLog;

public static class GetSecurityAuditLogEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/budgets/{id}/security-audit-log", Handle)
            .WithTags("AuditLog")
            .WithName("GetSecurityAuditLog")
            .Produces<PagedResult<SecurityAuditLogItem>>(StatusCodes.Status200OK)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid      id,
        IMediator mediator,
        CancellationToken ct,
        int       page     = 1,
        int       pageSize = 20)
    {
        var query  = new GetSecurityAuditLogQuery(id, page, pageSize);
        var result = await mediator.Send(query, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(result.Error, statusCode: StatusCodes.Status500InternalServerError);
    }
}
