using System;
using AiInterview.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiInterview.Api.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260414112100_AddAiProviderSettings")]
    /// <inheritdoc />
    public partial class AddAiProviderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_provider_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    api_key_protected = table.Column<string>(type: "text", nullable: true),
                    api_key_masked = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    temperature = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false, defaultValue: 0.7m),
                    max_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 2048),
                    system_prompt = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_provider_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_provider_settings");
        }
    }
}
