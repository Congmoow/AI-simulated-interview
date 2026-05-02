using AiInterview.Api.Services;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Authentication;

namespace AiInterview.Api.Tests.Services;

public class OpenAiCompatibleProviderTests
{
    [Fact]
    public void Constructor_ShouldConfigureHttpClientForHttp11AndBearerAuth()
    {
        var provider = new OpenAiCompatibleProvider(
            "https://example.com/v1",
            "secret-key",
            "gpt-test",
            0.2f,
            512);

        var httpClientField = typeof(OpenAiCompatibleProvider)
            .GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic);

        httpClientField.Should().NotBeNull();

        var httpClient = httpClientField!.GetValue(provider).Should().BeOfType<HttpClient>().Subject;

        httpClient.BaseAddress.Should().Be(new Uri("https://example.com/v1/"));
        httpClient.DefaultRequestHeaders.Authorization.Should().BeEquivalentTo(
            new AuthenticationHeaderValue("Bearer", "secret-key"));
        httpClient.DefaultRequestVersion.Should().Be(HttpVersion.Version11);
        httpClient.DefaultVersionPolicy.Should().Be(HttpVersionPolicy.RequestVersionOrLower);
    }

    [Fact]
    public void Factory_ShouldUseSharedHandlerWithTls12AndTls13()
    {
        var handlerField = typeof(OpenAiCompatibleHttpClientFactory)
            .GetField("SharedHandler", BindingFlags.Static | BindingFlags.NonPublic);

        handlerField.Should().NotBeNull();

        var handler = handlerField!.GetValue(null).Should().BeOfType<SocketsHttpHandler>().Subject;

        handler.SslOptions.EnabledSslProtocols.Should().Be(SslProtocols.Tls12 | SslProtocols.Tls13);
    }
}
