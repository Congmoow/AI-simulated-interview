using AiInterview.Api.Models.Entities;

namespace AiInterview.Api.Repositories.Interfaces;

public interface IAiSettingsRepository
{
    Task<AiProviderSetting?> GetSingleAsync(CancellationToken cancellationToken = default);

    Task UpsertSingleAsync(AiProviderSetting entity, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
