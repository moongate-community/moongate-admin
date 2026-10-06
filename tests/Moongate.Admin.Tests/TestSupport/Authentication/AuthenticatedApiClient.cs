using System.Net;
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
    public static async Task RefreshCsrfAsync(HttpClient client)
    {
        var body = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", body.GetProperty("requestToken").GetString());
    }
    public static async Task<HttpClient> CreateAsync(AdminApiFactory factory)
    {
        var client = Create(factory);
        await RefreshCsrfAsync(client);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "fixture-only" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await RefreshCsrfAsync(client);
        return client;
    }
}
