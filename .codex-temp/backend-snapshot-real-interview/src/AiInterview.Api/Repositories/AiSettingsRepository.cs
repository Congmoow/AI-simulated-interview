using AiInterview.Api.Data;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiInterview.Api.Repositories;

public class AiSettingsRepository(ApplicationDbContext dbContext) : IAiSettingsRepository
{
    public Task<AiProviderSetting?> GetSingleAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.AiProviderSettings
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertSingleAsync(AiProviderSetting entity, CancellationToken cancellationToken = default)
    {
        var existing = await GetSingleAsync(cancellationToken);
        if (existing is null)
        {
            await dbContext.AiProviderSettings.AddAsync(entity, cancellationToken);
        }
        else
        {
            existing.Provider = entity.Provider;
            existing.BaseUrl = entity.BaseUrl;
            existing.Model = entity.Model;
            existing.ApiKeyProtected = entity.ApiKeyProtected;
            existing.ApiKeyMasked = entity.ApiKeyMasked;
            existing.IsEnabled = entity.IsEnabled;
            existing.Temperature = entity.Temperature;
            existing.MaxTokens = entity.MaxTokens;
            existing.SystemPrompt = entity.SystemPrompt;
            existing.UpdatedBy = entity.UpdatedBy;
            existing.UpdatedAt = entity.UpdatedAt;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
