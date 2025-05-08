using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using MySqlDbReplicator.Core.Database;
using MySqlDbReplicator.Core.Models;

namespace MySqlDbReplicator.Core.Schema
{
    /// <summary>
    /// Compares database schemas between source and target
    /// </summary>
    public class SchemaComparer
    {
        private readonly DatabaseConnectionFactory _connectionFactory;
        private readonly ILogger<SchemaComparer> _logger;

        /// <summary>
        /// Initializes a new instance of the SchemaComparer class
        /// </summary>
        /// <param name="connectionFactory">Database connection factory</param>
        /// <param name="logger">Logger instance</param>
        public SchemaComparer(DatabaseConnectionFactory connectionFactory, ILogger<SchemaComparer> logger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Compares schemas between source and target databases
        /// </summary>
        /// <param name="sourceConfig">Source database configuration</param>
        /// <param name="targetConfig">Target database configuration</param>
        /// <returns>Schema comparison result</returns>
        public async Task<SchemaComparisonResult> CompareSchemaAsync(DatabaseConnectionConfig sourceConfig, DatabaseConnectionConfig targetConfig)
        {
            _logger.LogInformation("Starting schema comparison between source and target databases");
            
            var result = new SchemaComparisonResult();
            
            // Compare tables
            result.TableDifferences = await CompareTablesAsync(sourceConfig, targetConfig);
            
            // Compare views
            result.ViewDifferences = await CompareViewsAsync(sourceConfig, targetConfig);
            
            // Compare stored procedures
            result.StoredProcedureDifferences = await CompareStoredProceduresAsync(sourceConfig, targetConfig);
            
            // Compare functions
            result.FunctionDifferences = await CompareFunctionsAsync(sourceConfig, targetConfig);
            
            // Compare triggers
            result.TriggerDifferences = await CompareTriggersAsync(sourceConfig, targetConfig);
            
            _logger.LogInformation("Schema comparison completed. Found {TableCount} table differences, {ViewCount} view differences, " +
                                  "{ProcCount} stored procedure differences, {FuncCount} function differences, {TriggerCount} trigger differences",
                result.TableDifferences.Count, result.ViewDifferences.Count, 
                result.StoredProcedureDifferences.Count, result.FunctionDifferences.Count, result.TriggerDifferences.Count);
            
            return result;
        }

        private async Task<List<TableDifference>> CompareTablesAsync(DatabaseConnectionConfig sourceConfig, DatabaseConnectionConfig targetConfig)
        {
            var differences = new List<TableDifference>();
            
            // Get source tables
            var sourceTables = await GetTablesAsync(sourceConfig);
            
            // Get target tables
            var targetTables = await GetTablesAsync(targetConfig);
            
            // Find missing tables
            foreach (var sourceTable in sourceTables)
            {
                var targetTable = targetTables.FirstOrDefault(t => t.Name.Equals(sourceTable.Name, StringComparison.OrdinalIgnoreCase));
                
                if (targetTable == null)
                {
                    // Table doesn't exist in target
                    differences.Add(new TableDifference
                    {
                        TableName = sourceTable.Name,
                        DifferenceType = SchemaDifferenceType.Missing,
                        SourceDefinition = await GetTableCreateStatementAsync(sourceConfig, sourceTable.Name)
                    });
                }
                else
                {
                    // Table exists, compare columns
                    var columnDifferences = await CompareTableColumnsAsync(sourceConfig, targetConfig, sourceTable.Name);
                    
                    // Compare indexes
                    var indexDifferences = await CompareTableIndexesAsync(sourceConfig, targetConfig, sourceTable.Name);
                    
                    // Compare foreign keys
                    var fkDifferences = await CompareTableForeignKeysAsync(sourceConfig, targetConfig, sourceTable.Name);
                    
                    if (columnDifferences.Count > 0 || indexDifferences.Count > 0 || fkDifferences.Count > 0)
                    {
                        differences.Add(new TableDifference
                        {
                            TableName = sourceTable.Name,
                            DifferenceType = SchemaDifferenceType.Different,
                            ColumnDifferences = columnDifferences,
                            IndexDifferences = indexDifferences,
                            ForeignKeyDifferences = fkDifferences,
                            SourceDefinition = await GetTableCreateStatementAsync(sourceConfig, sourceTable.Name),
                            TargetDefinition = await GetTableCreateStatementAsync(targetConfig, sourceTable.Name)
                        });
                    }
                }
            }
            
            return differences;
        }

        private async Task<List<TableInfo>> GetTablesAsync(DatabaseConnectionConfig config)
        {
            var tables = new List<TableInfo>();
            
            using var connection = _connectionFactory.CreateConnection(config);
            await connection.OpenAsync();
            
            var query = @"
                SELECT TABLE_NAME, TABLE_TYPE, ENGINE, TABLE_COLLATION, TABLE_COMMENT
                FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = @database
                AND TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_NAME";
            
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@database", config.Database);
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(new TableInfo
                {
                    Name = reader.GetString("TABLE_NAME"),
                    Type = reader.GetString("TABLE_TYPE"),
                    Engine = reader.IsDBNull(reader.GetOrdinal("ENGINE")) ? null : reader.GetString("ENGINE"),
                    Collation = reader.IsDBNull(reader.GetOrdinal("TABLE_COLLATION")) ? null : reader.GetString("TABLE_COLLATION"),
                    Comment = reader.IsDBNull(reader.GetOrdinal("TABLE_COMMENT")) ? null : reader.GetString("TABLE_COMMENT")
                });
            }
            
            return tables;
        }

        private async Task<string> GetTableCreateStatementAsync(DatabaseConnectionConfig config, string tableName)
        {
            using var connection = _connectionFactory.CreateConnection(config);
            await connection.OpenAsync();
            
            var query = $"SHOW CREATE TABLE `{tableName}`";
            
            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                return reader.GetString(1); // The second column contains the CREATE TABLE statement
            }
            
            return string.Empty;
        }

        private async Task<List<ColumnDifference>> CompareTableColumnsAsync(DatabaseConnectionConfig sourceConfig, DatabaseConnectionConfig targetConfig, string tableName)
        {
            var differences = new List<ColumnDifference>();
            
            // Get source columns
            var sourceColumns = await GetTableColumnsAsync(sourceConfig, tableName);
            
            // Get target columns
            var targetColumns = await GetTableColumnsAsync(targetConfig, tableName);
            
            // Find missing or different columns
            foreach (var sourceColumn in sourceColumns)
            {
                var targetColumn = targetColumns.FirstOrDefault(c => c.Name.Equals(sourceColumn.Name, StringComparison.OrdinalIgnoreCase));
                
                if (targetColumn == null)
                {
                    // Column doesn't exist in target
                    differences.Add(new ColumnDifference
                    {
                        ColumnName = sourceColumn.Name,
                        DifferenceType = SchemaDifferenceType.Missing,
                        SourceDefinition = $"{sourceColumn.Name} {sourceColumn.DataType}{(sourceColumn.IsNullable ? "" : " NOT NULL")}{(!string.IsNullOrEmpty(sourceColumn.Default) ? $" DEFAULT {sourceColumn.Default}" : "")}"
                    });
                }
                else if (!AreColumnsEqual(sourceColumn, targetColumn))
                {
                    // Column exists but is different
                    differences.Add(new ColumnDifference
                    {
                        ColumnName = sourceColumn.Name,
                        DifferenceType = SchemaDifferenceType.Different,
                        SourceDefinition = $"{sourceColumn.Name} {sourceColumn.DataType}{(sourceColumn.IsNullable ? "" : " NOT NULL")}{(!string.IsNullOrEmpty(sourceColumn.Default) ? $" DEFAULT {sourceColumn.Default}" : "")}",
                        TargetDefinition = $"{targetColumn.Name} {targetColumn.DataType}{(targetColumn.IsNullable ? "" : " NOT NULL")}{(!string.IsNullOrEmpty(targetColumn.Default) ? $" DEFAULT {targetColumn.Default}" : "")}"
                    });
                }
            }
            
            // Find extra columns in target
            foreach (var targetColumn in targetColumns)
            {
                if (!sourceColumns.Any(c => c.Name.Equals(targetColumn.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    differences.Add(new ColumnDifference
                    {
                        ColumnName = targetColumn.Name,
                        DifferenceType = SchemaDifferenceType.Extra,
                        TargetDefinition = $"{targetColumn.Name} {targetColumn.DataType}{(targetColumn.IsNullable ? "" : " NOT NULL")}{(!string.IsNullOrEmpty(targetColumn.Default) ? $" DEFAULT {targetColumn.Default}" : "")}"
                    });
                }
            }
            
            return differences;
        }

        private async Task<List<ColumnInfo>> GetTableColumnsAsync(DatabaseConnectionConfig config, string tableName)
        {
            var columns = new List<ColumnInfo>();
            
            using var connection = _connectionFactory.CreateConnection(config);
            await connection.OpenAsync();
            
            var query = @"
                SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, EXTRA, COLUMN_COMMENT
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
                columns.Add(new ColumnInfo
                {
                    Name = reader.GetString("COLUMN_NAME"),
                    DataType = reader.GetString("COLUMN_TYPE"),
                    IsNullable = reader.GetString("IS_NULLABLE") == "YES",
                    Default = reader.IsDBNull(reader.GetOrdinal("COLUMN_DEFAULT")) ? null : reader.GetString("COLUMN_DEFAULT"),
                    Extra = reader.GetString("EXTRA"),
                    Comment = reader.IsDBNull(reader.GetOrdinal("COLUMN_COMMENT")) ? null : reader.GetString("COLUMN_COMMENT")
                });
            }
            
            return columns;
        }

        private bool AreColumnsEqual(ColumnInfo source, ColumnInfo target)
        {
            return source.DataType.Equals(target.DataType, StringComparison.OrdinalIgnoreCase) &&
                   source.IsNullable == target.IsNullable &&
                   ((source.Default == null && target.Default == null) ||
                    (source.Default != null && target.Default != null && source.Default.Equals(target.Default, StringComparison.OrdinalIgnoreCase))) &&
                   source.Extra.Equals(target.Extra, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<List<IndexDifference>> CompareTableIndexesAsync(DatabaseConnectionConfig sourceConfig, DatabaseConnectionConfig targetConfig, string tableName)
        {
            // Implementation for comparing indexes
            // This is a placeholder - the actual implementation would be similar to the column comparison
            return new List<IndexDifference>();
        }

        private async Task<List<ForeignKeyDifference>> CompareTableForeignKeysAsync(DatabaseConnectionConfig sourceConfig, DatabaseConnectionConfig targetConfig, string tableName)
        {
            // Implementation for comparing foreign keys
            // This is a placeholder - the actual implementation would be similar to the column comparison
            return new List<ForeignKeyDifference>();
        }

        private async Task<List<ViewDifference>> CompareViewsAsync(DatabaseConnectionConfig sourceConfig, DatabaseConnectionConfig targetConfig)
        {
            // Implementation for comparing views
            // This is a placeholder - the actual implementation would be similar to the table comparison
            return new List<ViewDifference>();
        }

        private async Task<List<StoredProcedureDifference>> CompareStoredProceduresAsync(DatabaseConnectionConfig sourceConfig, DatabaseConnectionConfig targetConfig)
        {
            // Implementation for comparing stored procedures
            // This is a placeholder - the actual implementation would be similar to the table comparison
            return new List<StoredProcedureDifference>();
        }

        private async Task<List<FunctionDifference>> CompareFunctionsAsync(DatabaseConnectionConfig sourceConfig, DatabaseConnectionConfig targetConfig)
        {
            // Implementation for comparing functions
            // This is a placeholder - the actual implementation would be similar to the table comparison
            return new List<FunctionDifference>();
        }

        private async Task<List<TriggerDifference>> CompareTriggersAsync(DatabaseConnectionConfig sourceConfig, DatabaseConnectionConfig targetConfig)
        {
            // Implementation for comparing triggers
            // This is a placeholder - the actual implementation would be similar to the table comparison
            return new List<TriggerDifference>();
        }
    }
}
