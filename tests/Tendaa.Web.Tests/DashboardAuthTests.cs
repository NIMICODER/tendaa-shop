using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tendaa.Web.Tests;

/// <summary>Proves the dashboard auth wiring with the real login flow (no fake auth handler):
/// correct password signs in, wrong password doesn't, protected routes 401 without a session,
/// unsafe /api verbs need the antiforgery header. Uses <see cref="WebhooksTestFactory"/>'s
/// explicit dummy config — never real user-secrets.</summary>
public class DashboardAuthTests
{
    private static HttpClient CreateClient(WebhooksTestFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/antiforgery");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("token").GetString()!;
    }

    private static void AddAntiforgeryHeader(HttpRequestMessage request, string token) =>
        request.Headers.TryAddWithoutValidation("RequestVerificationToken", token);

    private static HttpRequestMessage LoginRequest(string password, string? antiforgeryToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { password }),
        };
        if (antiforgeryToken is not null)
        {
            AddAntiforgeryHeader(request, antiforgeryToken);
        }

        return request;
    }

    [Fact]
    public async Task Health_WithoutLogin_Returns200()
    {
        await using var factory = new WebhooksTestFactory();
        await factory.InitializeDatabaseAsync();
        var client = CreateClient(factory);

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthMe_WithoutLogin_Returns401()
    {
        await using var factory = new WebhooksTestFactory();
        await factory.InitializeDatabaseAsync();
        var client = CreateClient(factory);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Media_WithoutLogin_Returns401()
    {
        await using var factory = new WebhooksTestFactory();
        await factory.InitializeDatabaseAsync();
        var client = CreateClient(factory);

        var response = await client.GetAsync("/api/media/whatever");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401AndStaysLoggedOut()
    {
        await using var factory = new WebhooksTestFactory();
        await factory.InitializeDatabaseAsync();
        var client = CreateClient(factory);

        var token = await GetAntiforgeryTokenAsync(client);
        var login = await client.SendAsync(LoginRequest("wrong-password", token));

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_WithoutAntiforgeryHeader_Returns400()
    {
        await using var factory = new WebhooksTestFactory();
        await factory.InitializeDatabaseAsync();
        var client = CreateClient(factory);

        var response = await client.SendAsync(LoginRequest("test-admin-password", antiforgeryToken: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_CorrectPassword_SignsIn_LogoutEndsSession()
    {
        await using var factory = new WebhooksTestFactory();
        await factory.InitializeDatabaseAsync();
        var client = CreateClient(factory);

        var anonymousToken = await GetAntiforgeryTokenAsync(client);
        var login = await client.SendAsync(LoginRequest("test-admin-password", anonymousToken));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        // Antiforgery tokens are identity-bound: the anonymous token can't authorize a
        // request sent as the signed-in seller, so fetch a fresh one for the logout POST.
        var signedInToken = await GetAntiforgeryTokenAsync(client);
        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        AddAntiforgeryHeader(logout, signedInToken);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(logout)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    /// <summary>Guards the "every /api route is protected" rule structurally: any new dashboard
    /// controller must explicitly choose [Authorize] or [AllowAnonymous] — forgetting both fails
    /// here instead of silently shipping an open endpoint.</summary>
    [Fact]
    public void EveryApiController_ExplicitlyDeclaresAuthorization()
    {
        var apiControllers = typeof(Program).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(ControllerBase)))
            .Where(t => t.GetCustomAttributes(typeof(RouteAttribute), inherit: true)
                .OfType<RouteAttribute>()
                .Any(r => r.Template?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true))
            .ToList();

        Assert.NotEmpty(apiControllers);

        foreach (var controller in apiControllers)
        {
            var declares = controller.GetCustomAttributes(inherit: true)
                .Any(a => a is AuthorizeAttribute or AllowAnonymousAttribute);
            Assert.True(declares, $"{controller.Name} must declare [Authorize] or [AllowAnonymous].");
        }
    }
}
