using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.TestSupport.Authentication;

public static class AuthenticatedApiClient
{
    public static HttpClient Create(AdminApiFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }
    public static async Task<HttpClient> CreateAsync(AdminApiFactory factory)
    {
        var client = Create(factory);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "fixture-only" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }
}
