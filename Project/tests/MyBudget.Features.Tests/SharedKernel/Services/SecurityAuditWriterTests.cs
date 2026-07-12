using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Services;
using NSubstitute;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Services;

/// <summary>
/// Unit tests for SecurityAuditWriter.
/// Verifies IP address and UserAgent extraction from a mocked IHttpContextAccessor.
/// </summary>
public sealed class SecurityAuditWriterTests : IDisposable
{
    private readonly AppDbContext _db;

    public SecurityAuditWriterTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new AppDbContext(opts);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    private static IHttpContextAccessor BuildAccessor(
        string? xForwardedFor = null,
        string? remoteIp      = null,
        string? userAgent     = null)
    {
        var httpContext = Substitute.For<HttpContext>();
        var request     = Substitute.For<HttpRequest>();
        var connection  = Substitute.For<ConnectionInfo>();

        // X-Forwarded-For header
        var headerDict = new HeaderDictionary();
        if (xForwardedFor is not null)
            headerDict["X-Forwarded-For"] = xForwardedFor;
        if (userAgent is not null)
            headerDict["User-Agent"] = userAgent;

        request.Headers.Returns(headerDict);
        httpContext.Request.Returns(request);

        // RemoteIpAddress fallback
        connection.RemoteIpAddress.Returns(
            remoteIp is null ? null : System.Net.IPAddress.Parse(remoteIp));
        httpContext.Connection.Returns(connection);

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        return accessor;
    }

    [Fact]
    public async Task WriteAsync_ExtractsIpAddress_FromXForwardedFor()
    {
        var accessor = BuildAccessor(xForwardedFor: "203.0.113.5, 10.0.0.1", userAgent: "TestAgent/1.0");
        var writer   = new SecurityAuditWriter(_db, accessor);

        await writer.WriteAsync("SuccessfulLogin", userId: Guid.NewGuid(), email: "a@b.com");

        var entry = _db.SecurityAuditLogs.Single();
        entry.IpAddress.ShouldBe("203.0.113.5");
        entry.UserAgent.ShouldBe("TestAgent/1.0");
    }

    [Fact]
    public async Task WriteAsync_FallsBack_ToRemoteIpAddress_WhenNoXForwardedFor()
    {
        var accessor = BuildAccessor(remoteIp: "192.168.1.100", userAgent: "Mozilla/5.0");
        var writer   = new SecurityAuditWriter(_db, accessor);

        await writer.WriteAsync("FailedLogin", userId: null, email: "x@y.com");

        var entry = _db.SecurityAuditLogs.Single();
        entry.IpAddress.ShouldBe("192.168.1.100");
        entry.UserAgent.ShouldBe("Mozilla/5.0");
    }

    [Fact]
    public async Task WriteAsync_NullHttpContext_DoesNotThrow_AndSetsNullIpAndAgent()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var writer = new SecurityAuditWriter(_db, accessor);

        await writer.WriteAsync("TokenRefreshed", userId: Guid.NewGuid(), email: null);

        var entry = _db.SecurityAuditLogs.Single();
        entry.IpAddress.ShouldBeNull();
        entry.UserAgent.ShouldBeNull();
    }

    [Fact]
    public async Task WriteAsync_PersistsEvent_UserId_And_Email()
    {
        var accessor = BuildAccessor(xForwardedFor: "1.2.3.4");
        var writer   = new SecurityAuditWriter(_db, accessor);
        var userId   = Guid.NewGuid();

        await writer.WriteAsync("AccountRegistered", userId: userId, email: "user@test.com");

        var entry = _db.SecurityAuditLogs.Single();
        entry.Event.ShouldBe("AccountRegistered");
        entry.UserId.ShouldBe(userId);
        entry.Email.ShouldBe("user@test.com");
        entry.Timestamp.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
    }
}
