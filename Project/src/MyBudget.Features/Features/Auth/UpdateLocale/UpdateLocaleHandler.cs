using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;
using MyBudget.Features.SharedKernel.Services;

namespace MyBudget.Features.Features.Auth.UpdateLocale;

public sealed class UpdateLocaleHandler : IRequestHandler<UpdateLocaleCommand, Result<Unit>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IConfiguration      _config;

    public UpdateLocaleHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        IConfiguration      config)
    {
        _db          = db;
        _currentUser = currentUser;
        _config      = config;
    }

    public async ValueTask<Result<Unit>> Handle(UpdateLocaleCommand cmd, CancellationToken ct)
    {
        // STEP 1 — Resolve current user from JWT claims
        var userId = _currentUser.UserId
            ?? throw new InvalidOperationException("Authenticated user has no UserId claim.");

        // STEP 2 — Validate locale against SupportedCultures from IConfiguration
        var supported = _config
            .GetSection("RequestLocalization:SupportedCultures")
            .Get<string[]>() ?? ["en", "es"];

        if (!supported.Contains(cmd.Locale, StringComparer.OrdinalIgnoreCase))
            return Result<Unit>.Failure("AUTH_LOCALE_UNSUPPORTED");

        // STEP 3 — Load user via EF
        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new InvalidOperationException("Authenticated user not found in database.");

        // STEP 4 — Apply locale change and persist
        user.UpdateLocale(cmd.Locale);
        await _db.SaveChangesAsync(ct);

        return Result<Unit>.Success(Unit.Value);
    }
}
