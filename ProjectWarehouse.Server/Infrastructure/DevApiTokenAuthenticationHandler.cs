using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Infrastructure;

public class DevApiTokenOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "DevApiToken";

    /// <summary>Policy scheme that routes a request to <see cref="SchemeName"/> or to JWT bearer.</summary>
    public const string SelectorSchemeName = "BearerOrDevApiToken";

    public string Token { get; set; } = "";

    public string Username { get; set; } = "admin";

    // Loopback only: the token is committed to the repo, so a box mistakenly run as Development must not
    // hand admin to the network. Proxied requests are refused outright — a local proxy that drops
    // X-Forwarded-For would otherwise make every client look like loopback. ForwardedHeadersMiddleware
    // moves a consumed X-Forwarded-For to X-Original-For, hence both.
    public static bool Matches(HttpRequest request, string token) =>
        !string.IsNullOrEmpty(token)
        && !request.Headers.ContainsKey("X-Forwarded-For")
        && !request.Headers.ContainsKey("X-Original-For")
        && request.HttpContext.Connection.RemoteIpAddress is { } remote
        && IPAddress.IsLoopback(remote)
        && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(request.Headers.Authorization.ToString()),
            Encoding.UTF8.GetBytes($"Bearer {token}"));
}

/// <summary>
/// Development only: a fixed bearer string authenticates as <see cref="DevApiTokenOptions.Username"/> with the
/// same claims a real access token would carry, read fresh on every request.
/// </summary>
public class DevApiTokenAuthenticationHandler(
    IOptionsMonitor<DevApiTokenOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService) : AuthenticationHandler<DevApiTokenOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!DevApiTokenOptions.Matches(Request, Options.Token))
            return AuthenticateResult.NoResult();

        var user = await userManager.FindByNameAsync(Options.Username);
        if (user is null)
            return AuthenticateResult.Fail($"Dev API token user '{Options.Username}' does not exist.");

        var claims = await tokenService.BuildClaimsAsync(user);
        var identity = new ClaimsIdentity(claims, Scheme.Name, "name", null);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
