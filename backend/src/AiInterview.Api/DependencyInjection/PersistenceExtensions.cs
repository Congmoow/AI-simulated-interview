using AiInterview.Api.Data;
using AiInterview.Api.Repositories;
using AiInterview.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;

namespace AiInterview.Api.DependencyInjection;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresConnectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("缺少 PostgreSQL 连接字符串配置");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(postgresConnectionString, npgsqlOptions => npgsqlOptions.UseVector())
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<IInterviewRepository, InterviewRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();
        services.AddScoped<IAiSettingsRepository, AiSettingsRepository>();

        return services;
    }
}
