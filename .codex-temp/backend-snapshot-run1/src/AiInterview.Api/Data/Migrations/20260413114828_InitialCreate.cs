using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;
using Pgvector;

#nullable disable

namespace AiInterview.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    tags = table.Column<string[]>(type: "varchar(50)[]", nullable: false, defaultValueSql: "'{}'"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_positions", x => x.id);
                    table.UniqueConstraint("ak_positions_code", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "learning_resources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    cover_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    target_dimensions = table.Column<string[]>(type: "varchar(30)[]", nullable: false, defaultValueSql: "'{}'"),
                    difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    duration = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reading_time = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    rating = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                    tags = table.Column<string[]>(type: "varchar(50)[]", nullable: false, defaultValueSql: "'{}'"),
                    metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_learning_resources", x => x.id);
                    table.ForeignKey(
                        name: "fk_learning_resources_positions_position_code",
                        column: x => x.position_code,
                        principalTable: "positions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_banks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    tags = table.Column<string[]>(type: "varchar(50)[]", nullable: false, defaultValueSql: "'{}'"),
                    ideal_answer = table.Column<string>(type: "text", nullable: true),
                    scoring_rubric = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    related_knowledge_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false, defaultValueSql: "'{}'"),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true),
                    use_count = table.Column<int>(type: "integer", nullable: false),
                    success_count = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_banks", x => x.id);
                    table.ForeignKey(
                        name: "fk_question_banks_positions_position_code",
                        column: x => x.position_code,
                        principalTable: "positions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "user"),
                    target_position_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    avatar_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_positions_target_position_code",
                        column: x => x.target_position_code,
                        principalTable: "positions",
                        principalColumn: "code");
                });

            migrationBuilder.CreateTable(
                name: "interviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    interview_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "standard"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "in_progress"),
                    total_rounds = table.Column<int>(type: "integer", nullable: false),
                    current_round = table.Column<int>(type: "integer", nullable: false),
                    question_types = table.Column<string[]>(type: "varchar(20)[]", nullable: false, defaultValueSql: "'{}'"),
                    config = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interviews", x => x.id);
                    table.ForeignKey(
                        name: "fk_interviews_positions_position_code",
                        column: x => x.position_code,
                        principalTable: "positions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_interviews_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    file_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    tags = table.Column<string[]>(type: "varchar(50)[]", nullable: false, defaultValueSql: "'{}'"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    chunk_count = table.Column<int>(type: "integer", nullable: false),
                    processing_error = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_knowledge_documents_positions_position_code",
                        column: x => x.position_code,
                        principalTable: "positions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_knowledge_documents_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "interview_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    interview_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    overall_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    executive_summary = table.Column<string>(type: "text", nullable: true),
                    strengths = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    weaknesses = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    detailed_analysis = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    learning_suggestions = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    training_plan = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    next_interview_focus = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interview_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_interview_reports_interviews_interview_id",
                        column: x => x.interview_id,
                        principalTable: "interviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_interview_reports_positions_position_code",
                        column: x => x.position_code,
                        principalTable: "positions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_interview_reports_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "interview_rounds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    interview_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_number = table.Column<int>(type: "integer", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    question_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    question_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    question_content = table.Column<string>(type: "text", nullable: false),
                    user_answer = table.Column<string>(type: "text", nullable: true),
                    user_input_mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "text"),
                    voice_transcription = table.Column<string>(type: "text", nullable: true),
                    ai_follow_ups = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    follow_up_count = table.Column<int>(type: "integer", nullable: false),
                    context = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interview_rounds", x => x.id);
                    table.ForeignKey(
                        name: "fk_interview_rounds_interviews_interview_id",
                        column: x => x.interview_id,
                        principalTable: "interviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_interview_rounds_question_banks_question_id",
                        column: x => x.question_id,
                        principalTable: "question_banks",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "interview_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    interview_id = table.Column<Guid>(type: "uuid", nullable: false),
                    overall_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    dimension_scores = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    dimension_details = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    rank_percentile = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    score_breakdown = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interview_scores", x => x.id);
                    table.ForeignKey(
                        name: "fk_interview_scores_interviews_interview_id",
                        column: x => x.interview_id,
                        principalTable: "interviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    content_hash = table.Column<string>(type: "text", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_chunks", x => x.id);
                    table.ForeignKey(
                        name: "fk_knowledge_chunks_knowledge_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "knowledge_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    interview_id = table.Column<Guid>(type: "uuid", nullable: true),
                    report_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    recommended_resources = table.Column<Guid[]>(type: "uuid[]", nullable: false, defaultValueSql: "'{}'"),
                    training_plan = table.Column<string>(type: "jsonb", nullable: true),
                    target_dimensions = table.Column<string[]>(type: "varchar(30)[]", nullable: false, defaultValueSql: "'{}'"),
                    match_scores = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    reason = table.Column<string>(type: "text", nullable: true),
                    is_viewed = table.Column<bool>(type: "boolean", nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recommendation_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_recommendation_records_interview_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "interview_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_recommendation_records_interviews_interview_id",
                        column: x => x.interview_id,
                        principalTable: "interviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_recommendation_records_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_reports_interview",
                table: "interview_reports",
                column: "interview_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_reports_position",
                table: "interview_reports",
                column: "position_code");

            migrationBuilder.CreateIndex(
                name: "idx_reports_score",
                table: "interview_reports",
                column: "overall_score");

            migrationBuilder.CreateIndex(
                name: "idx_reports_user",
                table: "interview_reports",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_reports_user_created",
                table: "interview_reports",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_rounds_interview",
                table: "interview_rounds",
                column: "interview_id");

            migrationBuilder.CreateIndex(
                name: "idx_rounds_interview_number",
                table: "interview_rounds",
                columns: new[] { "interview_id", "round_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_rounds_question",
                table: "interview_rounds",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "idx_scores_interview",
                table: "interview_scores",
                column: "interview_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_scores_overall",
                table: "interview_scores",
                column: "overall_score");

            migrationBuilder.CreateIndex(
                name: "idx_interviews_created",
                table: "interviews",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "idx_interviews_position",
                table: "interviews",
                column: "position_code");

            migrationBuilder.CreateIndex(
                name: "idx_interviews_user",
                table: "interviews",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_interviews_user_created",
                table: "interviews",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_interviews_user_status",
                table: "interviews",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_chunks_content_hash",
                table: "knowledge_chunks",
                column: "content_hash");

            migrationBuilder.CreateIndex(
                name: "idx_chunks_document",
                table: "knowledge_chunks",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "idx_chunks_embedding",
                table: "knowledge_chunks",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "ivfflat")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" })
                .Annotation("Npgsql:StorageParameter:lists", 100);

            migrationBuilder.CreateIndex(
                name: "idx_knowledge_docs_position",
                table: "knowledge_documents",
                column: "position_code");

            migrationBuilder.CreateIndex(
                name: "idx_knowledge_docs_status",
                table: "knowledge_documents",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_knowledge_docs_tags",
                table: "knowledge_documents",
                column: "tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_created_by",
                table: "knowledge_documents",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "idx_resources_active",
                table: "learning_resources",
                column: "is_active",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_resources_difficulty",
                table: "learning_resources",
                column: "difficulty");

            migrationBuilder.CreateIndex(
                name: "idx_resources_position",
                table: "learning_resources",
                column: "position_code");

            migrationBuilder.CreateIndex(
                name: "idx_resources_tags",
                table: "learning_resources",
                column: "tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "idx_resources_target_dims",
                table: "learning_resources",
                column: "target_dimensions")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "idx_resources_type",
                table: "learning_resources",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "idx_positions_active",
                table: "positions",
                column: "is_active",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_positions_code",
                table: "positions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_questions_active",
                table: "question_banks",
                column: "is_active",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_questions_difficulty",
                table: "question_banks",
                column: "difficulty");

            migrationBuilder.CreateIndex(
                name: "idx_questions_position",
                table: "question_banks",
                column: "position_code");

            migrationBuilder.CreateIndex(
                name: "idx_questions_position_type",
                table: "question_banks",
                columns: new[] { "position_code", "type" });

            migrationBuilder.CreateIndex(
                name: "idx_questions_search",
                table: "question_banks",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "idx_questions_tags",
                table: "question_banks",
                column: "tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "idx_questions_title_trgm",
                table: "question_banks",
                column: "title")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "idx_questions_type",
                table: "question_banks",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "idx_recommendations_interview",
                table: "recommendation_records",
                column: "interview_id");

            migrationBuilder.CreateIndex(
                name: "idx_recommendations_type",
                table: "recommendation_records",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "idx_recommendations_user",
                table: "recommendation_records",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_recommendations_user_created",
                table: "recommendation_records",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_records_report_id",
                table: "recommendation_records",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "idx_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_users_target_position",
                table: "users",
                column: "target_position_code");

            migrationBuilder.CreateIndex(
                name: "idx_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE INDEX idx_users_username_trgm ON users USING gin (username gin_trgm_ops);

                CREATE OR REPLACE FUNCTION question_banks_search_vector_update() RETURNS trigger AS $$
                BEGIN
                    NEW.search_vector := to_tsvector('simple', COALESCE(NEW.title, '') || ' ' || COALESCE(NEW.content, ''));
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER tr_question_banks_search_vector
                    BEFORE INSERT OR UPDATE ON question_banks
                    FOR EACH ROW EXECUTE FUNCTION question_banks_search_vector_update();

                UPDATE question_banks
                SET search_vector = to_tsvector('simple', COALESCE(title, '') || ' ' || COALESCE(content, ''));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS tr_question_banks_search_vector ON question_banks;
                DROP FUNCTION IF EXISTS question_banks_search_vector_update();
                DROP INDEX IF EXISTS idx_users_username_trgm;
                """);

            migrationBuilder.DropTable(
                name: "interview_rounds");

            migrationBuilder.DropTable(
                name: "interview_scores");

            migrationBuilder.DropTable(
                name: "knowledge_chunks");

            migrationBuilder.DropTable(
                name: "learning_resources");

            migrationBuilder.DropTable(
                name: "recommendation_records");

            migrationBuilder.DropTable(
                name: "question_banks");

            migrationBuilder.DropTable(
                name: "knowledge_documents");

            migrationBuilder.DropTable(
                name: "interview_reports");

            migrationBuilder.DropTable(
                name: "interviews");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "positions");
        }
    }
}
