using AiInterview.Api.Constants;
using AiInterview.Api.Data;
using AiInterview.Api.Mappings;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Options;
using AiInterview.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AiInterview.Api.Services;

public class SeedDataService(
    ApplicationDbContext dbContext,
    PasswordService passwordService,
    IOptions<SeedOptions> seedOptions,
    IHostEnvironment hostEnvironment,
    ILogger<SeedDataService> logger) : ISeedDataService
{
    private readonly SeedOptions _seedOptions = seedOptions.Value;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var shouldSeedUsers = !await dbContext.Users.AnyAsync(cancellationToken);
        if (shouldSeedUsers)
        {
            var missingUserPassword = string.IsNullOrWhiteSpace(_seedOptions.UserPassword);
            var missingAdminPassword = string.IsNullOrWhiteSpace(_seedOptions.AdminPassword);

            if (missingUserPassword || missingAdminPassword)
            {
                if (!hostEnvironment.IsDevelopment())
                {
                    throw new InvalidOperationException("缺少种子用户密码配置，已拒绝执行用户初始化。");
                }

                logger.LogWarning("开发环境未配置完整的种子用户密码，已跳过演示用户初始化。");
                shouldSeedUsers = false;
            }
        }

        if (!await dbContext.Positions.AnyAsync(cancellationToken))
        {
            await dbContext.Positions.AddRangeAsync(
            [
                new Position
                {
                    Code = "java-backend",
                    Name = "Java后端开发工程师",
                    Description = "负责后端服务设计与实现，主要技术栈包括Java、Spring Boot、MySQL/PostgreSQL、Redis等",
                    Tags = ["Java", "Spring Boot", "MySQL", "Redis", "微服务"],
                    DisplayOrder = 1
                },
                new Position
                {
                    Code = "web-frontend",
                    Name = "Web前端开发工程师",
                    Description = "负责前端页面开发与交互实现，主要技术栈包括React、TypeScript、CSS、工程化等",
                    Tags = ["React", "TypeScript", "CSS", "ECharts", "Next.js"],
                    DisplayOrder = 2
                }
            ], cancellationToken);
        }

        if (!await dbContext.QuestionBanks.AnyAsync(cancellationToken))
        {
            await dbContext.QuestionBanks.AddRangeAsync(CreateSeedQuestions(), cancellationToken);
        }

        if (!await dbContext.LearningResources.AnyAsync(cancellationToken))
        {
            await dbContext.LearningResources.AddRangeAsync(CreateSeedResources(), cancellationToken);
        }

        if (shouldSeedUsers)
        {
            await dbContext.Users.AddRangeAsync(
            [
                new User
                {
                    Username = "zhangsan",
                    PasswordHash = passwordService.HashPassword(_seedOptions.UserPassword),
                    Email = "zhangsan@example.com",
                    Phone = "13800138000",
                    TargetPositionCode = "java-backend",
                    Role = AppRoles.User
                },
                new User
                {
                    Username = "admin",
                    PasswordHash = passwordService.HashPassword(_seedOptions.AdminPassword),
                    Email = "admin@example.com",
                    Role = AppRoles.Admin,
                    TargetPositionCode = "web-frontend"
                }
            ], cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<QuestionBank> CreateSeedQuestions()
    {
        return
        [
            new QuestionBank
            {
                PositionCode = "java-backend",
                Type = QuestionTypes.Technical,
                Difficulty = QuestionDifficulties.Medium,
                Title = "请描述 Spring Boot 的自动配置原理",
                Content = "请详细描述 Spring Boot 是如何实现自动配置的，以及自动配置对开发效率的帮助。",
                Tags = ["Spring Boot", "Java", "自动配置"],
                IdealAnswer = "Spring Boot 通过 EnableAutoConfiguration、条件注解和 spring.factories / AutoConfiguration.imports 完成自动配置。",
                ScoringRubric = ApplicationMapper.SerializeObject(new Dictionary<string, decimal>
                {
                    ["technicalAccuracy"] = 30,
                    ["depth"] = 25,
                    ["clarity"] = 20,
                    ["practicality"] = 25
                })
            },
            new QuestionBank
            {
                PositionCode = "java-backend",
                Type = QuestionTypes.Project,
                Difficulty = QuestionDifficulties.Medium,
                Title = "介绍一个你负责过的高并发项目",
                Content = "请结合你的真实项目，说明系统规模、瓶颈点和你做过的优化。",
                Tags = ["项目经验", "高并发", "优化"],
                IdealAnswer = "从业务背景、系统瓶颈、定位方式、优化手段和最终结果逐层展开。"
            },
            new QuestionBank
            {
                PositionCode = "java-backend",
                Type = QuestionTypes.Scenario,
                Difficulty = QuestionDifficulties.Hard,
                Title = "如何设计一个分布式缓存失效方案",
                Content = "请从一致性、可用性和成本角度说明你的设计取舍。",
                Tags = ["分布式", "缓存", "一致性"],
                IdealAnswer = "可从双删、延迟双删、订阅消息、binlog 同步和幂等控制等方向作答。"
            },
            new QuestionBank
            {
                PositionCode = "web-frontend",
                Type = QuestionTypes.Technical,
                Difficulty = QuestionDifficulties.Medium,
                Title = "解释 React 中状态更新与渲染的关系",
                Content = "请说明状态更新、批处理、渲染时机以及性能优化的常见思路。",
                Tags = ["React", "状态管理", "性能优化"],
                IdealAnswer = "重点说明 React 的调度过程、批量更新和避免不必要渲染的策略。"
            },
            new QuestionBank
            {
                PositionCode = "web-frontend",
                Type = QuestionTypes.Project,
                Difficulty = QuestionDifficulties.Medium,
                Title = "介绍一次复杂前端页面的重构经历",
                Content = "请说明重构目标、拆分思路、落地步骤以及最终收益。",
                Tags = ["重构", "工程化", "组件化"],
                IdealAnswer = "可以围绕组件抽象、状态收敛、样式治理和性能结果展开。"
            },
            new QuestionBank
            {
                PositionCode = "web-frontend",
                Type = QuestionTypes.Behavioral,
                Difficulty = QuestionDifficulties.Easy,
                Title = "当产品需求频繁变化时你如何协作",
                Content = "请说明你如何和产品、设计、测试协作，确保交付质量。",
                Tags = ["协作", "沟通", "交付"],
                IdealAnswer = "重点体现优先级管理、风险同步和阶段性对齐能力。"
            }
        ];
    }

    private static IEnumerable<LearningResource> CreateSeedResources()
    {
        return
        [
            new LearningResource
            {
                PositionCode = "java-backend",
                Title = "分布式系统设计与实战",
                Type = "course",
                Provider = "慕课网",
                Url = "https://example.com/course/distributed-system",
                TargetDimensions = ["technicalAccuracy", "knowledgeDepth"],
                Difficulty = "intermediate",
                Duration = "20小时",
                Rating = 4.8m,
                Tags = ["分布式", "系统设计", "后端"]
            },
            new LearningResource
            {
                PositionCode = "java-backend",
                Title = "数据库索引优化详解",
                Type = "article",
                Provider = "掘金",
                Url = "https://example.com/article/db-index",
                TargetDimensions = ["technicalAccuracy"],
                Difficulty = "intermediate",
                ReadingTime = "15分钟",
                Rating = 4.6m,
                Tags = ["数据库", "索引", "优化"]
            },
            new LearningResource
            {
                PositionCode = "web-frontend",
                Title = "React 性能优化全链路实战",
                Type = "course",
                Provider = "极客时间",
                Url = "https://example.com/course/react-performance",
                TargetDimensions = ["clarity", "technicalAccuracy"],
                Difficulty = "intermediate",
                Duration = "16小时",
                Rating = 4.7m,
                Tags = ["React", "性能优化", "前端工程化"]
            },
            new LearningResource
            {
                PositionCode = "web-frontend",
                Title = "复杂交互界面的组件拆分方法",
                Type = "article",
                Provider = "掘金",
                Url = "https://example.com/article/component-design",
                TargetDimensions = ["logicalThinking", "clarity"],
                Difficulty = "beginner",
                ReadingTime = "12分钟",
                Rating = 4.5m,
                Tags = ["组件化", "设计模式", "前端"]
            }
        ];
    }
}
