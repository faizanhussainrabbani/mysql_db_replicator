using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlDbReplicator.Core.Database;
using MySqlDbReplicator.Core.Models;
using MySqlDbReplicator.Core.Schema;

namespace MySqlDbReplicator.Core.Replication
{
    /// <summary>
    /// Main service for database replication
    /// </summary>
    public class ReplicationService
    {
        private readonly DatabaseConnectionFactory _connectionFactory;
        private readonly SchemaComparer _schemaComparer;
        private readonly SchemaSynchronizer _schemaSynchronizer;
        private readonly DataReplicator _dataReplicator;
        private readonly ILogger<ReplicationService> _logger;

        /// <summary>
        /// Initializes a new instance of the ReplicationService class
        /// </summary>
        /// <param name="connectionFactory">Database connection factory</param>
        /// <param name="schemaComparer">Schema comparer</param>
        /// <param name="schemaSynchronizer">Schema synchronizer</param>
        /// <param name="dataReplicator">Data replicator</param>
        /// <param name="logger">Logger instance</param>
        public ReplicationService(
            DatabaseConnectionFactory connectionFactory,
            SchemaComparer schemaComparer,
            SchemaSynchronizer schemaSynchronizer,
            DataReplicator dataReplicator,
            ILogger<ReplicationService> logger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _schemaComparer = schemaComparer ?? throw new ArgumentNullException(nameof(schemaComparer));
            _schemaSynchronizer = schemaSynchronizer ?? throw new ArgumentNullException(nameof(schemaSynchronizer));
            _dataReplicator = dataReplicator ?? throw new ArgumentNullException(nameof(dataReplicator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Validates the replication configuration
        /// </summary>
        /// <param name="config">Replication configuration</param>
        /// <returns>Validation result</returns>
        public async Task<ValidationResult> ValidateConfigurationAsync(ReplicationConfig config)
        {
            var result = new ValidationResult();

            try
            {
                // Validate source connection
                _logger.LogInformation("Validating source database connection");
                var sourceConnectionValid = await _connectionFactory.TestConnectionAsync(config.Source);

                if (!sourceConnectionValid)
                {
                    result.Success = false;
                    result.ErrorMessage = "Could not connect to source database";
                    return result;
                }

                // Validate target connection
                _logger.LogInformation("Validating target database connection");
                var targetConnectionValid = await _connectionFactory.TestConnectionAsync(config.Target);

                if (!targetConnectionValid)
                {
                    result.Success = false;
                    result.ErrorMessage = "Could not connect to target database";
                    return result;
                }

                // Get server versions
                var sourceVersion = await _connectionFactory.GetServerVersionAsync(config.Source);
                var targetVersion = await _connectionFactory.GetServerVersionAsync(config.Target);

                _logger.LogInformation("Source MySQL version: {SourceVersion}", sourceVersion);
                _logger.LogInformation("Target MySQL version: {TargetVersion}", targetVersion);

                // Configuration is valid
                result.Success = true;
                result.SourceVersion = sourceVersion;
                result.TargetVersion = targetVersion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Configuration validation failed: {Message}", ex.Message);
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Performs the database replication
        /// </summary>
        /// <param name="config">Replication configuration</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Replication result</returns>
        public async Task<ReplicationResult> ReplicateAsync(
            ReplicationConfig config,
            IProgress<ReplicationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ReplicationResult();
            var startTime = DateTime.Now;

            try
            {
                // Validate configuration
                _logger.LogInformation("Validating configuration");
                var validationResult = await ValidateConfigurationAsync(config);

                if (!validationResult.Success)
                {
                    _logger.LogError("Configuration validation failed: {ErrorMessage}", validationResult.ErrorMessage);
                    result.Success = false;
                    result.ErrorMessage = validationResult.ErrorMessage;
                    return result;
                }

                // Synchronize schema if enabled
                if (config.SyncSchema)
                {
                    _logger.LogInformation("Synchronizing schema");

                    var schemaResult = await _schemaSynchronizer.SynchronizeSchemaAsync(
                        config, config.PreviewSchemaChanges);

                    result.SchemaSyncResult = schemaResult;

                    if (!schemaResult.Success)
                    {
                        _logger.LogError("Schema synchronization failed: {ErrorMessage}", schemaResult.ErrorMessage);
                        result.Success = false;
                        result.ErrorMessage = "Schema synchronization failed: " + schemaResult.ErrorMessage;
                        return result;
                    }

                    if (config.PreviewSchemaChanges && schemaResult.ComparisonResult.HasDifferences)
                    {
                        _logger.LogInformation("Schema differences found. Preview mode enabled. Stopping replication.");
                        result.Success = true;
                        result.PreviewOnly = true;
                        return result;
                    }
                }

                // Replicate data
                _logger.LogInformation("Replicating data");

                var dataResult = await _dataReplicator.ReplicateDataAsync(config, progress, cancellationToken);
                result.DataReplicationResult = dataResult;

                if (!dataResult.Success)
                {
                    _logger.LogError("Data replication failed: {ErrorMessage}", dataResult.ErrorMessage);
                    result.Success = false;
                    result.ErrorMessage = "Data replication failed: " + dataResult.ErrorMessage;
                    return result;
                }

                // Replication completed successfully
                var duration = DateTime.Now - startTime;

                _logger.LogInformation("Replication completed successfully in {Duration}", duration);
                _logger.LogInformation("Replicated {RowCount} rows across {TableCount} tables",
                    dataResult.TotalRowsProcessed, dataResult.TableResults.Count);

                result.Success = true;
                result.Duration = duration;
                result.RowsProcessed = dataResult.TotalRowsProcessed;
                result.TablesProcessed = dataResult.TableResults.Count;
            }
            catch (OperationCanceledException)
            {
                var duration = DateTime.Now - startTime;
                _logger.LogWarning("Replication was cancelled after {Duration}", duration);

                result.Success = false;
                result.ErrorMessage = "Operation was cancelled";
                result.Duration = duration;
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                _logger.LogError(ex, "Replication failed after {Duration}: {Message}", duration, ex.Message);

                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Duration = duration;
            }

            return result;
        }
    }

    /// <summary>
    /// Result of configuration validation
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Whether the validation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if validation failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Source database server version
        /// </summary>
        public string SourceVersion { get; set; } = string.Empty;

        /// <summary>
        /// Target database server version
        /// </summary>
        public string TargetVersion { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of a replication operation
    /// </summary>
    public class ReplicationResult
    {
        /// <summary>
        /// Whether the replication was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if replication failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Whether this was a preview only operation
        /// </summary>
        public bool PreviewOnly { get; set; }

        /// <summary>
        /// Duration of the replication
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Number of rows processed
        /// </summary>
        public long RowsProcessed { get; set; }

        /// <summary>
        /// Number of tables processed
        /// </summary>
        public int TablesProcessed { get; set; }

        /// <summary>
        /// Schema synchronization result
        /// </summary>
        public SchemaSyncResult? SchemaSyncResult { get; set; }

        /// <summary>
        /// Data replication result
        /// </summary>
        public DataReplicationResult? DataReplicationResult { get; set; }
    }
}
