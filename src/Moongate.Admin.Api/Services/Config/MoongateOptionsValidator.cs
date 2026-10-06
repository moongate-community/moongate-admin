using System.Net;
using Microsoft.Extensions.Options;
using Moongate.Admin.Api.Data.Config;

namespace Moongate.Admin.Api.Services.Config;

public sealed class MoongateOptionsValidator : IValidateOptions<MoongateOptions>
{
    private readonly IHostEnvironment _environment;

    public MoongateOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, MoongateOptions options)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var endpoint in options.Endpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint.Id) || string.IsNullOrWhiteSpace(endpoint.Label) ||
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

        if (ids.Count == 0 || !ids.Contains(options.AuthenticationEndpointId))
        {
            return ValidateOptionsResult.Fail("A configured Moongate authentication endpoint is required.");
        }

        return ValidateOptionsResult.Success;
    }
}
