using AiInterview.Api.Hubs;
using AiInterview.Api.Services;
using AiInterview.Api.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Api.DependencyInjection;

public static class ApplicationServicesExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSignalR();

        services.AddScoped<PasswordService>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<IRefreshTokenStore, RedisRefreshTokenStore>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPositionService, PositionService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<IInterviewService, InterviewService>();
        services.AddScoped<IInterviewReportGenerationService, InterviewReportGenerationService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ISeedDataService, SeedDataService>();
        services.AddScoped<IKnowledgeService, KnowledgeService>();
        services.AddScoped<IAiSettingsService, AiSettingsService>();
        services.AddSingleton<IInterviewReportGenerationQueue, InterviewReportGenerationQueue>();
        services.AddHostedService<StaleDocumentCleanupService>();
        services.AddHostedService<InterviewReportGenerationHostedService>();

        return services;
    }
}
