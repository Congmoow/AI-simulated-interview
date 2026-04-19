using System.Text;
using AiInterview.Api.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AiInterview.Api.DependencyInjection;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddAppAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        jwtOptions.SecretKey = ResolveSecretKey(configuration);
        jwtOptions.Validate();

        services.Configure<JwtOptions>(options =>
        {
            configuration.GetSection(JwtOptions.SectionName).Bind(options);
            options.SecretKey = jwtOptions.SecretKey;
        });

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrWhiteSpace(accessToken) && path.StartsWithSegments("/hubs/interview"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }

    private static string ResolveSecretKey(IConfiguration configuration)
    {
        const string key = $"{JwtOptions.SectionName}:SecretKey";
        if (configuration is not IConfigurationRoot configurationRoot)
        {
            return configuration[key] ?? string.Empty;
        }

        foreach (var provider in configurationRoot.Providers.Reverse())
        {
            if (!provider.TryGet(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (provider is EnvironmentVariablesConfigurationProvider)
            {
                return value;
            }

            throw new InvalidOperationException("JWT SecretKey 必须通过环境变量注入，不能使用源码中的默认值。");
        }

        return string.Empty;
    }
}
