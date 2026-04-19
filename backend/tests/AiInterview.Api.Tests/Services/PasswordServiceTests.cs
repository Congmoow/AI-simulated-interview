using AiInterview.Api.Services;
using FluentAssertions;

namespace AiInterview.Api.Tests.Services;

public class PasswordServiceTests
{
    [Fact]
    public void HashPassword_ShouldCreateDifferentHash_AndVerifySuccessfully()
    {
        const string password = "TestPassword123!";
        var service = new PasswordService();

        var hash = service.HashPassword(password);

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().NotBe(password);
        service.VerifyPassword(password, hash).Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalse_WhenPasswordDoesNotMatch()
    {
        const string password = "TestPassword123!";
        var service = new PasswordService();
        var hash = service.HashPassword(password);

        var result = service.VerifyPassword("WrongTestPassword123!", hash);

        result.Should().BeFalse();
    }
}
