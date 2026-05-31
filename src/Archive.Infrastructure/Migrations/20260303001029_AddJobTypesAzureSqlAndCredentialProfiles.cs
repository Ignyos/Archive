using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archive.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobTypesAzureSqlAndCredentialProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JobType",
                table: "BackupJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AzureSqlBackupJobs",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerName = table.Column<string>(type: "TEXT", nullable: false),
                    DatabaseName = table.Column<string>(type: "TEXT", nullable: false),
                    ResourceGroupName = table.Column<string>(type: "TEXT", nullable: true),
                    SubscriptionId = table.Column<string>(type: "TEXT", nullable: true),
                    CredentialsSecretReference = table.Column<string>(type: "TEXT", nullable: true)
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
                name: "CredentialProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ProtectedSecretValue = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CredentialProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AzureSqlBackupDestinations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AzureSqlBackupJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DestinationType = table.Column<int>(type: "INTEGER", nullable: false),
                    Target = table.Column<string>(type: "TEXT", nullable: false),
                    AccountOrDriveIdentifier = table.Column<string>(type: "TEXT", nullable: true),
                    CredentialsSecretReference = table.Column<string>(type: "TEXT", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_AzureSqlBackupDestinations_AzureSqlBackupJobId",
                table: "AzureSqlBackupDestinations",
                column: "AzureSqlBackupJobId");

            migrationBuilder.CreateIndex(
                name: "IX_CredentialProfiles_ProviderType_Name",
                table: "CredentialProfiles",
                columns: new[] { "ProviderType", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AzureSqlBackupDestinations");

            migrationBuilder.DropTable(
                name: "CredentialProfiles");

            migrationBuilder.DropTable(
                name: "AzureSqlBackupJobs");

            migrationBuilder.DropColumn(
                name: "JobType",
                table: "BackupJobs");
        }
    }
}
