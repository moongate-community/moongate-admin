using Moongate.Admin.Api.Data.Internal.Sessions;
using Moongate.Admin.Api.Types.Authentication;

namespace Moongate.Admin.Api.Services.Authentication;

public sealed class AdminSessionAccessor
{
    public Task<AdminSession?> GetAsync(HttpContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(context.Items[AdminAuthentication.SessionItem] as AdminSession);
    }
}
