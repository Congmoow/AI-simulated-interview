using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiInterview.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "interview_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    interview_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    related_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interview_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_interview_messages_interviews_interview_id",
                        column: x => x.interview_id,
                        principalTable: "interviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_interview_messages_question_banks_related_question_id",
                        column: x => x.related_question_id,
                        principalTable: "question_banks",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "idx_messages_interview",
                table: "interview_messages",
                column: "interview_id");

            migrationBuilder.CreateIndex(
                name: "idx_messages_interview_sequence",
                table: "interview_messages",
                columns: new[] { "interview_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_messages_related_question",
                table: "interview_messages",
                column: "related_question_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "interview_messages");
        }
    }
}
