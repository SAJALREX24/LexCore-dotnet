using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHearingOutcomeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionRequired",
                table: "Hearings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JudgeOrder",
                table: "Hearings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextHearingDate",
                table: "Hearings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "NextHearingTime",
                table: "Hearings",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "Hearings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAfterAt",
                table: "Hearings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UpdatedAfterHearing",
                table: "Hearings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionRequired",
                table: "Hearings");

            migrationBuilder.DropColumn(
                name: "JudgeOrder",
                table: "Hearings");

            migrationBuilder.DropColumn(
                name: "NextHearingDate",
                table: "Hearings");

            migrationBuilder.DropColumn(
                name: "NextHearingTime",
                table: "Hearings");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "Hearings");

            migrationBuilder.DropColumn(
                name: "UpdatedAfterAt",
                table: "Hearings");

            migrationBuilder.DropColumn(
                name: "UpdatedAfterHearing",
                table: "Hearings");
        }
    }
}
