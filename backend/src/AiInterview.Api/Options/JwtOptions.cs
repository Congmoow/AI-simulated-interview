namespace AiInterview.Api.Options;

public class JwtOptions
{
    public const int MinimumSecretKeyLength = 32;
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "ai-interview";

    public string Audience { get; set; } = "ai-interview-users";

    public string SecretKey { get; set; } = string.Empty;

    public int AccessTokenExpiresMinutes { get; set; } = 120;

    public int RefreshTokenExpiresDays { get; set; } = 7;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SecretKey))
        {
            throw new InvalidOperationException("缺少 JWT SecretKey 配置，请通过环境变量注入。");
        }

        if (SecretKey.Trim().Length < MinimumSecretKeyLength)
        {
            throw new InvalidOperationException($"JWT SecretKey 长度不能小于 {MinimumSecretKeyLength} 个字符。");
        }
    }
}
