using Moongate.Admin.Contracts.V1;

namespace Moongate.Admin.Tests.TestSupport.Grpc;

public sealed class FakeAdminHostState
{
    public ServerMode Mode { get; init; } = ServerMode.Login;
    public ServerMode? ReportedMode { get; init; }
    public string InstanceId { get; init; } = "fixture-login";
}
