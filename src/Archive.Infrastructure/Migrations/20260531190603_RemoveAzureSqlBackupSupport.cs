using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archive.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAzureSqlBackupSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hard cleanup: remove Azure SQL backup jobs and Azure SQL credential profiles.
            migrationBuilder.Sql("DELETE FROM BackupJobs WHERE JobType = 1;");
            migrationBuilder.Sql("DELETE FROM CredentialProfiles WHERE ProviderType = 0;");

            migrationBuilder.DropTable(
                name: "AzureSqlBackupWorkflowActions");

            migrationBuilder.DropTable(
                name: "AzureSqlBackupDestinations");

            migrationBuilder.DropTable(
                name: "AzureSqlBackupJobs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AzureSqlBackupJobs",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CredentialsSecretReference = table.Column<string>(type: "TEXT", nullable: true),
                    DatabaseName = table.Column<string>(type: "TEXT", nullable: false),
                    ResourceGroupName = table.Column<string>(type: "TEXT", nullable: true),
                    ServerName = table.Column<string>(type: "TEXT", nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AzureSqlBackupJobs", x => x.JobId);
                    table.ForeignKey(
                        name: "FK_AzureSqlBackupJobs_BackupJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "BackupJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AzureSqlBackupDestinations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AzureSqlBackupJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountOrDriveIdentifier = table.Column<string>(type: "TEXT", nullable: true),
                    CredentialsSecretReference = table.Column<string>(type: "TEXT", nullable: true),
                    DestinationType = table.Column<int>(type: "INTEGER", nullable: false),
                    Target = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AzureSqlBackupDestinations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AzureSqlBackupDestinations_AzureSqlBackupJobs_AzureSqlBackupJobId",
                        column: x => x.AzureSqlBackupJobId,
                        principalTable: "AzureSqlBackupJobs",
                        principalColumn: "JobId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AzureSqlBackupWorkflowActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AzureSqlBackupDestinationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AzureSqlBackupJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActionType = table.Column<int>(type: "INTEGER", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: true),
                    StepOrder = table.Column<int>(type: "INTEGER", nullable: false)
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
                name: "IX_AzureSqlBackupDestinations_AzureSqlBackupJobId",
                table: "AzureSqlBackupDestinations",
                column: "AzureSqlBackupJobId");

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
    }
}
