using AiInterview.Api.Services.Interfaces;
using StackExchange.Redis;

namespace AiInterview.Api.Services;

public class RedisRefreshTokenStore(IConnectionMultiplexer connectionMultiplexer) : IRefreshTokenStore
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    public Task StoreAsync(string refreshToken, Guid userId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        var expiry = expiresAt - DateTimeOffset.UtcNow;
        return _database.StringSetAsync(BuildKey(refreshToken), userId.ToString(), expiry);
    }

    public async Task<Guid?> GetUserIdAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var value = await _database.StringGetAsync(BuildKey(refreshToken));
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    public Task RemoveAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return _database.KeyDeleteAsync(BuildKey(refreshToken));
    }

    private static string BuildKey(string refreshToken) => $"refresh_token:{refreshToken}";
}
