using AiInterview.Api.Infrastructure;
using AiInterview.Api.Options;
using AiInterview.Api.Services;
using AiInterview.Api.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AiInterview.Api.DependencyInjection;

public static class ExternalDependenciesExtensions
{
    public static IServiceCollection AddExternalDependencies(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var dataProtectionKeysPath = configuration.GetValue<string>("DataProtection:KeysPath")
            ?? throw new InvalidOperationException("缺少 DataProtection KeysPath 配置");
        if (!Path.IsPathRooted(dataProtectionKeysPath))
        {
            dataProtectionKeysPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, dataProtectionKeysPath));
        }

        Directory.CreateDirectory(dataProtectionKeysPath);
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
            .SetApplicationName("AiInterview");
        services.AddSingleton<IApiKeyProtector, ApiKeyProtector>();

        services.Configure<AiServiceOptions>(configuration.GetSection(AiServiceOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<KnowledgeProcessingOptions>(configuration.GetSection(KnowledgeProcessingOptions.SectionName));

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("缺少 Redis 连接字符串配置");
        var frontendUrl = configuration.GetValue<string>("App:FrontendUrl") ?? "http://localhost:3000";

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddCors(options =>
        {
            options.AddPolicy("frontend", policy =>
            {
                policy
                    .SetIsOriginAllowed(origin =>
                        FrontendCorsPolicy.IsAllowedOrigin(origin, frontendUrl, environment.IsDevelopment()))
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        services.AddHttpClient<IAiIntegrationService, AiIntegrationService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<AiServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        return services;
    }
}
