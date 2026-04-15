using AiInterview.Api.DTOs.Admin;

namespace AiInterview.Api.Services.Interfaces;

public interface IAiSettingsService
{
    Task<AiSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<AiSettingsDto> UpdateSettingsAsync(UpdateAiSettingsRequest request, string updatedBy, CancellationToken cancellationToken = default);

    Task<AiTestResult> TestConnectionAsync(TestAiConnectionRequest? request, CancellationToken cancellationToken = default);

    Task<IAiProvider?> BuildProviderAsync(CancellationToken cancellationToken = default);

    Task<AiRuntimeSettingsDto?> GetRuntimeSettingsAsync(CancellationToken cancellationToken = default);
}
