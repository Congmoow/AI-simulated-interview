using AiInterview.Api.Constants;
using AiInterview.Api.DTOs.Reports;
using AiInterview.Api.Mappings;
using AiInterview.Api.Middleware;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services.Interfaces;

namespace AiInterview.Api.Services;

public class ReportService(IInterviewRepository interviewRepository, IReportRepository reportRepository) : IReportService
{
    public async Task<InterviewReportDto> GetReportAsync(Guid userId, Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await interviewRepository.GetByIdAsync(interviewId, cancellationToken);
        if (interview is null || interview.UserId != userId)
        {
            throw new AppException(ErrorCodes.InterviewNotFound, "面试不存在", StatusCodes.Status404NotFound);
        }

        var report = await reportRepository.GetReportByInterviewIdAsync(interviewId, cancellationToken)
            ?? throw new AppException(ErrorCodes.ReportNotGenerated, "报告尚未生成", StatusCodes.Status404NotFound);

        var score = await reportRepository.GetScoreByInterviewIdAsync(interviewId, cancellationToken);
        return ApplicationMapper.ToInterviewReportDto(report, score);
    }

    public async Task<GrowthDto> GetGrowthAsync(Guid userId, string? positionCode, string? timeRange, string? dimension, CancellationToken cancellationToken = default)
    {
        var reports = await reportRepository.GetUserReportsAsync(userId, positionCode, cancellationToken);
        reports = ApplyTimeRange(reports, timeRange);

        if (reports.Count == 0)
        {
            return new GrowthDto
            {
                Summary = new GrowthSummaryDto(),
                Trend = [],
                WeaknessTracking = []
            };
        }

        var scorePairs = new List<(DateOnly Date, decimal OverallScore, Dictionary<string, DimensionScoreDto> Dimensions)>();
        foreach (var report in reports)
        {
            var score = await reportRepository.GetScoreByInterviewIdAsync(report.InterviewId, cancellationToken);
            var dimensions = ApplicationMapper.DeserializeObject<Dictionary<string, DimensionScoreDto>>(score?.DimensionScores, []);

            if (!string.IsNullOrWhiteSpace(dimension) && dimensions.Count > 0 && !dimensions.ContainsKey(dimension))
            {
                continue;
            }

            scorePairs.Add((DateOnly.FromDateTime(report.GeneratedAt.UtcDateTime), report.OverallScore, dimensions));
        }

        if (scorePairs.Count == 0)
        {
            return new GrowthDto
            {
                Summary = new GrowthSummaryDto(),
                Trend = [],
                WeaknessTracking = []
            };
        }

        var latestDimensions = scorePairs.Last().Dimensions;
        var strongest = latestDimensions.OrderByDescending(x => x.Value.Score).FirstOrDefault().Key;
        var weakest = latestDimensions.OrderBy(x => x.Value.Score).FirstOrDefault().Key;
        var averageScore = Math.Round(scorePairs.Average(x => x.OverallScore), 2);
        var firstScore = scorePairs.First().OverallScore;
        var lastScore = scorePairs.Last().OverallScore;

        return new GrowthDto
        {
            Summary = new GrowthSummaryDto
            {
                TotalInterviews = scorePairs.Count,
                AverageScore = averageScore,
                ScoreChange = Math.Round(lastScore - firstScore, 2),
                StrongestDimension = strongest,
                WeakestDimension = weakest
            },
            Trend = scorePairs.Select(x => new GrowthTrendItemDto
            {
                Date = x.Date,
                OverallScore = x.OverallScore,
                Dimensions = x.Dimensions.ToDictionary(k => k.Key, v => v.Value.Score)
            }).ToArray(),
            WeaknessTracking = weakest is null
                ? []
                : [new WeaknessTrackingDto
                {
                    Dimension = weakest,
                    CurrentScore = latestDimensions.GetValueOrDefault(weakest)?.Score ?? 0,
                    Trend = lastScore >= firstScore ? "improving" : "stable",
                    RecentInterviews = Math.Min(scorePairs.Count, 3)
                }]
        };
    }

    private static List<Models.Entities.InterviewReport> ApplyTimeRange(List<Models.Entities.InterviewReport> reports, string? timeRange)
    {
        var now = DateTimeOffset.UtcNow;
        var start = timeRange switch
        {
            "week" => now.AddDays(-7),
            "month" => now.AddMonths(-1),
            "quarter" => now.AddMonths(-3),
            _ => DateTimeOffset.MinValue
        };

        return reports.Where(x => x.GeneratedAt >= start).OrderBy(x => x.GeneratedAt).ToList();
    }
}
