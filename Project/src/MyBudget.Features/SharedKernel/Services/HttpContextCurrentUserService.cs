using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace MyBudget.Features.SharedKernel.Services;

/// <summary>
/// Scoped implementation of <see cref="ICurrentUserService"/> backed by <see cref="IHttpContextAccessor"/>.
/// Returns null when there is no active HTTP context or the user is not authenticated.
/// </summary>
public sealed class HttpContextCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?
                .User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }
}
