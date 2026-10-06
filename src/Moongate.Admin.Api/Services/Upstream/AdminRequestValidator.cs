using System.Text;
using Moongate.Admin.Api.Data.Accounts;

namespace Moongate.Admin.Api.Services.Upstream;

public static class AdminRequestValidator
{
    private const int MaximumUsernameLength = 255;
    private const int MaximumPasswordBytes = 1024;
    private const uint MaximumPageSize = 200;

    public static void ValidateCredentials(string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > MaximumUsernameLength || username.Contains('\0') ||
            string.IsNullOrWhiteSpace(password) || password.Contains('\0') ||
            Encoding.UTF8.GetByteCount(password) > MaximumPasswordBytes)
        {
            throw new BadHttpRequestException("Invalid account credentials format.");
        }
    }

    public static void ValidateAccountCreation(CreateAccountRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCredentials(request.Username, request.Password);
        if (request.AccountType is { } role && !Enum.IsDefined(role))
        {
            throw new BadHttpRequestException("Invalid account type.");
        }
    }

    public static void ValidatePagination(uint pageSize)
    {
        if (pageSize > MaximumPageSize)
        {
            throw new BadHttpRequestException("Page size cannot exceed 200.");
        }
    }

    public static void ValidateAccountId(uint accountId)
    {
        if (accountId == 0)
        {
            throw new BadHttpRequestException("Account ID must be nonzero.");
        }
    }
}
