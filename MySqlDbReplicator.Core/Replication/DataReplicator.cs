using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using MySqlDbReplicator.Core.Database;
using MySqlDbReplicator.Core.Models;
using MySqlDbReplicator.Core.Schema;

namespace MySqlDbReplicator.Core.Replication
{
    /// <summary>
    /// Replicates data from source to target database
    /// </summary>
    public class DataReplicator
    {
        private readonly DatabaseConnectionFactory _connectionFactory;
        private readonly SchemaComparer _schemaComparer;
        private readonly ILogger<DataReplicator> _logger;

        /// <summary>
        /// Initializes a new instance of the DataReplicator class
        /// </summary>
        /// <param name="connectionFactory">Database connection factory</param>
        /// <param name="schemaComparer">Schema comparer</param>
        /// <param name="logger">Logger instance</param>
        public DataReplicator(
            DatabaseConnectionFactory connectionFactory,
            SchemaComparer schemaComparer,
            ILogger<DataReplicator> logger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _schemaComparer = schemaComparer ?? throw new ArgumentNullException(nameof(schemaComparer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Replicates data from source to target database
        /// </summary>
        /// <param name="config">Replication configuration</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Data replication result</returns>
        public async Task<DataReplicationResult> ReplicateDataAsync(
            ReplicationConfig config,
            IProgress<ReplicationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting data replication from source to target database");

            var result = new DataReplicationResult();

            try
            {
                // Get tables to replicate
                var tables = await GetTablesToReplicateAsync(config);
                _logger.LogInformation("Found {Count} tables to replicate", tables.Count);

                // Initialize progress
                var totalTables = tables.Count;
                var currentTableIndex = 0;

                // Process each table
                foreach (var table in tables)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    currentTableIndex++;
                    var tableProgress = new ReplicationProgress
                    {
                        CurrentTable = table,
                        CurrentTableIndex = currentTableIndex,
                        TotalTables = totalTables,
                        TablePercentComplete = 0,
                        OverallPercentComplete = (int)((currentTableIndex - 1) * 100.0 / totalTables)
                    };

                    progress?.Report(tableProgress);

                    _logger.LogInformation("Replicating table {TableIndex}/{TotalTables}: {TableName}",
                        currentTableIndex, totalTables, table);

                    try
                    {
                        // Replicate table data
                        var tableResult = await ReplicateTableDataAsync(config, table, tableProgress, progress, cancellationToken);
                        result.TableResults.Add(tableResult);

                        if (!tableResult.Success)
                        {
                            _logger.LogWarning("Failed to replicate table {TableName}: {ErrorMessage}",
                                table, tableResult.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error replicating table {TableName}: {Message}", table, ex.Message);

                        result.TableResults.Add(new TableReplicationResult
                        {
                            TableName = table,
                            Success = false,
                            ErrorMessage = ex.Message
                        });
                    }

                    // Update overall progress
                    tableProgress.OverallPercentComplete = (int)(currentTableIndex * 100.0 / totalTables);
                    progress?.Report(tableProgress);
                }

                // Calculate overall success
                result.Success = result.TableResults.All(r => r.Success);

                if (result.Success)
                {
                    _logger.LogInformation("Data replication completed successfully");
                }
                else
                {
                    _logger.LogWarning("Data replication completed with errors");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Data replication was cancelled");
                result.Success = false;
                result.ErrorMessage = "Operation was cancelled";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data replication failed: {Message}", ex.Message);
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private async Task<List<string>> GetTablesToReplicateAsync(ReplicationConfig config)
        {
            using var connection = _connectionFactory.CreateConnection(config.Source);
            await connection.OpenAsync();

            var tables = new List<string>();

            var query = @"
                SELECT TABLE_NAME
                FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = @database
                AND TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_NAME";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@database", config.Source.Database);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var tableName = reader.GetString(0);

                // Apply include/exclude filters
                if (ShouldReplicateTable(tableName, config))
                {
                    tables.Add(tableName);
                }
            }

            return tables;
        }

        private bool ShouldReplicateTable(string tableName, ReplicationConfig config)
        {
            // Check exclude list first
            if (config.ExcludeTables.Any(pattern => MatchesPattern(tableName, pattern)))
            {
                return false;
            }

            // If include list is empty, include all tables
            if (config.IncludeTables.Count == 0)
            {
                return true;
            }

            // Otherwise, check if table matches any include pattern
            return config.IncludeTables.Any(pattern => MatchesPattern(tableName, pattern));
        }

        private bool MatchesPattern(string tableName, string pattern)
        {
            // Simple pattern matching with * wildcard
            if (pattern == "*")
                return true;

            if (pattern.Contains("*"))
            {
                var regex = new System.Text.RegularExpressions.Regex(
                    "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                return regex.IsMatch(tableName);
            }

            return string.Equals(tableName, pattern, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<TableReplicationResult> ReplicateTableDataAsync(
            ReplicationConfig config,
            string tableName,
            ReplicationProgress progress,
            IProgress<ReplicationProgress> progressReporter,
            CancellationToken cancellationToken)
        {
            var result = new TableReplicationResult
            {
                TableName = tableName
            };

            // Get table columns
            var columns = await GetTableColumnsAsync(config.Source, tableName);

            if (columns.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "No columns found for table";
                return result;
            }

            // Apply data masking rules
            var maskingRules = config.DataMaskingRules
                .Where(r => string.Equals(r.Table, tableName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Get row count for progress reporting
            var rowCount = await GetTableRowCountAsync(config.Source, tableName);
            _logger.LogInformation("Table {TableName} has {RowCount} rows", tableName, rowCount);

            // Prepare connections
            using var sourceConnection = _connectionFactory.CreateConnection(config.Source);
            await sourceConnection.OpenAsync();

            using var targetConnection = _connectionFactory.CreateConnection(config.Target);
            await targetConnection.OpenAsync();

            // Prepare target table (truncate if full replication)
            if (config.Mode == ReplicationMode.Full)
            {
                await TruncateTargetTableAsync(targetConnection, tableName);
            }

            // Replicate data in batches
            var batchSize = config.BatchSize;
            var processedRows = 0;
            var startTime = DateTime.Now;

            while (processedRows < rowCount)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Read batch from source
                var batch = await ReadBatchFromSourceAsync(sourceConnection, tableName, columns, maskingRules,
                    processedRows, batchSize, cancellationToken);

                if (batch.Rows.Count == 0)
                    break;

                // Write batch to target
                await WriteBatchToTargetAsync(targetConnection, tableName, batch, cancellationToken);

                // Update progress
                processedRows += batch.Rows.Count;
                var rowsPerSecond = processedRows / Math.Max(1, (DateTime.Now - startTime).TotalSeconds);

                progress.TablePercentComplete = (int)(processedRows * 100.0 / Math.Max(1, rowCount));
                progress.ProcessedRows = processedRows;
                progress.TotalRows = rowCount;
                progress.RowsPerSecond = (int)rowsPerSecond;

                progressReporter?.Report(progress);

                _logger.LogDebug("Replicated {ProcessedRows}/{TotalRows} rows ({Percent}%) for table {TableName} at {RowsPerSecond} rows/sec",
                    processedRows, rowCount, progress.TablePercentComplete, tableName, (int)rowsPerSecond);
            }

            result.Success = true;
            result.RowsProcessed = processedRows;

            return result;
        }

        private async Task<List<string>> GetTableColumnsAsync(DatabaseConnectionConfig config, string tableName)
        {
            var columns = new List<string>();

            using var connection = _connectionFactory.CreateConnection(config);
            await connection.OpenAsync();

            var query = @"
                SELECT COLUMN_NAME
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = @database
                AND TABLE_NAME = @tableName
                ORDER BY ORDINAL_POSITION";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@database", config.Database);
            command.Parameters.AddWithValue("@tableName", tableName);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }

            return columns;
        }

        private async Task<long> GetTableRowCountAsync(DatabaseConnectionConfig config, string tableName)
        {
            using var connection = _connectionFactory.CreateConnection(config);
            await connection.OpenAsync();

            var query = $"SELECT COUNT(*) FROM `{tableName}`";

            using var command = new MySqlCommand(query, connection);
            var result = await command.ExecuteScalarAsync();

            return Convert.ToInt64(result);
        }

        private async Task TruncateTargetTableAsync(MySqlConnection connection, string tableName)
        {
            _logger.LogInformation("Truncating target table {TableName}", tableName);

            var query = $"TRUNCATE TABLE `{tableName}`";

            using var command = new MySqlCommand(query, connection);
            await command.ExecuteNonQueryAsync();
        }

        private async Task<DataBatch> ReadBatchFromSourceAsync(
            MySqlConnection connection,
            string tableName,
            List<string> columns,
            List<DataMaskingRule> maskingRules,
            int offset,
            int batchSize,
            CancellationToken cancellationToken)
        {
            var batch = new DataBatch
            {
                Columns = columns
            };

            var columnList = string.Join(", ", columns.Select(c => $"`{c}`"));
            var query = $"SELECT {columnList} FROM `{tableName}` LIMIT {offset}, {batchSize}";

            using var command = new MySqlCommand(query, connection);
            command.CommandTimeout = 300; // 5 minutes

            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new List<object>();

                for (int i = 0; i < columns.Count; i++)
                {
                    var columnName = columns[i];
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);

                    // Apply masking if needed
                    var maskingRule = maskingRules.FirstOrDefault(r =>
                        string.Equals(r.Column, columnName, StringComparison.OrdinalIgnoreCase));

                    if (maskingRule != null && value != null)
                    {
                        value = ApplyDataMasking(value, maskingRule);
                    }

                    row.Add(value);
                }

                batch.Rows.Add(row);
            }

            return batch;
        }

        private object ApplyDataMasking(object value, DataMaskingRule rule)
        {
            if (value == null)
                return null;

            var stringValue = value.ToString();

            switch (rule.Type)
            {
                case MaskingType.FullMask:
                    return new string('*', stringValue.Length);

                case MaskingType.PartialMask:
                    if (stringValue.Length <= 2)
                        return new string('*', stringValue.Length);

                    return stringValue[0] + new string('*', stringValue.Length - 2) + stringValue[stringValue.Length - 1];

                case MaskingType.FixedValue:
                    return rule.Pattern;

                case MaskingType.CustomPattern:
                    // Replace each character with the corresponding pattern character
                    // If pattern is shorter than the value, repeat the pattern
                    var result = new char[stringValue.Length];
                    var pattern = rule.Pattern;

                    for (int i = 0; i < stringValue.Length; i++)
                    {
                        var patternChar = pattern[i % pattern.Length];

                        if (patternChar == '#')
                            result[i] = char.IsDigit(stringValue[i]) ? stringValue[i] : '*';
                        else if (patternChar == 'X')
                            result[i] = char.IsLetter(stringValue[i]) ? stringValue[i] : '*';
                        else if (patternChar == '*')
                            result[i] = '*';
                        else
                            result[i] = patternChar;
                    }

                    return new string(result);

                default:
                    return value;
            }
        }

        private async Task WriteBatchToTargetAsync(
            MySqlConnection connection,
            string tableName,
            DataBatch batch,
            CancellationToken cancellationToken)
        {
            if (batch.Rows.Count == 0)
                return;

            var columnList = string.Join(", ", batch.Columns.Select(c => $"`{c}`"));
            var placeholders = string.Join(", ", batch.Columns.Select((_, i) => $"@p{i}"));

            var query = $"INSERT INTO `{tableName}` ({columnList}) VALUES ({placeholders})";

            using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                using var command = new MySqlCommand(query, connection, transaction);
                command.CommandTimeout = 300; // 5 minutes

                // Add parameters for each column
                for (int i = 0; i < batch.Columns.Count; i++)
                {
                    command.Parameters.Add($"@p{i}", MySqlDbType.VarChar);
                }

                // Execute for each row
                foreach (var row in batch.Rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    for (int i = 0; i < batch.Columns.Count; i++)
                    {
                        command.Parameters[$"@p{i}"].Value = row[i] ?? DBNull.Value;
                    }

                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    /// <summary>
    /// Result of a data replication operation
    /// </summary>
    public class DataReplicationResult
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
        /// Results for individual tables
        /// </summary>
        public List<TableReplicationResult> TableResults { get; set; } = new List<TableReplicationResult>();

        /// <summary>
        /// Total number of rows processed
        /// </summary>
        public long TotalRowsProcessed => TableResults.Sum(r => r.RowsProcessed);
    }

    /// <summary>
    /// Result of replicating a single table
    /// </summary>
    public class TableReplicationResult
    {
        /// <summary>
        /// Table name
        /// </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// Whether the replication was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if replication failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Number of rows processed
        /// </summary>
        public long RowsProcessed { get; set; }
    }

    /// <summary>
    /// Batch of data to be replicated
    /// </summary>
    public class DataBatch
    {
        /// <summary>
        /// Column names
        /// </summary>
        public List<string> Columns { get; set; } = new List<string>();

        /// <summary>
        /// Rows of data
        /// </summary>
        public List<List<object>> Rows { get; set; } = new List<List<object>>();
    }

    /// <summary>
    /// Progress information for replication
    /// </summary>
    public class ReplicationProgress
    {
        /// <summary>
        /// Current table being processed
        /// </summary>
        public string CurrentTable { get; set; } = string.Empty;

        /// <summary>
        /// Current table index
        /// </summary>
        public int CurrentTableIndex { get; set; }

        /// <summary>
        /// Total number of tables
        /// </summary>
        public int TotalTables { get; set; }

        /// <summary>
        /// Percentage complete for current table
        /// </summary>
        public int TablePercentComplete { get; set; }

        /// <summary>
        /// Overall percentage complete
        /// </summary>
        public int OverallPercentComplete { get; set; }

        /// <summary>
        /// Number of rows processed for current table
        /// </summary>
        public long ProcessedRows { get; set; }

        /// <summary>
        /// Total number of rows for current table
        /// </summary>
        public long TotalRows { get; set; }

        /// <summary>
        /// Rows processed per second
        /// </summary>
        public int RowsPerSecond { get; set; }
    }
}
