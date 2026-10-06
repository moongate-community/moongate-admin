using System.Text.Json;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Errors;

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
    [InlineData(StatusCode.Cancelled, 503)]
    public async Task Handle_UpstreamStatus_WritesSafeProblem(StatusCode code, int status)
    {
        var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        await using var provider = services;
        var context = new DefaultHttpContext { RequestServices = provider, TraceIdentifier = "test-correlation" };
        context.Response.Body = new MemoryStream();
        var handler = new AdminApiExceptionHandler();
        Assert.True(await handler.TryHandleAsync(context, new UpstreamCallException(code), CancellationToken.None));
        Assert.Equal(status, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("test-correlation", json.RootElement.GetProperty("correlationId").GetString());
        Assert.False(json.RootElement.TryGetProperty("accessToken", out _));
    }
}
