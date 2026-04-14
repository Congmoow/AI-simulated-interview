using AiInterview.Api.Models.Entities;
using AiInterview.Api.Options;
using AiInterview.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;

namespace AiInterview.Api.Tests.Services;

public class JwtTokenServiceTests
{
    [Fact]
    public void CreateAccessToken_ShouldContainExpectedClaims()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "ai-interview",
            Audience = "ai-interview-users",
            SecretKey = "this-is-a-long-secret-key-for-tests-123456",
            AccessTokenExpiresMinutes = 120,
            RefreshTokenExpiresDays = 7
        });

        var service = new JwtTokenService(options);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "zhangsan",
            Email = "zhangsan@example.com",
            Role = "user",
            TargetPositionCode = "java-backend"
        };

        var token = service.CreateAccessToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().ContainSingle(x => x.Type == JwtRegisteredClaimNames.Sub && x.Value == user.Id.ToString());
        jwt.Claims.Should().ContainSingle(x => x.Type == JwtRegisteredClaimNames.UniqueName && x.Value == user.Username);
        jwt.Claims.Should().ContainSingle(x => x.Type == "role" && x.Value == user.Role);
        jwt.Claims.Should().ContainSingle(x => x.Type == "position_id" && x.Value == user.TargetPositionCode);
    }

    [Fact]
    public void CreateRefreshToken_ShouldReturnNonEmptyToken()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "ai-interview",
            Audience = "ai-interview-users",
            SecretKey = "this-is-a-long-secret-key-for-tests-123456",
            AccessTokenExpiresMinutes = 120,
            RefreshTokenExpiresDays = 7
        });

        var service = new JwtTokenService(options);

        var token = service.CreateRefreshToken();

        token.Should().NotBeNullOrWhiteSpace();
        token.Length.Should().BeGreaterThan(20);
    }
}
