using Moongate.Admin.Api.Data.Accounts;
using Moongate.Admin.Api.Interfaces.Upstream;
using Moongate.Admin.Api.Services.Authentication;
using Moongate.Admin.Api.Services.Upstream;
using Moongate.Admin.Api.Types.Authentication;

namespace Moongate.Admin.Api.Extensions;

public static class AccountEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapAdminAccounts(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/accounts", ListAsync)
            .RequireAuthorization(AdminAuthentication.AccountPolicy)
            .Produces<AccountPageResponse>();
        endpoints.MapPost("/api/accounts", CreateAsync)
            .RequireAuthorization(AdminAuthentication.AccountPolicy)
            .Produces<AccountSummaryResponse>(StatusCodes.Status201Created);
        endpoints.MapPost("/api/accounts/{id}/revoke-sessions", RevokeAsync)
            .RequireAuthorization(AdminAuthentication.AccountPolicy)
            .Produces(StatusCodes.Status204NoContent);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        uint? pageSize, uint? afterAccountId, HttpContext context, IMoongateAdminClient client,
        CancellationToken cancellationToken
    )
    {
        var session = context.GetAdminSession();
        return TypedResults.Ok(
            await client.ListAccountsAsync(pageSize ?? 0, afterAccountId ?? 0, session.AccessToken, cancellationToken)
        );
    }

    private static async Task<IResult> CreateAsync(
        CreateAccountRequest request, HttpContext context, IMoongateAdminClient client, CancellationToken cancellationToken
    )
    {
        var session = context.GetAdminSession();
        var account = await client.CreateAccountAsync(request, session.AccessToken, cancellationToken);
        return TypedResults.Json(account, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> RevokeAsync(
        uint id, HttpContext context, IMoongateAdminClient client, JwtSessionService sessions,
        CancellationToken cancellationToken
    )
    {
        AdminRequestValidator.ValidateAccountId(id);
        var session = context.GetAdminSession();
        await client.RevokeAccountSessionsAsync(id, session.AccessToken, cancellationToken);
        if (id == session.Account.AccountId)
        {
            sessions.Remove(session.SessionId);
        }

        return TypedResults.NoContent();
    }
}
