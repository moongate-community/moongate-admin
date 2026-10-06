using Google.Protobuf.WellKnownTypes;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Upstream;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Contracts.V1;

namespace Moongate.Admin.Tests.Upstream;

public class AdminResponseMapperTests
{
    [Fact]
    public void ToAccount_UnknownRole_RejectsWireResponse()
    {
        var account = new FakeAdminAuthority().Summary();
        account.AccountType = (AccountType)99;
        Assert.Throws<UpstreamCallException>(() => AdminResponseMapper.ToAccount(account));
    }
    [Fact]
    public void ToDate_InvalidTimestamp_RejectsWireResponse()
    {
        Assert.Throws<UpstreamCallException>(() => AdminResponseMapper.ToDate(new Timestamp { Seconds = long.MaxValue }));
    }
    [Fact]
    public void ToServer_UnknownMode_RejectsWireResponse()
    {
        Assert.Throws<UpstreamCallException>(() => AdminResponseMapper.ToServer(new GetServerInfoResponse { Mode = (ServerMode)99 }));
    }
}
