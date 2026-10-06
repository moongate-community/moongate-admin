using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Moongate.Admin.Api.Data.Accounts;
using Moongate.Admin.Api.Data.Internal.Sessions;
using Moongate.Admin.Api.Types.Authentication;

namespace Moongate.Admin.Api.Services.Authentication;

public sealed class AdminSessionAccessor
{
    public async Task<AdminSession?> GetAsync(HttpContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await context.AuthenticateAsync(AdminAuthentication.Scheme);
        if (!result.Succeeded || result.Properties is null || result.Properties.ExpiresUtc is not { } expires)
        {
            return null;
        }

        var token = result.Properties.GetTokenValue(AdminAuthentication.TokenName);
        result.Properties.Items.TryGetValue(AdminAuthentication.AccountProperty, out var accountJson);
        if (string.IsNullOrEmpty(token) || accountJson is null)
        {
            return null;
        }

        var account = JsonSerializer.Deserialize<AccountSummaryResponse>(accountJson);
        if (account is null)
        {
            return null;
        }

        return new AdminSession { Account = account, AccessToken = token, ExpiresAt = expires };
    }
}
