using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlDbReplicator.Cli.Options;
using MySqlDbReplicator.Core.Replication;

namespace MySqlDbReplicator.Cli.Commands
{
    /// <summary>
    /// Command to replicate data from source to target database
    /// </summary>
    public class ReplicateCommand : CommandBase<ReplicateOptions>
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Progress<ReplicationProgress> _progress;
        private ReplicationProgress? _lastProgress;
        private DateTime _lastProgressUpdate;

        /// <summary>
        /// Initializes a new instance of the ReplicateCommand class
        /// </summary>
        /// <param name="serviceProvider">Service provider</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="options">Command options</param>
        public ReplicateCommand(IServiceProvider serviceProvider, ILogger<ReplicateCommand> logger, ReplicateOptions options)
            : base(serviceProvider, logger, options)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _progress = new Progress<ReplicationProgress>(OnProgressChanged);
            _lastProgressUpdate = DateTime.Now;

            // Register Ctrl+C handler
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _cancellationTokenSource.Cancel();
                Logger.LogWarning("Cancellation requested. Stopping replication...");
            };
        }

        /// <summary>
        /// Executes the replicate command
        /// </summary>
        /// <returns>Exit code</returns>
        public override async Task<int> ExecuteAsync()
        {
            try
            {
                // Load configuration
                var config = LoadConfiguration();

                // Apply command line overrides
                ApplyCommandLineOverrides(config, Options);

                // Get replication service
                var replicationService = ServiceProvider.GetRequiredService<ReplicationService>();

                // Start replication
                Logger.LogInformation("Starting replication from {Source} to {Target}",
                    $"{config.Source.Host}:{config.Source.Port}/{config.Source.Database}",
                    $"{config.Target.Host}:{config.Target.Port}/{config.Target.Database}");

                var result = await replicationService.ReplicateAsync(
                    config, _progress, _cancellationTokenSource.Token);

                // Generate report if requested
                if (!string.IsNullOrEmpty(Options.ReportFile))
                {
                    GenerateReport(result, Options.ReportFile);
                }

                // Display result
                if (result.Success)
                {
                    if (result.PreviewOnly)
                    {
                        Logger.LogInformation("Schema preview completed. Use --preview=false to execute changes.");
                        return 0;
                    }

                    Logger.LogInformation("Replication completed successfully in {Duration}", result.Duration);
                    Logger.LogInformation("Replicated {RowCount} rows across {TableCount} tables",
                        result.RowsProcessed, result.TablesProcessed);

                    return 0;
                }
                else
                {
                    Logger.LogError("Replication failed: {ErrorMessage}", result.ErrorMessage);
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Replication command failed: {Message}", ex.Message);
                return 1;
            }
            finally
            {
                _cancellationTokenSource.Dispose();
            }
        }

        private void OnProgressChanged(ReplicationProgress progress)
        {
            _lastProgress = progress;

            // Limit progress updates to once per second to avoid console flooding
            if ((DateTime.Now - _lastProgressUpdate).TotalSeconds < 1)
                return;

            _lastProgressUpdate = DateTime.Now;

            // Clear current line
            Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");

            // Write progress
            Console.Write($"Table {progress.CurrentTableIndex}/{progress.TotalTables} " +
                          $"[{progress.CurrentTable}]: {progress.TablePercentComplete}% " +
                          $"({progress.ProcessedRows}/{progress.TotalRows} rows, {progress.RowsPerSecond} rows/sec) " +
                          $"Overall: {progress.OverallPercentComplete}%");
        }

        private void GenerateReport(ReplicationResult result, string reportFile)
        {
            try
            {
                Logger.LogInformation("Generating report to {ReportFile}", reportFile);

                var report = new StringBuilder();
                report.AppendLine("# MySQL Database Replication Report");
                report.AppendLine();
                report.AppendLine($"Generated at: {DateTime.Now}");
                report.AppendLine();
                report.AppendLine("## Summary");
                report.AppendLine();
                report.AppendLine($"Status: {(result.Success ? "Success" : "Failed")}");

                if (!result.Success)
                {
                    report.AppendLine($"Error: {result.ErrorMessage}");
                }

                report.AppendLine($"Duration: {result.Duration}");
                report.AppendLine($"Tables Processed: {result.TablesProcessed}");
                report.AppendLine($"Rows Processed: {result.RowsProcessed}");
                report.AppendLine();

                if (result.SchemaSyncResult != null)
                {
                    report.AppendLine("## Schema Synchronization");
                    report.AppendLine();

                    if (result.SchemaSyncResult.ComparisonResult != null)
                    {
                        report.AppendLine($"Tables with differences: {result.SchemaSyncResult.ComparisonResult.TableDifferences.Count}");
                        report.AppendLine($"Views with differences: {result.SchemaSyncResult.ComparisonResult.ViewDifferences.Count}");
                        report.AppendLine($"Stored procedures with differences: {result.SchemaSyncResult.ComparisonResult.StoredProcedureDifferences.Count}");
                        report.AppendLine($"Functions with differences: {result.SchemaSyncResult.ComparisonResult.FunctionDifferences.Count}");
                        report.AppendLine($"Triggers with differences: {result.SchemaSyncResult.ComparisonResult.TriggerDifferences.Count}");
                    }

                    report.AppendLine();
                }

                if (result.DataReplicationResult != null && result.DataReplicationResult.TableResults.Count > 0)
                {
                    report.AppendLine("## Table Details");
                    report.AppendLine();
                    report.AppendLine("| Table | Status | Rows Processed |");
                    report.AppendLine("|-------|--------|----------------|");

                    foreach (var tableResult in result.DataReplicationResult.TableResults)
                    {
                        report.AppendLine($"| {tableResult.TableName} | {(tableResult.Success ? "Success" : "Failed")} | {tableResult.RowsProcessed} |");
                    }

                    report.AppendLine();
                }

                File.WriteAllText(reportFile, report.ToString());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to generate report: {Message}", ex.Message);
            }
        }
    }
}
