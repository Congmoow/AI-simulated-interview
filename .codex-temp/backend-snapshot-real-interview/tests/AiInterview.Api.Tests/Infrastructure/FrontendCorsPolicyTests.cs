using AiInterview.Api.Infrastructure;
using FluentAssertions;

namespace AiInterview.Api.Tests.Infrastructure;

public class FrontendCorsPolicyTests
{
    [Theory]
    [InlineData("http://localhost:3000")]
    [InlineData("http://localhost:3001")]
    [InlineData("http://127.0.0.1:3001")]
    public void IsAllowedOrigin_ShouldAllowLocalOriginsInDevelopment(string origin)
    {
        var allowed = FrontendCorsPolicy.IsAllowedOrigin(origin, "http://localhost:3000", isDevelopment: true);

        allowed.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://evil.local")]
    public void IsAllowedOrigin_ShouldRejectUnknownOriginsOutsideConfiguredValue(string origin)
    {
        var allowed = FrontendCorsPolicy.IsAllowedOrigin(origin, "http://localhost:3000", isDevelopment: false);

        allowed.Should().BeFalse();
    }
}
