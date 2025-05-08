using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using MySqlDbReplicator.Core.Database;
using MySqlDbReplicator.Core.Models;

namespace MySqlDbReplicator.Core.Schema
{
    /// <summary>
    /// Synchronizes schema between source and target databases
    /// </summary>
    public class SchemaSynchronizer
    {
        private readonly DatabaseConnectionFactory _connectionFactory;
        private readonly SchemaComparer _schemaComparer;
        private readonly ILogger<SchemaSynchronizer> _logger;

        /// <summary>
        /// Initializes a new instance of the SchemaSynchronizer class
        /// </summary>
        /// <param name="connectionFactory">Database connection factory</param>
        /// <param name="schemaComparer">Schema comparer</param>
        /// <param name="logger">Logger instance</param>
        public SchemaSynchronizer(
            DatabaseConnectionFactory connectionFactory,
            SchemaComparer schemaComparer,
            ILogger<SchemaSynchronizer> logger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _schemaComparer = schemaComparer ?? throw new ArgumentNullException(nameof(schemaComparer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Synchronizes schema from source to target database
        /// </summary>
        /// <param name="config">Replication configuration</param>
        /// <param name="previewOnly">Whether to only preview changes without executing them</param>
        /// <returns>Schema synchronization result</returns>
        public async Task<SchemaSyncResult> SynchronizeSchemaAsync(ReplicationConfig config, bool previewOnly = false)
        {
            _logger.LogInformation("Starting schema synchronization from source to target database");

            var result = new SchemaSyncResult();

            try
            {
                // Compare schemas
                var comparisonResult = await _schemaComparer.CompareSchemaAsync(config.Source, config.Target);
                result.ComparisonResult = comparisonResult;

                if (!comparisonResult.HasDifferences)
                {
                    _logger.LogInformation("No schema differences found. Synchronization not needed.");
                    return result;
                }

                // Generate synchronization script
                var script = GenerateSynchronizationScript(comparisonResult);
                result.SynchronizationScript = script;

                if (previewOnly)
                {
                    _logger.LogInformation("Preview mode enabled. Synchronization script generated but not executed.");
                    result.Success = true; // Mark as success in preview mode
                    return result;
                }

                // Execute synchronization script
                await ExecuteSynchronizationScriptAsync(config.Target, script);
                result.Success = true;

                _logger.LogInformation("Schema synchronization completed successfully");
            }
            catch (MySqlException mySqlEx)
            {
                _logger.LogError(mySqlEx, "MySQL error during schema synchronization: {Message}, Error Code: {ErrorCode}",
                    mySqlEx.Message, mySqlEx.Number);
                result.Success = false;
                result.ErrorMessage = $"MySQL error {mySqlEx.Number}: {mySqlEx.Message}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Schema synchronization failed: {Message}", ex.Message);
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private string GenerateSynchronizationScript(SchemaComparisonResult comparisonResult)
        {
            var script = new StringBuilder();

            // Add script header
            script.AppendLine("-- Schema synchronization script");
            script.AppendLine("-- Generated at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            script.AppendLine();

            // Add tables
            foreach (var tableDiff in comparisonResult.TableDifferences)
            {
                if (tableDiff.DifferenceType == SchemaDifferenceType.Missing)
                {
                    // Create missing table
                    script.AppendLine($"-- Create missing table: {tableDiff.TableName}");
                    script.AppendLine(tableDiff.SourceDefinition);
                    script.AppendLine();
                }
                else if (tableDiff.DifferenceType == SchemaDifferenceType.Different)
                {
                    // Alter existing table
                    script.AppendLine($"-- Modify table: {tableDiff.TableName}");

                    // Add column modifications
                    foreach (var colDiff in tableDiff.ColumnDifferences)
                    {
                        if (colDiff.DifferenceType == SchemaDifferenceType.Missing)
                        {
                            // Add missing column
                            script.AppendLine($"ALTER TABLE `{tableDiff.TableName}` ADD COLUMN {colDiff.SourceDefinition};");
                        }
                        else if (colDiff.DifferenceType == SchemaDifferenceType.Different)
                        {
                            // Modify existing column
                            script.AppendLine($"ALTER TABLE `{tableDiff.TableName}` MODIFY COLUMN {colDiff.SourceDefinition};");
                        }
                        else if (colDiff.DifferenceType == SchemaDifferenceType.Extra)
                        {
                            // Drop extra column (commented out for safety)
                            script.AppendLine($"-- ALTER TABLE `{tableDiff.TableName}` DROP COLUMN `{colDiff.ColumnName}`;");
                        }
                    }

                    // Add index modifications
                    foreach (var idxDiff in tableDiff.IndexDifferences)
                    {
                        if (idxDiff.DifferenceType == SchemaDifferenceType.Missing)
                        {
                            // Add missing index
                            script.AppendLine($"-- Add missing index: {idxDiff.IndexName}");
                            script.AppendLine(idxDiff.SourceDefinition);
                        }
                        else if (idxDiff.DifferenceType == SchemaDifferenceType.Different)
                        {
                            // Modify existing index
                            script.AppendLine($"-- Drop and recreate index: {idxDiff.IndexName}");
                            script.AppendLine($"DROP INDEX `{idxDiff.IndexName}` ON `{tableDiff.TableName}`;");
                            script.AppendLine(idxDiff.SourceDefinition);
                        }
                    }

                    // Add foreign key modifications
                    foreach (var fkDiff in tableDiff.ForeignKeyDifferences)
                    {
                        if (fkDiff.DifferenceType == SchemaDifferenceType.Missing)
                        {
                            // Add missing foreign key
                            script.AppendLine($"-- Add missing foreign key: {fkDiff.ForeignKeyName}");
                            script.AppendLine(fkDiff.SourceDefinition);
                        }
                        else if (fkDiff.DifferenceType == SchemaDifferenceType.Different)
                        {
                            // Modify existing foreign key
                            script.AppendLine($"-- Drop and recreate foreign key: {fkDiff.ForeignKeyName}");
                            script.AppendLine($"ALTER TABLE `{tableDiff.TableName}` DROP FOREIGN KEY `{fkDiff.ForeignKeyName}`;");
                            script.AppendLine(fkDiff.SourceDefinition);
                        }
                    }

                    script.AppendLine();
                }
            }

            // Add views
            foreach (var viewDiff in comparisonResult.ViewDifferences)
            {
                if (viewDiff.DifferenceType == SchemaDifferenceType.Missing)
                {
                    // Create missing view
                    script.AppendLine($"-- Create missing view: {viewDiff.ViewName}");
                    script.AppendLine(viewDiff.SourceDefinition);
                    script.AppendLine();
                }
                else if (viewDiff.DifferenceType == SchemaDifferenceType.Different)
                {
                    // Replace existing view
                    script.AppendLine($"-- Replace view: {viewDiff.ViewName}");
                    script.AppendLine($"DROP VIEW IF EXISTS `{viewDiff.ViewName}`;");
                    script.AppendLine(viewDiff.SourceDefinition);
                    script.AppendLine();
                }
            }

            // Add stored procedures
            foreach (var procDiff in comparisonResult.StoredProcedureDifferences)
            {
                if (procDiff.DifferenceType == SchemaDifferenceType.Missing ||
                    procDiff.DifferenceType == SchemaDifferenceType.Different)
                {
                    // Create or replace stored procedure
                    script.AppendLine($"-- Create or replace stored procedure: {procDiff.ProcedureName}");
                    script.AppendLine($"DROP PROCEDURE IF EXISTS `{procDiff.ProcedureName}`;");
                    script.AppendLine("DELIMITER //");
                    script.AppendLine(procDiff.SourceDefinition);
                    script.AppendLine("//");
                    script.AppendLine("DELIMITER ;");
                    script.AppendLine();
                }
            }

            // Add functions
            foreach (var funcDiff in comparisonResult.FunctionDifferences)
            {
                if (funcDiff.DifferenceType == SchemaDifferenceType.Missing ||
                    funcDiff.DifferenceType == SchemaDifferenceType.Different)
                {
                    // Create or replace function
                    script.AppendLine($"-- Create or replace function: {funcDiff.FunctionName}");
                    script.AppendLine($"DROP FUNCTION IF EXISTS `{funcDiff.FunctionName}`;");
                    script.AppendLine("DELIMITER //");
                    script.AppendLine(funcDiff.SourceDefinition);
                    script.AppendLine("//");
                    script.AppendLine("DELIMITER ;");
                    script.AppendLine();
                }
            }

            // Add triggers
            foreach (var triggerDiff in comparisonResult.TriggerDifferences)
            {
                if (triggerDiff.DifferenceType == SchemaDifferenceType.Missing ||
                    triggerDiff.DifferenceType == SchemaDifferenceType.Different)
                {
                    // Create or replace trigger
                    script.AppendLine($"-- Create or replace trigger: {triggerDiff.TriggerName}");
                    script.AppendLine($"DROP TRIGGER IF EXISTS `{triggerDiff.TriggerName}`;");
                    script.AppendLine("DELIMITER //");
                    script.AppendLine(triggerDiff.SourceDefinition);
                    script.AppendLine("//");
                    script.AppendLine("DELIMITER ;");
                    script.AppendLine();
                }
            }

            return script.ToString();
        }

        private async Task ExecuteSynchronizationScriptAsync(DatabaseConnectionConfig config, string script)
        {
            _logger.LogInformation("Executing schema synchronization script");

            using var connection = _connectionFactory.CreateConnection(config);
            await connection.OpenAsync();

            // Split script into individual statements
            var statements = SplitSqlStatements(script);

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                foreach (var statement in statements)
                {
                    if (string.IsNullOrWhiteSpace(statement) || statement.StartsWith("--"))
                        continue;

                    // Sanitize the statement to fix common issues
                    var sanitizedStatement = SanitizeSqlStatement(statement);

                    using var command = new MySqlCommand(sanitizedStatement, connection, transaction);
                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Schema synchronization script executed successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to execute schema synchronization script: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Sanitizes a SQL statement to fix common issues
        /// </summary>
        /// <param name="statement">The SQL statement to sanitize</param>
        /// <returns>The sanitized SQL statement</returns>
        private string SanitizeSqlStatement(string statement)
        {
            // Fix GUID/UUID default values
            // Replace DEFAULT 00000000-0000-0000-0000-000000000000 with DEFAULT '00000000-0000-0000-0000-000000000000'
            if (statement.Contains("DEFAULT 00000000-0000-0000-0000-000000000000"))
            {
                statement = statement.Replace("DEFAULT 00000000-0000-0000-0000-000000000000", "DEFAULT '00000000-0000-0000-0000-000000000000'");
                _logger.LogInformation("Fixed GUID/UUID default value in statement: {Statement}", statement);
            }

            // Fix datetime default values
            // Replace DEFAULT 0001-01-01 00:00:00.000000 with DEFAULT '0001-01-01 00:00:00.000000'
            if (statement.Contains("DEFAULT 0001-01-01 00:00:00.000000"))
            {
                statement = statement.Replace("DEFAULT 0001-01-01 00:00:00.000000", "DEFAULT '0001-01-01 00:00:00.000000'");
                _logger.LogInformation("Fixed datetime default value in statement: {Statement}", statement);
            }

            return statement;
        }

        private List<string> SplitSqlStatements(string script)
        {
            var statements = new List<string>();
            var currentStatement = new StringBuilder();
            var inString = false;
            var inComment = false;
            var delimiter = ";";

            for (int i = 0; i < script.Length; i++)
            {
                char c = script[i];
                char? nextChar = i < script.Length - 1 ? script[i + 1] : (char?)null;

                // Handle comments
                if (!inString && c == '-' && nextChar == '-')
                {
                    inComment = true;
                    currentStatement.Append(c);
                    continue;
                }

                if (inComment && (c == '\n' || c == '\r'))
                {
                    inComment = false;
                }

                // Handle strings
                if (c == '\'' && !inComment)
                {
                    inString = !inString;
                }

                // Handle delimiter changes
                if (!inString && !inComment && i + 9 < script.Length &&
                    script.Substring(i, 9).Equals("DELIMITER ", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract new delimiter
                    int j = i + 9;
                    while (j < script.Length && !char.IsWhiteSpace(script[j]))
                    {
                        j++;
                    }

                    delimiter = script.Substring(i + 9, j - (i + 9));

                    // Skip to end of line
                    while (i < script.Length && script[i] != '\n')
                    {
                        i++;
                    }

                    continue;
                }

                // Check for statement end
                if (!inString && !inComment &&
                    i + delimiter.Length <= script.Length &&
                    script.Substring(i, delimiter.Length) == delimiter)
                {
                    currentStatement.Append(delimiter);
                    statements.Add(currentStatement.ToString().Trim());
                    currentStatement.Clear();
                    i += delimiter.Length - 1;
                    continue;
                }

                currentStatement.Append(c);
            }

            // Add the last statement if not empty
            if (currentStatement.Length > 0)
            {
                statements.Add(currentStatement.ToString().Trim());
            }

            return statements;
        }
    }

    /// <summary>
    /// Result of a schema synchronization operation
    /// </summary>
    public class SchemaSyncResult
    {
        /// <summary>
        /// Whether the synchronization was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if synchronization failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Schema comparison result
        /// </summary>
        public SchemaComparisonResult? ComparisonResult { get; set; }

        /// <summary>
        /// Generated synchronization script
        /// </summary>
        public string SynchronizationScript { get; set; } = string.Empty;
    }
}
