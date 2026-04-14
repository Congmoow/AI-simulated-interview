using AiInterview.Api.Options;
using AiInterview.Api.Repositories.Interfaces;
using Microsoft.Extensions.Options;

namespace AiInterview.Api.Services;

public class StaleDocumentCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<KnowledgeProcessingOptions> options,
    ILogger<StaleDocumentCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "stale 扫描服务已启动（interval={IntervalMinutes}分钟，threshold={ThresholdMinutes}分钟）",
            options.Value.ScanIntervalMinutes,
            options.Value.StaleThresholdMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(options.Value.ScanIntervalMinutes), stoppingToken);
            await ScanAsync(stoppingToken);
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-options.Value.StaleThresholdMinutes);
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAdminRepository>();

        try
        {
            var count = await repo.MarkStaleDocumentsAsFailedAsync(cutoff, ct);

            if (count > 0)
                logger.LogWarning(
                    "stale 扫描：已将 {Count} 个超时文档标记为 failed（threshold={ThresholdMinutes}分钟，cutoff={Cutoff:O}）",
                    count, options.Value.StaleThresholdMinutes, cutoff);
            else
                logger.LogDebug(
                    "stale 扫描：无超时文档（threshold={ThresholdMinutes}分钟，cutoff={Cutoff:O}）",
                    options.Value.StaleThresholdMinutes, cutoff);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "stale 扫描异常，下次扫描继续");
        }
    }
}
