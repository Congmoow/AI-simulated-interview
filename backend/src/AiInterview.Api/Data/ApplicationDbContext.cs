using AiInterview.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace AiInterview.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Position> Positions => Set<Position>();

    public DbSet<QuestionBank> QuestionBanks => Set<QuestionBank>();

    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();

    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();

    public DbSet<Interview> Interviews => Set<Interview>();

    public DbSet<InterviewRound> InterviewRounds => Set<InterviewRound>();

    public DbSet<InterviewMessage> InterviewMessages => Set<InterviewMessage>();

    public DbSet<InterviewScore> InterviewScores => Set<InterviewScore>();

    public DbSet<InterviewReport> InterviewReports => Set<InterviewReport>();

    public DbSet<LearningResource> LearningResources => Set<LearningResource>();

    public DbSet<RecommendationRecord> RecommendationRecords => Set<RecommendationRecord>();

    public DbSet<AiProviderSetting> AiProviderSettings => Set<AiProviderSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.HasPostgresExtension("vector");

        ConfigureUser(modelBuilder);
        ConfigurePosition(modelBuilder);
        ConfigureQuestionBank(modelBuilder);
        ConfigureKnowledgeDocument(modelBuilder);
        ConfigureKnowledgeChunk(modelBuilder);
        ConfigureInterview(modelBuilder);
        ConfigureInterviewRound(modelBuilder);
        ConfigureInterviewMessage(modelBuilder);
        ConfigureInterviewScore(modelBuilder);
        ConfigureInterviewReport(modelBuilder);
        ConfigureLearningResource(modelBuilder);
        ConfigureRecommendationRecord(modelBuilder);
        ConfigureAiProviderSetting(modelBuilder);
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<User>();
        entity.ToTable("users");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Username).HasMaxLength(50).IsRequired();
        entity.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
        entity.Property(x => x.Email).HasMaxLength(255).IsRequired();
        entity.Property(x => x.Phone).HasMaxLength(20);
        entity.Property(x => x.Role).HasMaxLength(20).HasDefaultValue("user").IsRequired();
        entity.Property(x => x.TargetPositionCode).HasMaxLength(50);
        entity.Property(x => x.AvatarUrl).HasMaxLength(500);
        entity.HasIndex(x => x.Username).HasDatabaseName("idx_users_username").IsUnique();
        entity.HasIndex(x => x.Email).HasDatabaseName("idx_users_email").IsUnique();
        entity.HasIndex(x => x.TargetPositionCode).HasDatabaseName("idx_users_target_position");
        entity.HasOne(x => x.TargetPosition)
            .WithMany()
            .HasForeignKey(x => x.TargetPositionCode)
            .HasPrincipalKey(x => x.Code);
    }

    private static void ConfigurePosition(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Position>();
        entity.ToTable("positions");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Tags).HasColumnType("varchar(50)[]").HasDefaultValueSql("'{}'");
        entity.HasIndex(x => x.Code).HasDatabaseName("idx_positions_code").IsUnique();
        entity.HasIndex(x => x.IsActive)
            .HasDatabaseName("idx_positions_active")
            .HasFilter("is_active = true");
    }

    private static void ConfigureQuestionBank(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<QuestionBank>();
        entity.ToTable("question_banks");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.PositionCode).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Type).HasMaxLength(30).IsRequired();
        entity.Property(x => x.Difficulty).HasMaxLength(20).IsRequired();
        entity.Property(x => x.Title).HasMaxLength(500).IsRequired();
        entity.Property(x => x.Tags).HasColumnType("varchar(50)[]").HasDefaultValueSql("'{}'");
        entity.Property(x => x.ScoringRubric).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        entity.Property(x => x.RelatedKnowledgeIds).HasColumnType("uuid[]").HasDefaultValueSql("'{}'");
        entity.Property(x => x.SearchVector).HasColumnType("tsvector");
        entity.HasIndex(x => x.PositionCode).HasDatabaseName("idx_questions_position");
        entity.HasIndex(x => x.Type).HasDatabaseName("idx_questions_type");
        entity.HasIndex(x => x.Difficulty).HasDatabaseName("idx_questions_difficulty");
        entity.HasIndex(x => new { x.PositionCode, x.Type }).HasDatabaseName("idx_questions_position_type");
        entity.HasIndex(x => x.IsActive)
            .HasDatabaseName("idx_questions_active")
            .HasFilter("is_active = true");
        entity.HasIndex(x => x.Tags)
            .HasDatabaseName("idx_questions_tags")
            .HasMethod("gin");
        entity.HasIndex(x => x.SearchVector)
            .HasDatabaseName("idx_questions_search")
            .HasMethod("gin");
        entity.HasIndex(x => x.Title)
            .HasDatabaseName("idx_questions_title_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
        entity.HasOne(x => x.Position)
            .WithMany(x => x.QuestionBanks)
            .HasForeignKey(x => x.PositionCode)
            .HasPrincipalKey(x => x.Code);
    }

    private static void ConfigureKnowledgeDocument(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<KnowledgeDocument>();
        entity.ToTable("knowledge_documents");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.PositionCode).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Title).HasMaxLength(500).IsRequired();
        entity.Property(x => x.FileUrl).HasMaxLength(1000).IsRequired();
        entity.Property(x => x.FileType).HasMaxLength(20).IsRequired();
        entity.Property(x => x.Tags).HasColumnType("varchar(50)[]").HasDefaultValueSql("'{}'");
        entity.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("pending").IsRequired();
        entity.Property(x => x.Metadata).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        entity.HasIndex(x => x.PositionCode).HasDatabaseName("idx_knowledge_docs_position");
        entity.HasIndex(x => x.Status).HasDatabaseName("idx_knowledge_docs_status");
        entity.HasIndex(x => x.Tags)
            .HasDatabaseName("idx_knowledge_docs_tags")
            .HasMethod("gin");
        entity.HasOne(x => x.Position)
            .WithMany(x => x.KnowledgeDocuments)
            .HasForeignKey(x => x.PositionCode)
            .HasPrincipalKey(x => x.Code);
        entity.HasOne(x => x.Creator)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy);
    }

    private static void ConfigureKnowledgeChunk(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<KnowledgeChunk>();
        entity.ToTable("knowledge_chunks");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Metadata).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        entity.Property(x => x.Embedding).HasColumnType("vector(768)");
        entity.HasIndex(x => x.DocumentId).HasDatabaseName("idx_chunks_document");
        entity.HasIndex(x => x.ContentHash).HasDatabaseName("idx_chunks_content_hash");
        entity.HasIndex(x => x.Embedding)
            .HasDatabaseName("idx_chunks_embedding")
            .HasMethod("ivfflat")
            .HasOperators("vector_cosine_ops")
            .HasStorageParameter("lists", 100);
        entity.HasOne(x => x.Document)
            .WithMany(x => x.Chunks)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureInterview(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Interview>();
        entity.ToTable("interviews");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.PositionCode).HasMaxLength(50).IsRequired();
        entity.Property(x => x.InterviewMode).HasMaxLength(20).HasDefaultValue("standard").IsRequired();
        entity.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("in_progress").IsRequired();
        entity.Property(x => x.QuestionTypes).HasColumnType("varchar(20)[]").HasDefaultValueSql("'{}'");
        entity.Property(x => x.Config).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        entity.HasIndex(x => x.UserId).HasDatabaseName("idx_interviews_user");
        entity.HasIndex(x => new { x.UserId, x.Status }).HasDatabaseName("idx_interviews_user_status");
        entity.HasIndex(x => x.PositionCode).HasDatabaseName("idx_interviews_position");
        entity.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("idx_interviews_created")
            .IsDescending();
        entity.HasIndex(x => new { x.UserId, x.CreatedAt })
            .HasDatabaseName("idx_interviews_user_created")
            .IsDescending(false, true);
        entity.HasOne(x => x.User)
            .WithMany(x => x.Interviews)
            .HasForeignKey(x => x.UserId);
        entity.HasOne(x => x.Position)
            .WithMany(x => x.Interviews)
            .HasForeignKey(x => x.PositionCode)
            .HasPrincipalKey(x => x.Code);
    }

    private static void ConfigureInterviewRound(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<InterviewRound>();
        entity.ToTable("interview_rounds");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.QuestionTitle).HasMaxLength(500).IsRequired();
        entity.Property(x => x.QuestionType).HasMaxLength(30).IsRequired();
        entity.Property(x => x.UserInputMode).HasMaxLength(10).HasDefaultValue("text");
        entity.Property(x => x.AiFollowUps).HasColumnType("text[]").HasDefaultValueSql("'{}'");
        entity.Property(x => x.Context).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        entity.HasIndex(x => x.InterviewId).HasDatabaseName("idx_rounds_interview");
        entity.HasIndex(x => new { x.InterviewId, x.RoundNumber })
            .HasDatabaseName("idx_rounds_interview_number")
            .IsUnique();
        entity.HasIndex(x => x.QuestionId).HasDatabaseName("idx_rounds_question");
        entity.HasOne(x => x.Interview)
            .WithMany(x => x.Rounds)
            .HasForeignKey(x => x.InterviewId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Question)
            .WithMany()
            .HasForeignKey(x => x.QuestionId);
    }

    private static void ConfigureInterviewMessage(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<InterviewMessage>();
        entity.ToTable("interview_messages");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Role).HasMaxLength(20).IsRequired();
        entity.Property(x => x.MessageType).HasMaxLength(20).IsRequired();
        entity.Property(x => x.Content).HasColumnType("text").IsRequired();
        entity.Property(x => x.Metadata).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        entity.HasIndex(x => x.InterviewId).HasDatabaseName("idx_messages_interview");
        entity.HasIndex(x => new { x.InterviewId, x.Sequence })
            .HasDatabaseName("idx_messages_interview_sequence")
            .IsUnique();
        entity.HasIndex(x => x.RelatedQuestionId).HasDatabaseName("idx_messages_related_question");
        entity.HasOne(x => x.Interview)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.InterviewId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.RelatedQuestion)
            .WithMany()
            .HasForeignKey(x => x.RelatedQuestionId);
    }

    private static void ConfigureInterviewScore(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<InterviewScore>();
        entity.ToTable("interview_scores");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.OverallScore).HasPrecision(5, 2);
        entity.Property(x => x.DimensionScores).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        entity.Property(x => x.DimensionDetails).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        entity.Property(x => x.ScoreBreakdown).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        entity.Property(x => x.RankPercentile).HasPrecision(5, 2);
        entity.Property(x => x.ModelVersion).HasMaxLength(50);
        entity.HasIndex(x => x.InterviewId).HasDatabaseName("idx_scores_interview").IsUnique();
        entity.HasIndex(x => x.OverallScore).HasDatabaseName("idx_scores_overall");
        entity.HasOne(x => x.Interview)
            .WithOne(x => x.Score)
            .HasForeignKey<InterviewScore>(x => x.InterviewId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureInterviewReport(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<InterviewReport>();
        entity.ToTable("interview_reports");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.PositionCode).HasMaxLength(50).IsRequired();
        entity.Property(x => x.OverallScore).HasPrecision(5, 2);
        entity.Property(x => x.Strengths).HasColumnType("text[]").HasDefaultValueSql("'{}'");
        entity.Property(x => x.Weaknesses).HasColumnType("text[]").HasDefaultValueSql("'{}'");
        entity.Property(x => x.DetailedAnalysis).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        entity.Property(x => x.LearningSuggestions).HasColumnType("text[]").HasDefaultValueSql("'{}'");
        entity.Property(x => x.TrainingPlan).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        entity.Property(x => x.NextInterviewFocus).HasColumnType("text[]").HasDefaultValueSql("'{}'");
        entity.Property(x => x.ModelVersion).HasMaxLength(50);
        entity.HasIndex(x => x.InterviewId).HasDatabaseName("idx_reports_interview").IsUnique();
        entity.HasIndex(x => x.UserId).HasDatabaseName("idx_reports_user");
        entity.HasIndex(x => new { x.UserId, x.CreatedAt })
            .HasDatabaseName("idx_reports_user_created")
            .IsDescending(false, true);
        entity.HasIndex(x => x.PositionCode).HasDatabaseName("idx_reports_position");
        entity.HasIndex(x => x.OverallScore).HasDatabaseName("idx_reports_score");
        entity.HasOne(x => x.Interview)
            .WithOne(x => x.Report)
            .HasForeignKey<InterviewReport>(x => x.InterviewId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.User)
            .WithMany(x => x.InterviewReports)
            .HasForeignKey(x => x.UserId);
        entity.HasOne(x => x.Position)
            .WithMany()
            .HasForeignKey(x => x.PositionCode)
            .HasPrincipalKey(x => x.Code);
    }

    private static void ConfigureLearningResource(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<LearningResource>();
        entity.ToTable("learning_resources");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.PositionCode).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Title).HasMaxLength(500).IsRequired();
        entity.Property(x => x.Type).HasMaxLength(30).IsRequired();
        entity.Property(x => x.Provider).HasMaxLength(200);
        entity.Property(x => x.Url).HasMaxLength(1000);
        entity.Property(x => x.CoverUrl).HasMaxLength(1000);
        entity.Property(x => x.TargetDimensions).HasColumnType("varchar(30)[]").HasDefaultValueSql("'{}'");
        entity.Property(x => x.Difficulty).HasMaxLength(20);
        entity.Property(x => x.Duration).HasMaxLength(50);
        entity.Property(x => x.ReadingTime).HasMaxLength(20);
        entity.Property(x => x.Rating).HasPrecision(3, 2);
        entity.Property(x => x.Tags).HasColumnType("varchar(50)[]").HasDefaultValueSql("'{}'");
        entity.Property(x => x.Metadata).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        entity.HasIndex(x => x.PositionCode).HasDatabaseName("idx_resources_position");
        entity.HasIndex(x => x.Type).HasDatabaseName("idx_resources_type");
        entity.HasIndex(x => x.TargetDimensions)
            .HasDatabaseName("idx_resources_target_dims")
            .HasMethod("gin");
        entity.HasIndex(x => x.Difficulty).HasDatabaseName("idx_resources_difficulty");
        entity.HasIndex(x => x.Tags)
            .HasDatabaseName("idx_resources_tags")
            .HasMethod("gin");
        entity.HasIndex(x => x.IsActive)
            .HasDatabaseName("idx_resources_active")
            .HasFilter("is_active = true");
        entity.HasOne(x => x.Position)
            .WithMany(x => x.LearningResources)
            .HasForeignKey(x => x.PositionCode)
            .HasPrincipalKey(x => x.Code);
    }

    private static void ConfigureAiProviderSetting(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AiProviderSetting>();
        entity.ToTable("ai_provider_settings");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Provider).HasMaxLength(50).IsRequired();
        entity.Property(x => x.BaseUrl).HasMaxLength(500).IsRequired();
        entity.Property(x => x.Model).HasMaxLength(100).IsRequired();
        entity.Property(x => x.ApiKeyProtected).HasColumnType("text");
        entity.Property(x => x.ApiKeyMasked).HasMaxLength(64);
        entity.Property(x => x.Temperature).HasPrecision(4, 2).HasDefaultValue(0.7m);
        entity.Property(x => x.MaxTokens).HasDefaultValue(2048);
        entity.Property(x => x.SystemPrompt).HasColumnType("text").HasDefaultValue(string.Empty);
        entity.Property(x => x.UpdatedBy).HasMaxLength(100).IsRequired();
    }

    private static void ConfigureRecommendationRecord(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RecommendationRecord>();
        entity.ToTable("recommendation_records");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Type).HasMaxLength(30).IsRequired();
        entity.Property(x => x.RecommendedResources).HasColumnType("uuid[]").HasDefaultValueSql("'{}'");
        entity.Property(x => x.TrainingPlan).HasColumnType("jsonb");
        entity.Property(x => x.TargetDimensions).HasColumnType("varchar(30)[]").HasDefaultValueSql("'{}'");
        entity.Property(x => x.MatchScores).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        entity.HasIndex(x => x.UserId).HasDatabaseName("idx_recommendations_user");
        entity.HasIndex(x => new { x.UserId, x.CreatedAt })
            .HasDatabaseName("idx_recommendations_user_created")
            .IsDescending(false, true);
        entity.HasIndex(x => x.Type).HasDatabaseName("idx_recommendations_type");
        entity.HasIndex(x => x.InterviewId).HasDatabaseName("idx_recommendations_interview");
        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId);
        entity.HasOne(x => x.Interview)
            .WithMany()
            .HasForeignKey(x => x.InterviewId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(x => x.Report)
            .WithMany()
            .HasForeignKey(x => x.ReportId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
