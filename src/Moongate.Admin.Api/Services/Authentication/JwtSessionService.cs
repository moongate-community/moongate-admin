using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Moongate.Admin.Api.Data.Internal.Sessions;
using Moongate.Admin.Api.Data.Sessions;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Types.Authentication;
using Grpc.Core;

namespace Moongate.Admin.Api.Services.Authentication;

public sealed class JwtSessionService : IDisposable
{
    private const int KeyBytes = 32;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly SymmetricSecurityKey _key = new(RandomNumberGenerator.GetBytes(KeyBytes));
    private readonly TimeProvider _clock;

    public JwtSessionService(TimeProvider clock)
    {
        _clock = clock;
    }
    public JwtLoginResponse Create(UpstreamLoginResult login, string configurationRevision)
    {
        ArgumentNullException.ThrowIfNull(login);
        ArgumentException.ThrowIfNullOrEmpty(configurationRevision);
        var expiry = DateTimeOffset.FromUnixTimeSeconds(login.ExpiresAt.ToUnixTimeSeconds());
        if (expiry <= _clock.GetUtcNow())
        {
            throw new UpstreamCallException(StatusCode.Internal);
        }
        var id = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(KeyBytes));
        var session = new AdminSession
        {
            SessionId = id, AccessToken = login.AccessToken, ExpiresAt = expiry, Account = login.Account,
            ConfigurationRevision = configurationRevision
        };
        var jwt = new JwtSecurityToken(AdminAuthentication.Issuer, AdminAuthentication.Audience,
        [
            new Claim(JwtRegisteredClaimNames.Sub, login.Account.AccountId.ToString(CultureInfo.InvariantCulture)),
            new Claim(JwtRegisteredClaimNames.Jti, id),
            new Claim("name", login.Account.Username),
            new Claim("role", login.Account.AccountType.ToString().ToLowerInvariant())
        ], _clock.GetUtcNow().UtcDateTime, expiry.UtcDateTime, new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        _cache.Set(id, session, expiry);
        return new JwtLoginResponse { AccessToken = token, ExpiresAt = expiry, Account = login.Account };
    }
    public AdminSession? Find(string? id)
    {
        if (id is not null && _cache.TryGetValue(id, out AdminSession? session) && session is not null)
        {
            if (session.ExpiresAt > _clock.GetUtcNow())
            {
                return session;
            }
            _cache.Remove(id);
        }
        return null;
    }
    public void Remove(string id)
    {
        _cache.Remove(id);
    }
    public TokenValidationParameters CreateValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true, IssuerSigningKey = _key,
            ValidateIssuer = true, ValidIssuer = AdminAuthentication.Issuer,
            ValidateAudience = true, ValidAudience = AdminAuthentication.Audience,
            ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero,
            NameClaimType = "name", RoleClaimType = "role",
            LifetimeValidator = (notBefore, expires, _, _) => expires is { } end && end > _clock.GetUtcNow().UtcDateTime &&
                (notBefore is null || notBefore.Value <= _clock.GetUtcNow().UtcDateTime)
        };
    }
    public void Dispose()
    {
        _cache.Dispose();
        CryptographicOperations.ZeroMemory(_key.Key);
    }
}
