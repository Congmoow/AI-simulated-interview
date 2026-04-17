namespace AiInterview.Api.Services.Interfaces;

public interface IInterviewReportGenerationService
{
    Task ProcessInterviewAsync(Guid interviewId, CancellationToken cancellationToken = default);
}
