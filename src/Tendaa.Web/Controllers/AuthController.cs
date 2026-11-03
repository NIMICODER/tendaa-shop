using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Tendaa.Application.Common;
using Tendaa.Core.Configurations.ConfigModels;

namespace Tendaa.Web.Controllers;

public sealed record LoginRequest(string Password);

public sealed record AntiforgeryTokenResponse(string Token);

/// <summary>Single-password cookie login for the MVP dashboard (one admin login per shop, no
/// seller-user entity yet). Same-origin SPA, so a plain cookie + antiforgery header — no JWT,
/// no CORS.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class AuthController(IOptions<DashboardOptions> dashboardOptions, IAntiforgery antiforgery) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var expected = Encoding.UTF8.GetBytes(dashboardOptions.Value.AdminPassword);
        var provided = Encoding.UTF8.GetBytes(request.Password ?? string.Empty);

        // Same constant-time pattern as the webhook's HMAC compare; the length check first
        // because FixedTimeEquals throws on mismatched lengths.
        if (provided.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(expected, provided))
        {
            return Unauthorized(TendaaResult.Unauthorized("Invalid password."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "seller")],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return Ok(TendaaResult.Success("Signed in."));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(TendaaResult.Success("Signed out."));
    }

    /// <summary>Liveness probe for the SPA: 200 when the cookie session is valid, 401 otherwise.</summary>
    [HttpGet("me")]
    public IActionResult Me() => Ok(TendaaResult.Success("Authenticated."));

    /// <summary>Hands the SPA its antiforgery request token; the cookie half is set by the same
    /// call. Anonymous so the login page can fetch a token before any session exists.</summary>
    [HttpGet("antiforgery")]
    [AllowAnonymous]
    public IActionResult AntiforgeryToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new AntiforgeryTokenResponse(tokens.RequestToken ?? string.Empty));
    }
}
