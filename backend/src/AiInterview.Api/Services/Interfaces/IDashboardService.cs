using AiInterview.Api.DTOs.Dashboard;

namespace AiInterview.Api.Services.Interfaces;

public interface IDashboardService
{
    Task<DashboardInsightsDto> GetInsightsAsync(Guid userId, CancellationToken cancellationToken = default);
}
