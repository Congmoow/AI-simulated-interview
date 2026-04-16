using AiInterview.Api.Services.Interfaces;
using AiInterview.Api.Repositories.Interfaces;

namespace AiInterview.Api.Services;

public sealed class InterviewReportGenerationHostedService(
    IServiceScopeFactory scopeFactory,
    IInterviewReportGenerationQueue queue,
    ILogger<InterviewReportGenerationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("报告后台生成服务已启动");
        await RestorePendingJobsAsync(stoppingToken);

        await foreach (var interviewId in queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IInterviewReportGenerationService>();
                await service.ProcessInterviewAsync(interviewId, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "后台消费报告任务失败，interviewId={InterviewId}", interviewId);
            }
            finally
            {
                queue.MarkCompleted(interviewId);
            }
        }
    }

    private async Task RestorePendingJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var interviewRepository = scope.ServiceProvider.GetRequiredService<IInterviewRepository>();
        var pendingInterviewIds = await interviewRepository.GetInterviewIdsPendingReportGenerationAsync(cancellationToken);

        foreach (var interviewId in pendingInterviewIds)
        {
            await queue.EnqueueAsync(interviewId, cancellationToken);
        }

        logger.LogInformation("已恢复待生成报告任务，count={Count}", pendingInterviewIds.Count);
    }
}
