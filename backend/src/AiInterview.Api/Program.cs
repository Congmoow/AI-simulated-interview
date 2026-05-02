using AiInterview.Api.Data;
using AiInterview.Api.DependencyInjection;
using AiInterview.Api.Hubs;
using AiInterview.Api.Middleware;
using AiInterview.Api.Options;
using AiInterview.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));

builder.Services
    .AddApiPresentation()
    .AddExternalDependencies(builder.Configuration, builder.Environment)
    .AddPersistence(builder.Configuration)
    .AddAppAuthentication(builder.Configuration)
    .AddApplicationServices()
    .AddAppSwagger();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("frontend");

if (app.Environment.IsDevelopment())
{
    app.UseAppSwagger();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<InterviewHub>("/hubs/interview");
app.MapGet("/health", async (HttpContext httpContext, ApplicationDbContext dbContext, IConnectionMultiplexer redis, CancellationToken cancellationToken) =>
{
    var databaseHealthy = await dbContext.Database.CanConnectAsync(cancellationToken);
    var redisHealthy = false;

    try
    {
        await redis.GetDatabase().PingAsync();
        redisHealthy = true;
    }
    catch
    {
        redisHealthy = false;
    }

    var overallHealthy = databaseHealthy && redisHealthy;

    if (!httpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
    {
        return overallHealthy
            ? Results.Ok(new { status = "healthy", service = "backend" })
            : Results.StatusCode(503);
    }

    return Results.Ok(new
    {
        status = overallHealthy ? "healthy" : "degraded",
        checks = new
        {
            database = databaseHealthy,
            redis = redisHealthy
        },
        service = "backend"
    });
}).AllowAnonymous();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();

    var seedEnabled = app.Configuration.GetValue("Seed:Enabled", app.Environment.IsDevelopment());
    if (seedEnabled)
    {
        var seedDataService = scope.ServiceProvider.GetRequiredService<ISeedDataService>();
        await seedDataService.SeedAsync();
    }
}

app.Run();
