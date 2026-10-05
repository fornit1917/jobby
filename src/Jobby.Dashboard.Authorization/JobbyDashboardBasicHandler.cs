using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobby.Dashboard.Authorization;

internal sealed class JobbyDashboardBasicHandler : AuthenticationHandler<JobbyDashboardBasicOptions>
{
    public JobbyDashboardBasicHandler(
        IOptionsMonitor<JobbyDashboardBasicOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? header = Request.Headers.Authorization;
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
        }
        catch (FormatException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Malformed Basic credentials."));
        }

        var sep = decoded.IndexOf(':');
        if (sep < 0)
        {
            return Task.FromResult(AuthenticateResult.Fail("Malformed Basic credentials."));
        }

        var username = decoded[..sep];
        var password = decoded[(sep + 1)..];
        if (!BasicCredentials.Validate(username, password, Options.Username, Options.PasswordHash))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid credentials."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, username)], Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        var realm = Options.Realm.Replace("\\", @"\\").Replace("\"", "\\\"");
        Response.Headers.Append("WWW-Authenticate", $"Basic realm=\"{realm}\", charset=\"UTF-8\"");
        return Task.CompletedTask;
    }
}