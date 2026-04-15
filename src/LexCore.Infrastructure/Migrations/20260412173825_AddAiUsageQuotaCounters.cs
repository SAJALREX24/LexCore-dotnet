using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiUsageQuotaCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChatCount",
                table: "AiUsageQuotas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DraftCount",
                table: "AiUsageQuotas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MonthYear",
                table: "AiUsageQuotas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ResearchCount",
                table: "AiUsageQuotas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalTokensUsed",
                table: "AiUsageQuotas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AiUsageQuotas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatCount",
                table: "AiUsageQuotas");

            migrationBuilder.DropColumn(
                name: "DraftCount",
                table: "AiUsageQuotas");

            migrationBuilder.DropColumn(
                name: "MonthYear",
                table: "AiUsageQuotas");

            migrationBuilder.DropColumn(
                name: "ResearchCount",
                table: "AiUsageQuotas");

            migrationBuilder.DropColumn(
                name: "TotalTokensUsed",
                table: "AiUsageQuotas");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AiUsageQuotas");
        }
    }
}
