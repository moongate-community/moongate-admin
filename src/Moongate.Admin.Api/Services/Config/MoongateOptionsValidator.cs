using System.Net;
using Microsoft.Extensions.Options;
using Moongate.Admin.Api.Data.Config;

namespace Moongate.Admin.Api.Services.Config;

public sealed class MoongateOptionsValidator : IValidateOptions<MoongateOptions>
{
    private const int MaximumEndpoints = 16;
    private const int MaximumIdLength = 64;
    private const int MaximumLabelLength = 100;
    private const int MaximumAddressLength = 2048;
    private readonly IHostEnvironment _environment;

    public MoongateOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, MoongateOptions options)
    {
        if (options.Endpoints is { Count: 0 } && options.AuthenticationEndpointId == "")
        {
            return ValidateOptionsResult.Success;
        }

        if (options.Endpoints is null || options.Endpoints.Count is 0 or > MaximumEndpoints)
        {
            return ValidateOptionsResult.Fail("Invalid Moongate endpoint configuration.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var endpoint in options.Endpoints)
        {
            if (endpoint is null || string.IsNullOrEmpty(endpoint.Id) || endpoint.Id.Length > MaximumIdLength ||
                endpoint.Id.Any(character =>
                    !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-'
                ) ||
                string.IsNullOrWhiteSpace(endpoint.Label) || endpoint.Label.Length > MaximumLabelLength ||
                endpoint.Label.Any(char.IsControl) ||
                string.IsNullOrWhiteSpace(endpoint.Address) || endpoint.Address.Length > MaximumAddressLength ||
                !ids.Add(endpoint.Id) || !Uri.TryCreate(endpoint.Address, UriKind.Absolute, out var uri) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/")
            {
                return ValidateOptionsResult.Fail("Invalid Moongate endpoint configuration.");
            }

            if (uri.Scheme != Uri.UriSchemeHttps &&
                !(uri.Scheme == Uri.UriSchemeHttp && _environment.IsDevelopment() && options.AllowInsecureLoopback &&
                  IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address)))
            {
                return ValidateOptionsResult.Fail(
                    "Moongate endpoints require HTTPS; Development may explicitly allow literal loopback HTTP."
                );
            }
        }

        if (!ids.Contains(options.AuthenticationEndpointId))
        {
            return ValidateOptionsResult.Fail("A configured Moongate authentication endpoint is required.");
        }

        return ValidateOptionsResult.Success;
    }
}
