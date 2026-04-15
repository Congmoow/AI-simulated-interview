using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;

namespace AiInterview.Api.Services;

internal static class OpenAiCompatibleHttpClientFactory
{
    internal static SocketsHttpHandler CreateHandler()
    {
        return new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            SslOptions =
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }
        };
    }

    internal static HttpClient CreateClient(string baseUrl, string apiKey)
    {
        var client = new HttpClient(CreateHandler())
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        return client;
    }
}
