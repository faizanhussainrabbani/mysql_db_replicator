using System;
using System.Collections.Generic;

namespace MySqlDbReplicator.Core.Models
{
    /// <summary>
    /// Configuration for the database replication process
    /// </summary>
    public class ReplicationConfig
    {
        /// <summary>
        /// Source database connection configuration
        /// </summary>
        public DatabaseConnectionConfig Source { get; set; } = new DatabaseConnectionConfig();

        /// <summary>
        /// Target database connection configuration
        /// </summary>
        public DatabaseConnectionConfig Target { get; set; } = new DatabaseConnectionConfig();

        /// <summary>
        /// Replication mode
        /// </summary>
        public ReplicationMode Mode { get; set; } = ReplicationMode.Full;

        /// <summary>
        /// Whether to synchronize schema before data replication
        /// </summary>
        public bool SyncSchema { get; set; } = true;

        /// <summary>
        /// Whether to preview schema changes before execution
        /// </summary>
        public bool PreviewSchemaChanges { get; set; } = true;

        /// <summary>
        /// Batch size for data replication
        /// </summary>
        public int BatchSize { get; set; } = 1000;

        /// <summary>
        /// Maximum number of retry attempts for failed operations
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Delay between retry attempts in milliseconds
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Conflict resolution strategy
        /// </summary>
        public ConflictResolutionStrategy ConflictResolution { get; set; } = ConflictResolutionStrategy.SourceWins;

        /// <summary>
        /// List of schemas to include in replication (empty means all schemas)
        /// </summary>
        public List<string> IncludeSchemas { get; set; } = new List<string>();

        /// <summary>
        /// List of schemas to exclude from replication
        /// </summary>
        public List<string> ExcludeSchemas { get; set; } = new List<string>();

        /// <summary>
        /// List of tables to include in replication (empty means all tables)
        /// </summary>
        public List<string> IncludeTables { get; set; } = new List<string>();

        /// <summary>
        /// List of tables to exclude from replication
        /// </summary>
        public List<string> ExcludeTables { get; set; } = new List<string>();

        /// <summary>
        /// Number of parallel threads to use for replication
        /// </summary>
        public int ParallelThreads { get; set; } = 1;

        /// <summary>
        /// Whether to enable checkpointing for resumable replication
        /// </summary>
        public bool EnableCheckpointing { get; set; } = false;

        /// <summary>
        /// Path to store checkpoint files
        /// </summary>
        public string CheckpointPath { get; set; } = "./checkpoints";

        /// <summary>
        /// Email notification settings
        /// </summary>
        public EmailNotificationConfig EmailNotification { get; set; } = new EmailNotificationConfig();

        /// <summary>
        /// Data masking rules for sensitive fields
        /// </summary>
        public List<DataMaskingRule> DataMaskingRules { get; set; } = new List<DataMaskingRule>();
    }

    /// <summary>
    /// Replication modes
    /// </summary>
    public enum ReplicationMode
    {
        /// <summary>
        /// Full replication of all data
        /// </summary>
        Full,

        /// <summary>
        /// Incremental replication of changed data only
        /// </summary>
        Incremental
    }

    /// <summary>
    /// Conflict resolution strategies
    /// </summary>
    public enum ConflictResolutionStrategy
    {
        /// <summary>
        /// Source data wins in case of conflict
        /// </summary>
        SourceWins,

        /// <summary>
        /// Target data wins in case of conflict
        /// </summary>
        TargetWins,

        /// <summary>
        /// Newer data wins based on timestamp
        /// </summary>
        NewerWins,

        /// <summary>
        /// Fail replication on conflict
        /// </summary>
        Fail
    }

    /// <summary>
    /// Email notification configuration
    /// </summary>
    public class EmailNotificationConfig
    {
        /// <summary>
        /// Whether to enable email notifications
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// SMTP server address
        /// </summary>
        public string SmtpServer { get; set; } = string.Empty;

        /// <summary>
        /// SMTP server port
        /// </summary>
        public int SmtpPort { get; set; } = 25;

        /// <summary>
        /// Whether to use SSL for SMTP connection
        /// </summary>
        public bool UseSsl { get; set; } = true;

        /// <summary>
        /// SMTP username
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// SMTP password
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Sender email address
        /// </summary>
        public string FromAddress { get; set; } = string.Empty;

        /// <summary>
        /// Recipient email addresses
        /// </summary>
        public List<string> ToAddresses { get; set; } = new List<string>();
    }

    /// <summary>
    /// Data masking rule for sensitive fields
    /// </summary>
    public class DataMaskingRule
    {
        /// <summary>
        /// Table name
        /// </summary>
        public string Table { get; set; } = string.Empty;

        /// <summary>
        /// Column name
        /// </summary>
        public string Column { get; set; } = string.Empty;

        /// <summary>
        /// Masking type
        /// </summary>
        public MaskingType Type { get; set; } = MaskingType.FullMask;

        /// <summary>
        /// Custom masking pattern (if applicable)
        /// </summary>
        public string Pattern { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data masking types
    /// </summary>
    public enum MaskingType
    {
        /// <summary>
        /// Replace entire value with asterisks
        /// </summary>
        FullMask,

        /// <summary>
        /// Keep first and last characters, mask the rest
        /// </summary>
        PartialMask,

        /// <summary>
        /// Replace with fixed value
        /// </summary>
        FixedValue,

        /// <summary>
        /// Use custom pattern
        /// </summary>
        CustomPattern
    }
}
