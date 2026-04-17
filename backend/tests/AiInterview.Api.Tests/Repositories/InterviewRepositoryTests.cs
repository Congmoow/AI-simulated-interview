using AiInterview.Api.Constants;
using AiInterview.Api.Data;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AiInterview.Api.Tests.Repositories;

file sealed class TestApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : ApplicationDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<KnowledgeChunk>().Ignore(x => x.Embedding);
        modelBuilder.Entity<QuestionBank>().Ignore(x => x.SearchVector);
    }
}

public class InterviewRepositoryTests
{
    [Fact]
    public async Task GetUserHistoryAsync_ShouldSupportCommaSeparatedStatuses()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var completedInterview = CreateInterview(userId, InterviewStatuses.Completed, DateTimeOffset.UtcNow.AddHours(-4));
        var generatingReportInterview = CreateInterview(userId, InterviewStatuses.GeneratingReport, DateTimeOffset.UtcNow.AddHours(-3));
        var reportFailedInterview = CreateInterview(userId, InterviewStatuses.ReportFailed, DateTimeOffset.UtcNow.AddHours(-2));
        var inProgressInterview = CreateInterview(userId, InterviewStatuses.InProgress, DateTimeOffset.UtcNow.AddHours(-1));
        var otherUserInterview = CreateInterview(otherUserId, InterviewStatuses.Completed, DateTimeOffset.UtcNow);

        await SeedRequiredEntitiesAsync(dbContext, userId, otherUserId);
        await dbContext.Interviews.AddRangeAsync(
            completedInterview,
            generatingReportInterview,
            reportFailedInterview,
            inProgressInterview,
            otherUserInterview);
        await dbContext.SaveChangesAsync();

        var repository = new InterviewRepository(dbContext);

        var result = await repository.GetUserHistoryAsync(
            userId,
            positionCode: null,
            status: $"{InterviewStatuses.Completed},{InterviewStatuses.GeneratingReport},{InterviewStatuses.ReportFailed}",
            startDate: null,
            endDate: null,
            page: 1,
            pageSize: 10);

        result.Select(item => item.Id).Should().BeEquivalentTo(
            [completedInterview.Id, generatingReportInterview.Id, reportFailedInterview.Id]);
        result.Should().NotContain(item => item.Id == inProgressInterview.Id);
        result.Should().NotContain(item => item.Id == otherUserInterview.Id);
    }

    [Fact]
    public async Task CountUserHistoryAsync_ShouldRespectCommaSeparatedStatuses()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();

        await SeedRequiredEntitiesAsync(dbContext, userId);
        await dbContext.Interviews.AddRangeAsync(
            CreateInterview(userId, InterviewStatuses.Completed, DateTimeOffset.UtcNow.AddHours(-3)),
            CreateInterview(userId, InterviewStatuses.GeneratingReport, DateTimeOffset.UtcNow.AddHours(-2)),
            CreateInterview(userId, InterviewStatuses.InProgress, DateTimeOffset.UtcNow.AddHours(-1)));
        await dbContext.SaveChangesAsync();

        var repository = new InterviewRepository(dbContext);

        var count = await repository.CountUserHistoryAsync(
            userId,
            positionCode: null,
            status: $"{InterviewStatuses.Completed},{InterviewStatuses.GeneratingReport}",
            startDate: null,
            endDate: null);

        count.Should().Be(2);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestApplicationDbContext(options);
    }

    private static async Task SeedRequiredEntitiesAsync(ApplicationDbContext dbContext, params Guid[] userIds)
    {
        await dbContext.Positions.AddAsync(new Position
        {
            Code = "java-backend",
            Name = "Java 后端工程师",
            Description = "测试岗位"
        });

        foreach (var userId in userIds)
        {
            await dbContext.Users.AddAsync(new User
            {
                Id = userId,
                Username = $"user-{userId:N}",
                PasswordHash = "hash",
                Email = $"{userId:N}@example.com",
                Role = "user"
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static Interview CreateInterview(Guid userId, string status, DateTimeOffset createdAt)
    {
        return new Interview
        {
            UserId = userId,
            PositionCode = "java-backend",
            InterviewMode = InterviewModes.Standard,
            Status = status,
            TotalRounds = 5,
            CurrentRound = status == InterviewStatuses.InProgress ? 2 : 5,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            EndedAt = status == InterviewStatuses.InProgress ? null : createdAt.AddMinutes(25),
            DurationSeconds = status == InterviewStatuses.InProgress ? null : 1500
        };
    }
}
