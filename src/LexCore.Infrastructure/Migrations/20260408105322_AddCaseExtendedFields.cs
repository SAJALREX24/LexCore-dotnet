using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseExtendedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_CaseId",
                table: "Documents");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Payments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "InvoiceId",
                table: "Payments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "CaseId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdvancePayment",
                table: "Payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMode",
                table: "Payments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentType",
                table: "Payments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "Payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Tags",
                table: "Documents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MimeType",
                table: "Documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Documents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AIDraftId",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AIDraftStatus",
                table: "Documents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentCategory",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentSource",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentTag",
                table: "Documents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HearingId",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAIDraft",
                table: "Documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "PerHearingFee",
                table: "Cases",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OppositePartyLawyer",
                table: "Cases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OppositeParty",
                table: "Cases",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FeeType",
                table: "Cases",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FIRNumber",
                table: "Cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CourtName",
                table: "Cases",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClientWhatsApp",
                table: "Cases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClientPosition",
                table: "Cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClientPhone",
                table: "Cases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClientName",
                table: "Cases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CaseType",
                table: "Cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CaseStage",
                table: "Cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AgreedFees",
                table: "Cases",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AdvancePaid",
                table: "Cases",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActsAndSectionsJson",
                table: "Cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorisedRepresentative",
                table: "Cases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorisedRepresentativeDesignation",
                table: "Cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaseNature",
                table: "Cases",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaseNotesHtml",
                table: "Cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CaseStageChangeAlert",
                table: "Cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CaseTypeCode",
                table: "Cases",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientAddress",
                table: "Cases",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClientAge",
                table: "Cases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientFatherName",
                table: "Cases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientIDDocumentType",
                table: "Cases",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientInstructionsHtml",
                table: "Cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientType",
                table: "Cases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ClientWhatsAppEnabled",
                table: "Cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CompanyCIN",
                table: "Cases",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyGST",
                table: "Cases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Cases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CourtHierarchyName",
                table: "Cases",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CourtType",
                table: "Cases",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "Cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FIRDate",
                table: "Cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HearingReminderEvening",
                table: "Cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HearingReminderMorning",
                table: "Cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "InvoiceOverdueAlert",
                table: "Cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LimitationAlertEnabled",
                table: "Cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NatureOfOffence",
                table: "Cases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OppositeCounselCity",
                table: "Cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OppositeCounselEnrollment",
                table: "Cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OppositeCounselName",
                table: "Cases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OppositeCounselPhone",
                table: "Cases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OppositePartiesJson",
                table: "Cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PSDistrict",
                table: "Cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PSState",
                table: "Cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMode",
                table: "Cases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "Cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivateNotesHtml",
                table: "Cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateUT",
                table: "Cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TotalFeeLockedAt",
                table: "Cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CaseId",
                table: "Payments",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CaseId_Category",
                table: "Documents",
                columns: new[] { "CaseId", "DocumentCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_HearingId",
                table: "Documents",
                column: "HearingId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_CourtType",
                table: "Cases",
                column: "CourtType");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Hearings_HearingId",
                table: "Documents",
                column: "HearingId",
                principalTable: "Hearings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Cases_CaseId",
                table: "Payments",
                column: "CaseId",
                principalTable: "Cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Hearings_HearingId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Cases_CaseId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CaseId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Documents_CaseId_Category",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_HearingId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Cases_CourtType",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CaseId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "IsAdvancePayment",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentMode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "AIDraftId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "AIDraftStatus",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DocumentCategory",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DocumentSource",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DocumentTag",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "HearingId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsAIDraft",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ActsAndSectionsJson",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "AuthorisedRepresentative",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "AuthorisedRepresentativeDesignation",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CaseNature",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CaseNotesHtml",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CaseStageChangeAlert",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CaseTypeCode",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ClientAddress",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ClientAge",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ClientFatherName",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ClientIDDocumentType",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ClientInstructionsHtml",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ClientType",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ClientWhatsAppEnabled",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CompanyCIN",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CompanyGST",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CourtHierarchyName",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CourtType",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "District",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "FIRDate",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "HearingReminderEvening",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "HearingReminderMorning",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "InvoiceOverdueAlert",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "LimitationAlertEnabled",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "NatureOfOffence",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "OppositeCounselCity",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "OppositeCounselEnrollment",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "OppositeCounselName",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "OppositeCounselPhone",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "OppositePartiesJson",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "PSDistrict",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "PSState",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "PaymentMode",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "PrivateNotesHtml",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "StateUT",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "TotalFeeLockedAt",
                table: "Cases");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Payments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<Guid>(
                name: "InvoiceId",
                table: "Payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Tags",
                table: "Documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MimeType",
                table: "Documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PerHearingFee",
                table: "Cases",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OppositePartyLawyer",
                table: "Cases",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OppositeParty",
                table: "Cases",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FeeType",
                table: "Cases",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FIRNumber",
                table: "Cases",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CourtName",
                table: "Cases",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClientWhatsApp",
                table: "Cases",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClientPosition",
                table: "Cases",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClientPhone",
                table: "Cases",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClientName",
                table: "Cases",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CaseType",
                table: "Cases",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CaseStage",
                table: "Cases",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AgreedFees",
                table: "Cases",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AdvancePaid",
                table: "Cases",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CaseId",
                table: "Documents",
                column: "CaseId");
        }
    }
}
