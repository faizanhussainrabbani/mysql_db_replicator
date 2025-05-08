using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlDbReplicator.Cli.Options;
using MySqlDbReplicator.Core.Schema;
using MySqlDbReplicator.Core.Replication;

namespace MySqlDbReplicator.Cli.Commands
{
    /// <summary>
    /// Command to synchronize schema from source to target database
    /// </summary>
    public class SyncSchemaCommand : CommandBase<SyncSchemaOptions>
    {
        /// <summary>
        /// Initializes a new instance of the SyncSchemaCommand class
        /// </summary>
        /// <param name="serviceProvider">Service provider</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="options">Command options</param>
        public SyncSchemaCommand(IServiceProvider serviceProvider, ILogger<SyncSchemaCommand> logger, SyncSchemaOptions options)
            : base(serviceProvider, logger, options)
        {
        }

        /// <summary>
        /// Executes the sync-schema command
        /// </summary>
        /// <returns>Exit code</returns>
        public override async Task<int> ExecuteAsync()
        {
            try
            {
                // Load configuration
                var config = LoadConfiguration();

                // Get schema synchronizer
                var schemaSynchronizer = ServiceProvider.GetRequiredService<SchemaSynchronizer>();

                // Synchronize schema
                Logger.LogInformation("Synchronizing schema from source to target database");
                Logger.LogInformation("Preview mode: {Preview}", Options.Preview);

                var result = await schemaSynchronizer.SynchronizeSchemaAsync(config, Options.Preview);

                // Save script if requested
                if (!string.IsNullOrEmpty(Options.OutputFile) && !string.IsNullOrEmpty(result.SynchronizationScript))
                {
                    Logger.LogInformation("Saving synchronization script to {OutputFile}", Options.OutputFile);
                    File.WriteAllText(Options.OutputFile, result.SynchronizationScript);
                }

                // Display result
                if (result.Success)
                {
                    if (Options.Preview)
                    {
                        if (result.ComparisonResult != null && result.ComparisonResult.HasDifferences)
                        {
                            Logger.LogInformation("Schema differences found. Preview mode enabled.");
                            Logger.LogInformation("Generated synchronization script with {Count} statements",
                                CountStatements(result.SynchronizationScript));

                            if (string.IsNullOrEmpty(Options.OutputFile))
                            {
                                // Display script to console if no output file specified
                                Logger.LogInformation("Synchronization script:");
                                Logger.LogInformation(result.SynchronizationScript);
                            }
                        }
                        else
                        {
                            Logger.LogInformation("No schema differences found. Synchronization not needed.");
                        }
                    }
                    else
                    {
                        Logger.LogInformation("Schema synchronization completed successfully");
                    }

                    return 0;
                }
                else
                {
                    if (string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        Logger.LogError("Schema synchronization failed with an unknown error. Check database connections and permissions.");
                    }
                    else
                    {
                        Logger.LogError("Schema synchronization failed: {ErrorMessage}", result.ErrorMessage);
                    }

                    // Try to validate the connections to provide more information
                    try
                    {
                        var replicationService = ServiceProvider.GetRequiredService<ReplicationService>();
                        var validationResult = await replicationService.ValidateConfigurationAsync(config);

                        if (!validationResult.Success)
                        {
                            Logger.LogError("Connection validation failed: {ErrorMessage}", validationResult.ErrorMessage);
                        }
                        else
                        {
                            Logger.LogInformation("Database connections are valid. Source version: {SourceVersion}, Target version: {TargetVersion}",
                                validationResult.SourceVersion, validationResult.TargetVersion);
                        }
                    }
                    catch (Exception validationEx)
                    {
                        Logger.LogError(validationEx, "Failed to validate database connections: {Message}", validationEx.Message);
                    }

                    return 1;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Sync-schema command failed: {Message}", ex.Message);
                return 1;
            }
        }

        private int CountStatements(string script)
        {
            if (string.IsNullOrEmpty(script))
                return 0;

            // Count non-comment, non-empty lines that end with a semicolon
            var count = 0;
            var lines = script.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (!string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith("--") && trimmedLine.EndsWith(";"))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
