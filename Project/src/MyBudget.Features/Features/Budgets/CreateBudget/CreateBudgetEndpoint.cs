using System.Security.Claims;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.Budgets.CreateBudget;

public static class CreateBudgetEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets", Handle)
            .WithTags("Budgets")
            .WithName("CreateBudget")
            .Produces<CreateBudgetResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Handle(
        CreateBudgetRequest request,
        ClaimsPrincipal principal,
        IMediator mediator,
        CancellationToken ct)
    {
        var userIdRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var userId))
            return Results.Unauthorized();

        var command = new CreateBudgetCommand(request.Name, userId);
        var result  = await mediator.Send(command, ct);

        if (!result.IsSuccess)
            return Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);

        return Results.Created($"/api/budgets/{result.Value!.BudgetId}", result.Value);
    }

    private sealed record CreateBudgetRequest(string Name);
}
