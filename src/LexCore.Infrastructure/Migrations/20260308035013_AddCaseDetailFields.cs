using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseDetailFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdvancePaid",
                table: "Cases",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AgreedFees",
                table: "Cases",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaseStage",
                table: "Cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientName",
                table: "Cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientPhone",
                table: "Cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientPosition",
                table: "Cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientWhatsApp",
                table: "Cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FIRNumber",
                table: "Cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeeType",
                table: "Cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LimitationAlertSent1",
                table: "Cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LimitationAlertSent30",
                table: "Cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LimitationAlertSent7",
                table: "Cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LimitationDate",
                table: "Cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyClientOnHearing",
                table: "Cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyClientOnStatus",
                table: "Cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OppositeParty",
                table: "Cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OppositePartyLawyer",
                table: "Cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PerHearingFee",
                table: "Cases",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReliefSought",
                table: "Cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SectionAct",
                table: "Cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VakalatnamaSigned",
                table: "Cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdvancePaid",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "AgreedFees",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CaseStage",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ClientName",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ClientPhone",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ClientPosition",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ClientWhatsApp",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "FIRNumber",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "FeeType",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "LimitationAlertSent1",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "LimitationAlertSent30",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "LimitationAlertSent7",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "LimitationDate",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "NotifyClientOnHearing",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "NotifyClientOnStatus",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "OppositeParty",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "OppositePartyLawyer",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "PerHearingFee",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ReliefSought",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SectionAct",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "VakalatnamaSigned",
                table: "Cases");
        }
    }
}
