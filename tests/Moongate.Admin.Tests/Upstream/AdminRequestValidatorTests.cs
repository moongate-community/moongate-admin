using Microsoft.AspNetCore.Http;
using Moongate.Admin.Api.Data.Accounts;
using Moongate.Admin.Api.Services.Upstream;
using Moongate.Admin.Api.Types.Accounts;

namespace Moongate.Admin.Tests.Upstream;

public class AdminRequestValidatorTests
{
    [Theory]
    [InlineData(null, "valid")]
    [InlineData("", "valid")]
    [InlineData(" ", "valid")]
    [InlineData("admin", null)]
    [InlineData("admin", "")]
    [InlineData("admin", " ")]
    [InlineData("ad\0min", "valid")]
    [InlineData("admin", "bad\0value")]
    public void ValidateCredentials_InvalidFormat_Rejects(string? user, string? password)
    {
        Assert.Throws<BadHttpRequestException>(() => AdminRequestValidator.ValidateCredentials(user, password));
    }
    [Fact]
    public void ValidateCredentials_MultibytePassword_EnforcesByteLimit()
    {
        AdminRequestValidator.ValidateCredentials("Admin", new string('é', 512));
        Assert.Throws<BadHttpRequestException>(() => AdminRequestValidator.ValidateCredentials("Admin", new string('é', 513)));
        Assert.Throws<BadHttpRequestException>(() => AdminRequestValidator.ValidateCredentials(new string('a', 256), "valid"));
    }
    [Fact]
    public void ValidateCredentials_PaddedCaseDistinctNames_AcceptsOriginal()
    {
        AdminRequestValidator.ValidateCredentials(" Admin ", " valid ");
        AdminRequestValidator.ValidateCredentials("admin", "valid");
    }
    [Fact]
    public void ValidateAccountCreation_UndefinedRole_Rejects()
    {
        Assert.Throws<BadHttpRequestException>(() => AdminRequestValidator.ValidateAccountCreation(new CreateAccountRequest
        {
            Username = "admin", Password = "valid", AccountType = (AdminAccountType)99
        }));
    }
    [Fact]
    public void ValidatePagination_Limits_RejectsOverflow()
    {
        AdminRequestValidator.ValidatePagination(0);
        AdminRequestValidator.ValidatePagination(200);
        Assert.Throws<BadHttpRequestException>(() => AdminRequestValidator.ValidatePagination(201));
        Assert.Throws<BadHttpRequestException>(() => AdminRequestValidator.ValidateAccountId(0));
    }
}
