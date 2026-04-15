using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFirmsAndSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Firms_FirmId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Cases_Firms_FirmId",
                table: "Cases");

            migrationBuilder.DropForeignKey(
                name: "FK_Chats_Firms_FirmId",
                table: "Chats");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Firms_FirmId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Hearings_Firms_FirmId",
                table: "Hearings");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Firms_FirmId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Firms_FirmId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Firms_FirmId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Firms_FirmId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "Firms");

            migrationBuilder.DropIndex(
                name: "IX_Users_FirmId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Payments_FirmId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_FirmId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_FirmId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceNumber_FirmId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Hearings_FirmId",
                table: "Hearings");

            migrationBuilder.DropIndex(
                name: "IX_Documents_FirmId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Chats_FirmId",
                table: "Chats");

            migrationBuilder.DropIndex(
                name: "IX_Cases_CaseNumber",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_Cases_FirmId",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_Cases_FirmId_Status",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_FirmId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "FirmId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FirmId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "FirmId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "FirmId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "FirmId",
                table: "Hearings");

            migrationBuilder.DropColumn(
                name: "FirmId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FirmId",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "FirmId",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "FirmId",
                table: "AuditLogs");

            // Deduplicate InvoiceNumbers that were only unique per-firm before.
            // All existing data is test data (confirmed disposable). This renames
            // any duplicates so the new global unique index can be created cleanly.
            migrationBuilder.Sql(@"
                UPDATE ""Invoices"" i
                SET ""InvoiceNumber"" = i.""InvoiceNumber"" || '-' || CAST(i.""Id"" AS varchar(8))
                WHERE i.""Id"" NOT IN (
                    SELECT DISTINCT ON (""InvoiceNumber"") ""Id""
                    FROM ""Invoices""
                    ORDER BY ""InvoiceNumber"", ""CreatedAt""
                );
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            // Deduplicate CaseNumbers that were only unique per-firm before.
            migrationBuilder.Sql(@"
                UPDATE ""Cases"" c
                SET ""CaseNumber"" = c.""CaseNumber"" || '-' || CAST(c.""Id"" AS varchar(8))
                WHERE c.""Id"" NOT IN (
                    SELECT DISTINCT ON (""CaseNumber"") ""Id""
                    FROM ""Cases""
                    ORDER BY ""CaseNumber"", ""CreatedAt""
                );
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_CaseNumber_Unique",
                table: "Cases",
                column: "CaseNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Cases_CaseNumber_Unique",
                table: "Cases");

            migrationBuilder.AddColumn<Guid>(
                name: "FirmId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FirmId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FirmId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FirmId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FirmId",
                table: "Hearings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FirmId",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FirmId",
                table: "Chats",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FirmId",
                table: "Cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FirmId",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Firms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GstNumber = table.Column<string>(type: "text", nullable: true),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Plan = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubscriptionStatus = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Firms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Plan = table.Column<int>(type: "integer", nullable: false),
                    RazorpaySubscriptionId = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Firms_FirmId",
                        column: x => x.FirmId,
                        principalTable: "Firms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_FirmId",
                table: "Users",
                column: "FirmId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_FirmId",
                table: "Payments",
                column: "FirmId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_FirmId",
                table: "Notifications",
                column: "FirmId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_FirmId",
                table: "Invoices",
                column: "FirmId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber_FirmId",
                table: "Invoices",
                columns: new[] { "InvoiceNumber", "FirmId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hearings_FirmId",
                table: "Hearings",
                column: "FirmId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_FirmId",
                table: "Documents",
                column: "FirmId");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_FirmId",
                table: "Chats",
                column: "FirmId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_CaseNumber",
                table: "Cases",
                column: "CaseNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_FirmId",
                table: "Cases",
                column: "FirmId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_FirmId_Status",
                table: "Cases",
                columns: new[] { "FirmId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_FirmId",
                table: "AuditLogs",
                column: "FirmId");

            migrationBuilder.CreateIndex(
                name: "IX_Firms_Slug",
                table: "Firms",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_FirmId",
                table: "Subscriptions",
                column: "FirmId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Firms_FirmId",
                table: "AuditLogs",
                column: "FirmId",
                principalTable: "Firms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Cases_Firms_FirmId",
                table: "Cases",
                column: "FirmId",
                principalTable: "Firms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Chats_Firms_FirmId",
                table: "Chats",
                column: "FirmId",
                principalTable: "Firms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Firms_FirmId",
                table: "Documents",
                column: "FirmId",
                principalTable: "Firms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Hearings_Firms_FirmId",
                table: "Hearings",
                column: "FirmId",
                principalTable: "Firms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Firms_FirmId",
                table: "Invoices",
                column: "FirmId",
                principalTable: "Firms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Firms_FirmId",
                table: "Notifications",
                column: "FirmId",
                principalTable: "Firms",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Firms_FirmId",
                table: "Payments",
                column: "FirmId",
                principalTable: "Firms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Firms_FirmId",
                table: "Users",
                column: "FirmId",
                principalTable: "Firms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
