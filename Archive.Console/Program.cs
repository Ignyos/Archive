using System.CommandLine;
using System.Text.Json;
using Archive.Core;

namespace Archive.Console;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Archive - Directory Synchronization and Backup Utility");

        // Direct mode command
        var syncCommand = CreateSyncCommand();
        rootCommand.AddCommand(syncCommand);

        // Config mode command
        var runCommand = CreateRunCommand();
        rootCommand.AddCommand(runCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static Command CreateSyncCommand()
    {
        var command = new Command("sync", "Synchronize a source directory to a destination directory");

        var sourceOption = new Option<string>(
            aliases: new[] { "--source", "-s" },
            description: "Source directory path")
        { IsRequired = true };

        var destinationOption = new Option<string>(
            aliases: new[] { "--destination", "-d" },
            description: "Destination directory path")
        { IsRequired = true };

        var recursiveOption = new Option<bool>(
            aliases: new[] { "--recursive", "-r" },
            getDefaultValue: () => true,
            description: "Recursively sync subdirectories");

        var deleteOption = new Option<bool>(
            aliases: new[] { "--delete-orphaned" },
            getDefaultValue: () => false,
            description: "Delete files in destination that don't exist in source");

        var dryRunOption = new Option<bool>(
            aliases: new[] { "--dry-run", "--preview" },
            getDefaultValue: () => false,
            description: "Preview changes without making modifications");

        var excludeOption = new Option<string[]>(
            aliases: new[] { "--exclude", "-e" },
            getDefaultValue: () => Array.Empty<string>(),
            description: "File patterns to exclude (can be specified multiple times)");

        var verifyOption = new Option<bool>(
            aliases: new[] { "--verify" },
            getDefaultValue: () => false,
            description: "Verify files after copying using hash comparison");

        command.AddOption(sourceOption);
        command.AddOption(destinationOption);
        command.AddOption(recursiveOption);
        command.AddOption(deleteOption);
        command.AddOption(dryRunOption);
        command.AddOption(excludeOption);
        command.AddOption(verifyOption);

        command.SetHandler(async (source, destination, recursive, deleteOrphaned, dryRun, exclude, verify) =>
        {
            await ExecuteSyncAsync(source, destination, recursive, deleteOrphaned, dryRun, exclude, verify);
        }, sourceOption, destinationOption, recursiveOption, deleteOption, dryRunOption, excludeOption, verifyOption);

        return command;
    }

    private static Command CreateRunCommand()
    {
        var command = new Command("run", "Run backup jobs from a configuration file");

        var configOption = new Option<string>(
            aliases: new[] { "--config", "-c" },
            description: "Path to the configuration file (JSON format)")
        { IsRequired = true };

        var jobNameOption = new Option<string?>(
            aliases: new[] { "--job", "-j" },
            description: "Specific job name to run (if not specified, runs all enabled jobs)");

        var dryRunOption = new Option<bool>(
            aliases: new[] { "--dry-run", "--preview" },
            getDefaultValue: () => false,
            description: "Preview changes without making modifications");

        command.AddOption(configOption);
        command.AddOption(jobNameOption);
        command.AddOption(dryRunOption);

        command.SetHandler(async (configPath, jobName, dryRun) =>
        {
            await ExecuteJobCollectionAsync(configPath, jobName, dryRun);
        }, configOption, jobNameOption, dryRunOption);

        return command;
    }

    private static async Task ExecuteSyncAsync(
        string source,
        string destination,
        bool recursive,
        bool deleteOrphaned,
        bool dryRun,
        string[] exclude,
        bool verify)
    {
        System.Console.WriteLine("Archive - Directory Sync");
        System.Console.WriteLine("========================");
        System.Console.WriteLine($"Source:      {source}");
        System.Console.WriteLine($"Destination: {destination}");
        System.Console.WriteLine($"Mode:        {(dryRun ? "DRY RUN (Preview)" : "LIVE")}");
        System.Console.WriteLine();

        var options = new SyncOptions
        {
            Recursive = recursive,
            DeleteOrphaned = deleteOrphaned,
            DryRun = dryRun,
            ExcludePatterns = exclude.ToList(),
            VerifyAfterCopy = verify
        };

        var engine = new SyncEngine();
        var result = await engine.SynchronizeAsync(source, destination, options);

        DisplayResult(result);
    }

    private static async Task ExecuteJobCollectionAsync(string configPath, string? jobName, bool dryRun)
    {
        System.Console.WriteLine("Archive - Backup Job Runner");
        System.Console.WriteLine("===========================");
        System.Console.WriteLine($"Config: {configPath}");
        System.Console.WriteLine();

        if (!File.Exists(configPath))
        {
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine($"Error: Configuration file not found: {configPath}");
            System.Console.ResetColor();
            return;
        }

        // Load job collection
        BackupJobCollection collection;
        try
        {
            var json = await File.ReadAllTextAsync(configPath);
            collection = JsonSerializer.Deserialize(json, AppJsonContext.Default.BackupJobCollection) ?? new BackupJobCollection();
        }
        catch (Exception ex)
        {
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine($"Error loading configuration: {ex.Message}");
            System.Console.ResetColor();
            return;
        }

        // Filter jobs
        var jobsToRun = string.IsNullOrEmpty(jobName)
            ? collection.GetRunnableJobs().ToList()
            : collection.EnabledJobs.Where(j => j.Name.Equals(jobName, StringComparison.OrdinalIgnoreCase)).ToList();

        if (jobsToRun.Count == 0)
        {
            System.Console.ForegroundColor = ConsoleColor.Yellow;
            System.Console.WriteLine(string.IsNullOrEmpty(jobName)
                ? "No runnable jobs found (check schedule or enabled status)."
                : $"Job '{jobName}' not found or not enabled.");
            System.Console.ResetColor();
            return;
        }

        System.Console.WriteLine($"Running {jobsToRun.Count} job(s)...");
        System.Console.WriteLine();

        var engine = new SyncEngine();
        var jobNumber = 1;

        foreach (var job in jobsToRun)
        {
            System.Console.ForegroundColor = ConsoleColor.Cyan;
            System.Console.WriteLine($"[{jobNumber}/{jobsToRun.Count}] {job.Name}");
            System.Console.ResetColor();
            System.Console.WriteLine($"    Source:      {job.SourcePath}");
            System.Console.WriteLine($"    Destination: {job.DestinationPath}");
            System.Console.WriteLine();

            // Override dry run if specified on command line
            if (dryRun)
            {
                job.Options.DryRun = true;
            }

            // Set up schedule callback
            job.Options.ShouldContinue = () => collection.Schedule.IsOperationAllowed(DateTime.Now);

            var result = await engine.SynchronizeAsync(
                job.SourcePath,
                job.DestinationPath,
                job.Options);

            DisplayResult(result);

            // Update job with result
            job.LastRunTime = DateTime.Now;
            job.LastResult = result;

            System.Console.WriteLine();
            jobNumber++;
        }

        // Save updated collection
        try
        {
            var json = JsonSerializer.Serialize(collection, AppJsonContext.Default.BackupJobCollection);
            await File.WriteAllTextAsync(configPath, json);
        }
        catch (Exception ex)
        {
            System.Console.ForegroundColor = ConsoleColor.Yellow;
            System.Console.WriteLine($"Warning: Could not save updated configuration: {ex.Message}");
            System.Console.ResetColor();
        }
    }

    private static void DisplayResult(SyncResult result)
    {
        System.Console.ForegroundColor = result.Success ? ConsoleColor.Green : ConsoleColor.Red;
        System.Console.WriteLine($"Status: {(result.Success ? "SUCCESS" : "FAILED")} ({result.StoppedReason})");
        System.Console.ResetColor();

        System.Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F2}s");
        System.Console.WriteLine();

        System.Console.WriteLine("Statistics:");
        System.Console.WriteLine($"  Files Copied:  {result.FilesCopied}");
        System.Console.WriteLine($"  Files Updated: {result.FilesUpdated}");
        System.Console.WriteLine($"  Files Deleted: {result.FilesDeleted}");
        System.Console.WriteLine($"  Files Skipped: {result.FilesSkipped}");
        System.Console.WriteLine($"  Bytes Transferred: {FormatBytes(result.BytesTransferred)}");
        System.Console.WriteLine();

        if (result.Errors.Count > 0)
        {
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine($"Errors ({result.Errors.Count}):");
            foreach (var error in result.Errors)
            {
                System.Console.WriteLine($"  - {error}");
            }
            System.Console.ResetColor();
            System.Console.WriteLine();
        }

        if (result.Warnings.Count > 0)
        {
            System.Console.ForegroundColor = ConsoleColor.Yellow;
            System.Console.WriteLine($"Warnings ({result.Warnings.Count}):");
            foreach (var warning in result.Warnings)
            {
                System.Console.WriteLine($"  - {warning}");
            }
            System.Console.ResetColor();
            System.Console.WriteLine();
        }

        if (result.PlannedOperations.Count > 0)
        {
            System.Console.ForegroundColor = ConsoleColor.Cyan;
            System.Console.WriteLine($"Planned Operations ({result.PlannedOperations.Count}):");
            var grouped = result.PlannedOperations.GroupBy(op => op.Type);
            foreach (var group in grouped)
            {
                System.Console.WriteLine($"  {group.Key}: {group.Count()} file(s)");
                if (group.Key != SyncOperationType.Skip)
                {
                    foreach (var op in group.Take(10))
                    {
                        System.Console.WriteLine($"    - {op.Description}");
                    }
                    if (group.Count() > 10)
                    {
                        System.Console.WriteLine($"    ... and {group.Count() - 10} more");
                    }
                }
            }
            System.Console.ResetColor();
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
