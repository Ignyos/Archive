using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archive.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "ExclusionPatterns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Pattern = table.Column<string>(type: "TEXT", nullable: false),
                    IsGlobal = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSystemSuggestion = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExclusionPatterns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Recursive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeleteOrphaned = table.Column<bool>(type: "INTEGER", nullable: false),
                    VerifyAfterCopy = table.Column<bool>(type: "INTEGER", nullable: false),
                    SkipHiddenAndSystem = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    SourcePath = table.Column<string>(type: "TEXT", nullable: false),
                    DestinationPath = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncMode = table.Column<int>(type: "INTEGER", nullable: false),
                    ComparisonMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    DeletedFileHandling = table.Column<int>(type: "INTEGER", nullable: true),
                    OverwriteBehavior = table.Column<int>(type: "INTEGER", nullable: false),
                    SyncOptionsId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TriggerType = table.Column<int>(type: "INTEGER", nullable: false),
                    CronExpression = table.Column<string>(type: "TEXT", nullable: true),
                    SimpleTriggerTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NotifyOnStart = table.Column<bool>(type: "INTEGER", nullable: true),
                    NotifyOnComplete = table.Column<bool>(type: "INTEGER", nullable: true),
                    NotifyOnFail = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupJobs_SyncOptions_SyncOptionsId",
                        column: x => x.SyncOptionsId,
                        principalTable: "SyncOptions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BackupJobExclusionPatterns",
                columns: table => new
                {
                    BackupJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExclusionPatternId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupJobExclusionPatterns", x => new { x.BackupJobId, x.ExclusionPatternId });
                    table.ForeignKey(
                        name: "FK_BackupJobExclusionPatterns_BackupJobs_BackupJobId",
                        column: x => x.BackupJobId,
                        principalTable: "BackupJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BackupJobExclusionPatterns_ExclusionPatterns_ExclusionPatternId",
                        column: x => x.ExclusionPatternId,
                        principalTable: "ExclusionPatterns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    FilesScanned = table.Column<int>(type: "INTEGER", nullable: false),
                    FilesCopied = table.Column<int>(type: "INTEGER", nullable: false),
                    FilesUpdated = table.Column<int>(type: "INTEGER", nullable: false),
                    FilesDeleted = table.Column<int>(type: "INTEGER", nullable: false),
                    FilesSkipped = table.Column<int>(type: "INTEGER", nullable: false),
                    FilesFailed = table.Column<int>(type: "INTEGER", nullable: false),
                    BytesTransferred = table.Column<long>(type: "INTEGER", nullable: false),
                    ErrorCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WarningCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobExecutions_BackupJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "BackupJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobExecutionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: true),
                    OperationType = table.Column<int>(type: "INTEGER", nullable: true),
                    ExceptionDetails = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionLogs_JobExecutions_JobExecutionId",
                        column: x => x.JobExecutionId,
                        principalTable: "JobExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupJobExclusionPatterns_ExclusionPatternId",
                table: "BackupJobExclusionPatterns",
                column: "ExclusionPatternId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupJobs_SyncOptionsId",
                table: "BackupJobs",
                column: "SyncOptionsId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionLogs_JobExecutionId",
                table: "ExecutionLogs",
                column: "JobExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_JobExecutions_JobId",
                table: "JobExecutions",
                column: "JobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "BackupJobExclusionPatterns");

            migrationBuilder.DropTable(
                name: "ExecutionLogs");

            migrationBuilder.DropTable(
                name: "ExclusionPatterns");

            migrationBuilder.DropTable(
                name: "JobExecutions");

            migrationBuilder.DropTable(
                name: "BackupJobs");

            migrationBuilder.DropTable(
                name: "SyncOptions");
        }
    }
}
