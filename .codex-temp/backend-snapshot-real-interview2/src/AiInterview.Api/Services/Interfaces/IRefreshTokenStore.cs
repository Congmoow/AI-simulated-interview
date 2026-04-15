namespace AiInterview.Api.Services.Interfaces;

public interface IRefreshTokenStore
{
    Task StoreAsync(string refreshToken, Guid userId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);

    Task<Guid?> GetUserIdAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RemoveAsync(string refreshToken, CancellationToken cancellationToken = default);
}
