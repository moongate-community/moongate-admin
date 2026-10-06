using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
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
        endpoints.MapGet("/api/accounts", ListAsync).RequireAuthorization(AdminAuthentication.AccountPolicy);
        endpoints.MapPost("/api/accounts", CreateAsync)
            .RequireAuthorization(AdminAuthentication.AccountPolicy)
            .AddEndpointFilter<CsrfValidationFilter>()
            .Produces<AccountSummaryResponse>(StatusCodes.Status201Created);
        endpoints.MapPost("/api/accounts/{id}/revoke-sessions", RevokeAsync)
            .RequireAuthorization(AdminAuthentication.AccountPolicy)
            .AddEndpointFilter<CsrfValidationFilter>();
        return endpoints;
    }

    private static async Task<Ok<AccountPageResponse>> ListAsync(
        uint? pageSize, uint? afterAccountId, HttpContext context,
        IMoongateAdminClient client, AdminSessionAccessor sessions, CancellationToken cancellationToken
    )
    {
        var session = await sessions.GetAsync(context, cancellationToken)
                      ?? throw new BadHttpRequestException("Authentication required.", StatusCodes.Status401Unauthorized);
        return TypedResults.Ok(
            await client.ListAccountsAsync(pageSize ?? 0, afterAccountId ?? 0, session.AccessToken, cancellationToken)
        );
    }

    private static async Task<IResult> CreateAsync(
        CreateAccountRequest request, HttpContext context,
        IMoongateAdminClient client, AdminSessionAccessor sessions, CancellationToken cancellationToken
    )
    {
        var session = await sessions.GetAsync(context, cancellationToken)
                      ?? throw new BadHttpRequestException("Authentication required.", StatusCodes.Status401Unauthorized);
        var account = await client.CreateAccountAsync(request, session.AccessToken, cancellationToken);
        return TypedResults.Json(account, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<NoContent> RevokeAsync(
        uint id, HttpContext context,
        IMoongateAdminClient client, AdminSessionAccessor sessions, CancellationToken cancellationToken
    )
    {
        AdminRequestValidator.ValidateAccountId(id);
        var session = await sessions.GetAsync(context, cancellationToken)
                      ?? throw new BadHttpRequestException("Authentication required.", StatusCodes.Status401Unauthorized);
        await client.RevokeAccountSessionsAsync(id, session.AccessToken, cancellationToken);
        if (id == session.Account.AccountId)
        {
            await context.SignOutAsync();
        }

        return TypedResults.NoContent();
    }
}
