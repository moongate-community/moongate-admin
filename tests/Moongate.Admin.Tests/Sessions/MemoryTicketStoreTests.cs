using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Time.Testing;
using Moongate.Admin.Api.Services.Authentication;

namespace Moongate.Admin.Tests.Sessions;

public class MemoryTicketStoreTests
{
    [Fact]
    public async Task RetrieveAsync_AbsoluteExpiryAndIsolation_Enforced()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        using var store = new MemoryTicketStore(clock);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("name", "Admin")], "test")),
            new AuthenticationProperties { ExpiresUtc = clock.GetUtcNow().AddMinutes(30) },
            "test"
        );
        var key = await store.StoreAsync(ticket);
        Assert.NotEqual("stub", key);
        var retrieved = await store.RetrieveAsync(key);
        Assert.NotNull(retrieved);
        retrieved.Properties.ExpiresUtc = clock.GetUtcNow().AddDays(1);
        Assert.Equal(clock.GetUtcNow().AddMinutes(30), (await store.RetrieveAsync(key))?.Properties.ExpiresUtc);
        await store.RenewAsync(key, retrieved);
        clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Null(await store.RetrieveAsync(key));
        await store.RenewAsync(key, ticket);
        Assert.Null(await store.RetrieveAsync(key));
    }

    [Fact]
    public async Task RemoveAsync_Ticket_CannotBeRenewed()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        using var store = new MemoryTicketStore(clock);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(),
            new AuthenticationProperties
            {
                ExpiresUtc = clock.GetUtcNow().AddMinutes(30)
            },
            "test"
        );
        var key = await store.StoreAsync(ticket);
        await store.RemoveAsync(key);
        await store.RenewAsync(key, ticket);
        Assert.Null(await store.RetrieveAsync(key));
        Assert.Null(await store.RetrieveAsync("missing"));
    }
}
