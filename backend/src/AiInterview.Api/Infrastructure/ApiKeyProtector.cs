using Microsoft.AspNetCore.DataProtection;

namespace AiInterview.Api.Infrastructure;

public interface IApiKeyProtector
{
    string Protect(string plainKey);
    string Unprotect(string protectedKey);
    string Mask(string plainKey);
}

public class ApiKeyProtector(IDataProtectionProvider provider) : IApiKeyProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("AiProviderApiKey");

    public string Protect(string plainKey) => _protector.Protect(plainKey);

    public string Unprotect(string protectedKey) => _protector.Unprotect(protectedKey);

    public string Mask(string plainKey)
    {
        if (string.IsNullOrEmpty(plainKey))
        {
            return string.Empty;
        }

        if (plainKey.Length <= 8)
        {
            return new string('*', plainKey.Length);
        }

        var prefix = plainKey[..Math.Min(6, plainKey.Length / 3)];
        var suffix = plainKey[^Math.Min(4, plainKey.Length / 4)..];
        return $"{prefix}...{suffix}";
    }
}
