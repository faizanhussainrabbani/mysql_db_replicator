using System.Collections.Generic;
using CommandLine;

namespace MySqlDbReplicator.Cli.Options
{
    /// <summary>
    /// Base options for all commands
    /// </summary>
    public class BaseOptions
    {
        [Option('c', "config", Required = false, HelpText = "Path to configuration file")]
        public string? ConfigFile { get; set; }

        [Option('v', "verbose", Required = false, HelpText = "Enable verbose logging")]
        public bool Verbose { get; set; }
    }

    /// <summary>
    /// Options for the init command
    /// </summary>
    [Verb("init", HelpText = "Initialize a new configuration file")]
    public class InitOptions : BaseOptions
    {
        [Option('o', "output", Required = false, Default = "replication-config.yml", HelpText = "Output file path")]
        public string OutputFile { get; set; } = "replication-config.yml";
    }

    /// <summary>
    /// Options for the validate command
    /// </summary>
    [Verb("validate", HelpText = "Validate configuration and database connections")]
    public class ValidateOptions : BaseOptions
    {
    }

    /// <summary>
    /// Options for the compare command
    /// </summary>
    [Verb("compare", HelpText = "Compare source and target database schemas")]
    public class CompareOptions : BaseOptions
    {
        [Option('o', "output", Required = false, HelpText = "Output file for comparison results")]
        public string? OutputFile { get; set; }
    }

    /// <summary>
    /// Options for the sync-schema command
    /// </summary>
    [Verb("sync-schema", HelpText = "Synchronize schema from source to target database")]
    public class SyncSchemaOptions : BaseOptions
    {
        [Option('p', "preview", Required = false, Default = false, HelpText = "Preview changes without executing them")]
        public bool Preview { get; set; }

        [Option('o', "output", Required = false, HelpText = "Output file for synchronization script")]
        public string? OutputFile { get; set; }
    }

    /// <summary>
    /// Options for the replicate command
    /// </summary>
    [Verb("replicate", HelpText = "Replicate data from source to target database")]
    public class ReplicateOptions : BaseOptions
    {
        [Option('s', "skip-schema", Required = false, Default = false, HelpText = "Skip schema synchronization")]
        public bool SkipSchema { get; set; }

        [Option('m', "mode", Required = false, Default = "full", HelpText = "Replication mode (full or incremental)")]
        public string Mode { get; set; } = "full";

        [Option('b', "batch-size", Required = false, Default = 1000, HelpText = "Batch size for data replication")]
        public int BatchSize { get; set; } = 1000;

        [Option('i', "include", Required = false, HelpText = "Tables to include (comma-separated)")]
        public string? IncludeTables { get; set; }

        [Option('e', "exclude", Required = false, HelpText = "Tables to exclude (comma-separated)")]
        public string? ExcludeTables { get; set; }

        [Option('p', "parallel", Required = false, Default = 1, HelpText = "Number of parallel threads")]
        public int ParallelThreads { get; set; } = 1;

        [Option('r', "report", Required = false, HelpText = "Output file for replication report")]
        public string? ReportFile { get; set; }
    }

    /// <summary>
    /// Options for the create-config command
    /// </summary>
    [Verb("create-config", HelpText = "Create a configuration file interactively")]
    public class CreateConfigOptions : BaseOptions
    {
        [Option('o', "output", Required = false, Default = "replication-config.yml", HelpText = "Output file path")]
        public string OutputFile { get; set; } = "replication-config.yml";
    }
}
