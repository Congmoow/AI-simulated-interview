using AiInterview.Api.DTOs.Reports;

namespace AiInterview.Api.Services.Interfaces;

public interface IReportService
{
    Task<InterviewReportDto> GetReportAsync(Guid userId, Guid interviewId, CancellationToken cancellationToken = default);

    Task<GrowthDto> GetGrowthAsync(Guid userId, string? positionCode, string? timeRange, string? dimension, CancellationToken cancellationToken = default);
}
