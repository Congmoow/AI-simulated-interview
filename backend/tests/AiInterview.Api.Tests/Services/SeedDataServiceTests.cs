using AiInterview.Api.Data;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Options;
using AiInterview.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiInterview.Api.Tests.Services;

file sealed class SeedTestApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : ApplicationDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<KnowledgeChunk>().Ignore(x => x.Embedding);
        modelBuilder.Entity<QuestionBank>().Ignore(x => x.SearchVector);
    }
}

file sealed class StubHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "AiInterview.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
}

file sealed class RecordingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

public class SeedDataServiceTests
{
    [Fact]
    public async Task SeedAsync_WhenDevelopmentPasswordsMissing_ShouldSkipSeedUsersAndLogWarning()
    {
        await using var dbContext = CreateDbContext();
        var logger = new RecordingLogger<SeedDataService>();
        var service = new SeedDataService(
            dbContext,
            new PasswordService(),
            Microsoft.Extensions.Options.Options.Create(new SeedOptions()),
            new StubHostEnvironment { EnvironmentName = Environments.Development },
            logger);

        await service.SeedAsync();

        dbContext.Users.Should().BeEmpty();
        dbContext.Positions.Should().NotBeEmpty();
        dbContext.QuestionBanks.Should().NotBeEmpty();
        dbContext.LearningResources.Should().NotBeEmpty();
        logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("种子用户", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SeedAsync_WhenNonDevelopmentPasswordsMissing_ShouldThrowAndNotSeedAnyData()
    {
        await using var dbContext = CreateDbContext();
        var service = new SeedDataService(
            dbContext,
            new PasswordService(),
            Microsoft.Extensions.Options.Options.Create(new SeedOptions()),
            new StubHostEnvironment { EnvironmentName = Environments.Production },
            new RecordingLogger<SeedDataService>());

        var act = () => service.SeedAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
        dbContext.Users.Should().BeEmpty();
        dbContext.Positions.Should().BeEmpty();
        dbContext.QuestionBanks.Should().BeEmpty();
        dbContext.LearningResources.Should().BeEmpty();
    }

    [Fact]
    public async Task SeedAsync_WhenPasswordsConfigured_ShouldCreateSeedUsers()
    {
        await using var dbContext = CreateDbContext();
        var service = new SeedDataService(
            dbContext,
            new PasswordService(),
            Microsoft.Extensions.Options.Options.Create(new SeedOptions
            {
                UserPassword = "UserPassword123!",
                AdminPassword = "AdminPassword123!"
            }),
            new StubHostEnvironment { EnvironmentName = Environments.Production },
            new RecordingLogger<SeedDataService>());

        await service.SeedAsync();

        dbContext.Users.Select(x => x.Username).Should().BeEquivalentTo(["zhangsan", "admin"]);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SeedTestApplicationDbContext(options);
    }
}
