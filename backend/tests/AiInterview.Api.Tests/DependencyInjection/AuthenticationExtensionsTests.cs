using System.Text;
using AiInterview.Api.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Api.Tests.DependencyInjection;

public class AuthenticationExtensionsTests
{
    [Fact]
    public void AddAppAuthentication_ShouldRejectSecretKeyFromJsonConfiguration()
    {
        using var secretScope = new JwtSecretKeyEnvironmentScope(null);
        var configuration = BuildJsonConfiguration("""
        {
          "Jwt": {
            "Issuer": "ai-interview",
            "Audience": "ai-interview-users",
            "SecretKey": "json-configured-secret-key-with-32chars",
            "AccessTokenExpiresMinutes": 120,
            "RefreshTokenExpiresDays": 7
          }
        }
        """);
        var services = new ServiceCollection();

        Action act = () => services.AddAppAuthentication(configuration);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddAppAuthentication_ShouldRejectShortSecretKeyFromEnvironment()
    {
        using var secretScope = new JwtSecretKeyEnvironmentScope("too-short-secret");
        var configuration = BuildEmptyConfiguration();
        var services = new ServiceCollection();

        Action act = () => services.AddAppAuthentication(configuration);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddAppAuthentication_ShouldAllowValidSecretKeyFromEnvironment()
    {
        using var secretScope = new JwtSecretKeyEnvironmentScope("this-is-a-valid-env-secret-key-123456");
        var configuration = BuildEmptyConfiguration();
        var services = new ServiceCollection();

        Action act = () => services.AddAppAuthentication(configuration);

        act.Should().NotThrow();
    }

    private static IConfiguration BuildEmptyConfiguration()
    {
        return new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
    }

    private static IConfiguration BuildJsonConfiguration(string json)
    {
        return new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .AddEnvironmentVariables()
            .Build();
    }

    private sealed class JwtSecretKeyEnvironmentScope : IDisposable
    {
        private readonly string? _originalValue;

        public JwtSecretKeyEnvironmentScope(string? value)
        {
            _originalValue = Environment.GetEnvironmentVariable("Jwt__SecretKey");
            Environment.SetEnvironmentVariable("Jwt__SecretKey", value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("Jwt__SecretKey", _originalValue);
        }
    }
}
