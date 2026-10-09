using System.Text.Json;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moongate.Admin.Api.Internal;
using Microsoft.Extensions.Logging;
using Moongate.Admin.Api.Services.Errors;
using Moongate.Admin.Tests.TestSupport.Logging;

namespace Moongate.Admin.Tests.Integration.Errors;

public class ProblemDetailsTests
{
    [Theory]
    [InlineData(StatusCode.InvalidArgument, 400)]
    [InlineData(StatusCode.Unauthenticated, 401)]
    [InlineData(StatusCode.PermissionDenied, 403)]
    [InlineData(StatusCode.NotFound, 404)]
    [InlineData(StatusCode.AlreadyExists, 409)]
    [InlineData(StatusCode.ResourceExhausted, 429)]
    [InlineData(StatusCode.Unavailable, 503)]
    [InlineData(StatusCode.DeadlineExceeded, 504)]
    [InlineData(StatusCode.Unimplemented, 501)]
    [InlineData(StatusCode.Internal, 502)]
    [InlineData(StatusCode.Unknown, 502)]
    [InlineData(StatusCode.Cancelled, 503)]
    public async Task Handle_UpstreamStatus_WritesSafeProblem(StatusCode code, int status)
    {
        await using var provider = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider, TraceIdentifier = "test-correlation" };
        context.Response.Body = new MemoryStream();
        var handler = new AdminApiExceptionHandler(NullLogger<AdminApiExceptionHandler>.Instance);
        Assert.True(await handler.TryHandleAsync(context, new UpstreamCallException(code), CancellationToken.None));
        Assert.Equal(status, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("test-correlation", json.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal("upstream_" + code.ToString().ToLowerInvariant(), json.RootElement.GetProperty("code").GetString());
        Assert.False(json.RootElement.TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task Handle_UnexpectedException_IsRedacted500()
    {
        await using var provider = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider, TraceIdentifier = "c" };
        context.Response.Body = new MemoryStream();
        var handler = new AdminApiExceptionHandler(NullLogger<AdminApiExceptionHandler>.Instance);
        Assert.True(await handler.TryHandleAsync(context, new InvalidOperationException("secret-detail"), CancellationToken.None));
        Assert.Equal(500, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        Assert.DoesNotContain("secret-detail", await new StreamReader(context.Response.Body).ReadToEndAsync());
    }

    [Fact]
    public async Task Handle_UnexpectedException_LogsTheExceptionForDiagnosis()
    {
        await using var provider = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider, TraceIdentifier = "c" };
        context.Response.Body = new MemoryStream();
        var logger = new CapturingLogger<AdminApiExceptionHandler>();
        var failure = new InvalidOperationException("boom");
        await new AdminApiExceptionHandler(logger).TryHandleAsync(context, failure, CancellationToken.None);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && ReferenceEquals(entry.Exception, failure));
    }
}
