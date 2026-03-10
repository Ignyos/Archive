using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archive.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAzureSqlWorkflowActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AzureSqlBackupWorkflowActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AzureSqlBackupJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StepOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionType = table.Column<int>(type: "INTEGER", nullable: false),
                    AzureSqlBackupDestinationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AzureSqlBackupWorkflowActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AzureSqlBackupWorkflowActions_AzureSqlBackupDestinations_AzureSqlBackupDestinationId",
                        column: x => x.AzureSqlBackupDestinationId,
                        principalTable: "AzureSqlBackupDestinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AzureSqlBackupWorkflowActions_AzureSqlBackupJobs_AzureSqlBackupJobId",
                        column: x => x.AzureSqlBackupJobId,
                        principalTable: "AzureSqlBackupJobs",
                        principalColumn: "JobId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AzureSqlBackupWorkflowActions_AzureSqlBackupDestinationId",
                table: "AzureSqlBackupWorkflowActions",
                column: "AzureSqlBackupDestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_AzureSqlBackupWorkflowActions_AzureSqlBackupJobId_StepOrder",
                table: "AzureSqlBackupWorkflowActions",
                columns: new[] { "AzureSqlBackupJobId", "StepOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AzureSqlBackupWorkflowActions");
        }
    }
}
