namespace AiInterview.Api.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "ai-interview";

    public string Audience { get; set; } = "ai-interview-users";

    public string SecretKey { get; set; } = string.Empty;

    public int AccessTokenExpiresMinutes { get; set; } = 120;

    public int RefreshTokenExpiresDays { get; set; } = 7;
}
