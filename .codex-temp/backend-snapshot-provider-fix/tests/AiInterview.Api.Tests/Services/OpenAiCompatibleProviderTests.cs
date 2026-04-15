using AiInterview.Api.Services;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;

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
}
