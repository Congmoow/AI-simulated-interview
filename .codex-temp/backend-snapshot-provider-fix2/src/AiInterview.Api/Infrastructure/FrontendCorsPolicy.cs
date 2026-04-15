namespace AiInterview.Api.Infrastructure;

public static class FrontendCorsPolicy
{
    public static bool IsAllowedOrigin(string origin, string configuredOrigin, bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (string.Equals(origin, configuredOrigin, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!isDevelopment || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }
}
