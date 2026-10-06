using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Moongate.Admin.Api.Internal;

namespace Moongate.Admin.Api.Services.Config;

public sealed class SetupTokenService : IHostedService, IDisposable
{
    private const int TokenBytes = 32;
    private const int EncodedTokenLength = 43;
    private readonly IConfiguration _configuration;
    private byte[]? _digest;
    public bool Available { get { return _digest is not null; } }
    public SetupTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = _configuration["MOONGATE_ADMIN_SETUP_TOKEN"];
        if (string.IsNullOrEmpty(token)) { return Task.CompletedTask; }
        if (token.Length != EncodedTokenLength || token.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_') ||
            WebEncoders.Base64UrlDecode(token).Length != TokenBytes)
        {
            throw new ConfigurationException(StatusCodes.Status500InternalServerError, "setup_token_invalid");
        }
        _digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    public bool Verify(string? presented)
    {
        if (_digest is null || presented is null) { return false; }
        return CryptographicOperations.FixedTimeEquals(_digest, SHA256.HashData(Encoding.UTF8.GetBytes(presented)));
    }
    public void Dispose()
    {
        if (_digest is not null) { CryptographicOperations.ZeroMemory(_digest); _digest = null; }
    }
}
