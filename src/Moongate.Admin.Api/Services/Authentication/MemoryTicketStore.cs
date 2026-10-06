using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace Moongate.Admin.Api.Services.Authentication;

public sealed class MemoryTicketStore : ITicketStore, IDisposable
{
    private const int ReferenceBytes = 32;
    private readonly object _gate = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly TimeProvider _clock;

    public MemoryTicketStore(TimeProvider clock)
    {
        _clock = clock;
    }
    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var expiry = ticket.Properties.ExpiresUtc ?? throw new InvalidOperationException("Session expiration is required.");
        if (expiry <= _clock.GetUtcNow())
        {
            throw new InvalidOperationException("Session already expired.");
        }
        var key = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(ReferenceBytes));
        lock (_gate)
        {
            _cache.Set(key, (TicketSerializer.Default.Serialize(ticket), expiry), expiry);
        }
        return Task.FromResult(key);
    }
    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(key, out (byte[] Bytes, DateTimeOffset Expiry) entry) && entry.Expiry > _clock.GetUtcNow())
            {
                return Task.FromResult(TicketSerializer.Default.Deserialize(entry.Bytes));
            }
            _cache.Remove(key);
            return Task.FromResult<AuthenticationTicket?>(null);
        }
    }
    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(key, out (byte[] Bytes, DateTimeOffset Expiry) entry) && entry.Expiry > _clock.GetUtcNow())
            {
                var copy = TicketSerializer.Default.Deserialize(TicketSerializer.Default.Serialize(ticket))
                    ?? throw new InvalidOperationException("Invalid session ticket.");
                var expiry = copy.Properties.ExpiresUtc is { } updated && updated < entry.Expiry ? updated : entry.Expiry;
                copy.Properties.ExpiresUtc = expiry;
                _cache.Set(key, (TicketSerializer.Default.Serialize(copy), expiry), expiry);
            }
        }
        return Task.CompletedTask;
    }
    public Task RemoveAsync(string key)
    {
        lock (_gate)
        {
            _cache.Remove(key);
        }
        return Task.CompletedTask;
    }
    public void Dispose()
    {
        _cache.Dispose();
    }
}
