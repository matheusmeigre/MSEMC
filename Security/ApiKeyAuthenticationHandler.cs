using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MSEMC.Security;

/// <summary>
/// Custom authentication handler that validates requests via API Key in HTTP header.
/// Lightweight alternative to JWT for service-to-service communication.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<Configuration.ApiKeyOptions> apiKeyOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";

    private readonly Configuration.ApiKeyOptions _apiKeyOptions = apiKeyOptions.Value;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(_apiKeyOptions.HeaderName, out var providedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail(
                $"Missing header: {_apiKeyOptions.HeaderName}"));
        }

        if (!string.Equals(providedKey, _apiKeyOptions.Key, StringComparison.Ordinal))
        {
            Logger.LogWarning(
                "Invalid API key provided from {RemoteIp}",
                Context.Connection.RemoteIpAddress);

            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "ApiKeyUser"),
            new Claim(ClaimTypes.AuthenticationMethod, SchemeName)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.Append("WWW-Authenticate", $"ApiKey header=\"{_apiKeyOptions.HeaderName}\"");
        return Task.CompletedTask;
    }
}
