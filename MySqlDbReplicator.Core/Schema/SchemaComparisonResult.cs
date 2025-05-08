using System;
using System.Collections.Generic;

namespace MySqlDbReplicator.Core.Schema
{
    /// <summary>
    /// Result of a schema comparison between source and target databases
    /// </summary>
    public class SchemaComparisonResult
    {
        /// <summary>
        /// Differences in tables
        /// </summary>
        public List<TableDifference> TableDifferences { get; set; } = new List<TableDifference>();

        /// <summary>
        /// Differences in views
        /// </summary>
        public List<ViewDifference> ViewDifferences { get; set; } = new List<ViewDifference>();

        /// <summary>
        /// Differences in stored procedures
        /// </summary>
        public List<StoredProcedureDifference> StoredProcedureDifferences { get; set; } = new List<StoredProcedureDifference>();

        /// <summary>
        /// Differences in functions
        /// </summary>
        public List<FunctionDifference> FunctionDifferences { get; set; } = new List<FunctionDifference>();

        /// <summary>
        /// Differences in triggers
        /// </summary>
        public List<TriggerDifference> TriggerDifferences { get; set; } = new List<TriggerDifference>();

        /// <summary>
        /// Whether there are any differences
        /// </summary>
        public bool HasDifferences =>
            TableDifferences.Count > 0 ||
            ViewDifferences.Count > 0 ||
            StoredProcedureDifferences.Count > 0 ||
            FunctionDifferences.Count > 0 ||
            TriggerDifferences.Count > 0;

        /// <summary>
        /// Total number of differences
        /// </summary>
        public int TotalDifferences =>
            TableDifferences.Count +
            ViewDifferences.Count +
            StoredProcedureDifferences.Count +
            FunctionDifferences.Count +
            TriggerDifferences.Count;
    }

    /// <summary>
    /// Types of schema differences
    /// </summary>
    public enum SchemaDifferenceType
    {
        /// <summary>
        /// Object exists in source but not in target
        /// </summary>
        Missing,

        /// <summary>
        /// Object exists in both source and target but with different definitions
        /// </summary>
        Different,

        /// <summary>
        /// Object exists in target but not in source
        /// </summary>
        Extra
    }

    /// <summary>
    /// Base class for schema differences
    /// </summary>
    public abstract class SchemaDifference
    {
        /// <summary>
        /// Type of difference
        /// </summary>
        public SchemaDifferenceType DifferenceType { get; set; }

        /// <summary>
        /// Source object definition
        /// </summary>
        public string? SourceDefinition { get; set; }

        /// <summary>
        /// Target object definition
        /// </summary>
        public string? TargetDefinition { get; set; }
    }

    /// <summary>
    /// Difference in a table
    /// </summary>
    public class TableDifference : SchemaDifference
    {
        /// <summary>
        /// Table name
        /// </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// Differences in columns
        /// </summary>
        public List<ColumnDifference> ColumnDifferences { get; set; } = new List<ColumnDifference>();

        /// <summary>
        /// Differences in indexes
        /// </summary>
        public List<IndexDifference> IndexDifferences { get; set; } = new List<IndexDifference>();

        /// <summary>
        /// Differences in foreign keys
        /// </summary>
        public List<ForeignKeyDifference> ForeignKeyDifferences { get; set; } = new List<ForeignKeyDifference>();
    }

    /// <summary>
    /// Difference in a column
    /// </summary>
    public class ColumnDifference : SchemaDifference
    {
        /// <summary>
        /// Column name
        /// </summary>
        public string ColumnName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Difference in an index
    /// </summary>
    public class IndexDifference : SchemaDifference
    {
        /// <summary>
        /// Index name
        /// </summary>
        public string IndexName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Difference in a foreign key
    /// </summary>
    public class ForeignKeyDifference : SchemaDifference
    {
        /// <summary>
        /// Foreign key name
        /// </summary>
        public string ForeignKeyName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Difference in a view
    /// </summary>
    public class ViewDifference : SchemaDifference
    {
        /// <summary>
        /// View name
        /// </summary>
        public string ViewName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Difference in a stored procedure
    /// </summary>
    public class StoredProcedureDifference : SchemaDifference
    {
        /// <summary>
        /// Stored procedure name
        /// </summary>
        public string ProcedureName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Difference in a function
    /// </summary>
    public class FunctionDifference : SchemaDifference
    {
        /// <summary>
        /// Function name
        /// </summary>
        public string FunctionName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Difference in a trigger
    /// </summary>
    public class TriggerDifference : SchemaDifference
    {
        /// <summary>
        /// Trigger name
        /// </summary>
        public string TriggerName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Information about a table
    /// </summary>
    public class TableInfo
    {
        /// <summary>
        /// Table name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Table type
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Storage engine
        /// </summary>
        public string? Engine { get; set; }

        /// <summary>
        /// Table collation
        /// </summary>
        public string? Collation { get; set; }

        /// <summary>
        /// Table comment
        /// </summary>
        public string? Comment { get; set; }
    }

    /// <summary>
    /// Information about a column
    /// </summary>
    public class ColumnInfo
    {
        /// <summary>
        /// Column name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Data type
        /// </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// Whether the column allows NULL values
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// Default value
        /// </summary>
        public string? Default { get; set; }

        /// <summary>
        /// Extra attributes (e.g., auto_increment)
        /// </summary>
        public string Extra { get; set; } = string.Empty;

        /// <summary>
        /// Column comment
        /// </summary>
        public string? Comment { get; set; }
    }
}
