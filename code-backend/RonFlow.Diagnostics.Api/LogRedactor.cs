using System.Text.RegularExpressions;

namespace RonFlow.Diagnostics.Api;

public interface ILogRedactor
{
    string Redact(string value);
}

public sealed partial class LogRedactor : ILogRedactor
{
    public string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var redacted = GitHubPatRegex().Replace(value, "github_pat_***");
        redacted = CredentialedUrlRegex().Replace(redacted, "${scheme}***@${host}");
        redacted = BearerTokenRegex().Replace(redacted, "Bearer ***");
        redacted = BasicAuthorizationRegex().Replace(redacted, "Basic ***");
        redacted = ConnectionStringPasswordRegex().Replace(redacted, "${key}=***");
        return redacted;
    }

    [GeneratedRegex(@"github_pat_[A-Za-z0-9_]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GitHubPatRegex();

    [GeneratedRegex(@"(?<scheme>https?://)[^@\s/]+@(?<host>[^/\s]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CredentialedUrlRegex();

    [GeneratedRegex(@"\bBearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"\bBasic\s+[A-Za-z0-9+/=]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BasicAuthorizationRegex();

    [GeneratedRegex(@"(?<key>\b(?:Password|Pwd)\s*)=\s*[^;,\s]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionStringPasswordRegex();
}
