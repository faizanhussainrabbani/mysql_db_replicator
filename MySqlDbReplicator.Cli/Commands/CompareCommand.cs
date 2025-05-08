using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlDbReplicator.Cli.Options;
using MySqlDbReplicator.Core.Schema;

namespace MySqlDbReplicator.Cli.Commands
{
    /// <summary>
    /// Command to compare source and target database schemas
    /// </summary>
    public class CompareCommand : CommandBase<CompareOptions>
    {
        /// <summary>
        /// Initializes a new instance of the CompareCommand class
        /// </summary>
        /// <param name="serviceProvider">Service provider</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="options">Command options</param>
        public CompareCommand(IServiceProvider serviceProvider, ILogger<CompareCommand> logger, CompareOptions options)
            : base(serviceProvider, logger, options)
        {
        }

        /// <summary>
        /// Executes the compare command
        /// </summary>
        /// <returns>Exit code</returns>
        public override async Task<int> ExecuteAsync()
        {
            try
            {
                // Load configuration
                var config = LoadConfiguration();
                
                // Get schema comparer
                var schemaComparer = ServiceProvider.GetRequiredService<SchemaComparer>();
                
                // Compare schemas
                Logger.LogInformation("Comparing schemas between source and target databases");
                
                var result = await schemaComparer.CompareSchemaAsync(config.Source, config.Target);
                
                // Display results
                Logger.LogInformation("Schema comparison completed");
                Logger.LogInformation("Found {TableCount} table differences, {ViewCount} view differences, " +
                                     "{ProcCount} stored procedure differences, {FuncCount} function differences, " +
                                     "{TriggerCount} trigger differences",
                    result.TableDifferences.Count, result.ViewDifferences.Count,
                    result.StoredProcedureDifferences.Count, result.FunctionDifferences.Count,
                    result.TriggerDifferences.Count);
                
                // Generate report if requested
                if (!string.IsNullOrEmpty(Options.OutputFile))
                {
                    GenerateReport(result, Options.OutputFile);
                }
                else
                {
                    // Display detailed results to console
                    DisplayDetailedResults(result);
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Compare command failed: {Message}", ex.Message);
                return 1;
            }
        }

        private void DisplayDetailedResults(SchemaComparisonResult result)
        {
            if (!result.HasDifferences)
            {
                Logger.LogInformation("No schema differences found");
                return;
            }
            
            // Display table differences
            if (result.TableDifferences.Count > 0)
            {
                Logger.LogInformation("Table differences:");
                
                foreach (var tableDiff in result.TableDifferences)
                {
                    if (tableDiff.DifferenceType == SchemaDifferenceType.Missing)
                    {
                        Logger.LogInformation("  - Table {TableName} is missing in target", tableDiff.TableName);
                    }
                    else if (tableDiff.DifferenceType == SchemaDifferenceType.Different)
                    {
                        Logger.LogInformation("  - Table {TableName} has differences:", tableDiff.TableName);
                        
                        if (tableDiff.ColumnDifferences.Count > 0)
                        {
                            Logger.LogInformation("    - {Count} column differences", tableDiff.ColumnDifferences.Count);
                        }
                        
                        if (tableDiff.IndexDifferences.Count > 0)
                        {
                            Logger.LogInformation("    - {Count} index differences", tableDiff.IndexDifferences.Count);
                        }
                        
                        if (tableDiff.ForeignKeyDifferences.Count > 0)
                        {
                            Logger.LogInformation("    - {Count} foreign key differences", tableDiff.ForeignKeyDifferences.Count);
                        }
                    }
                }
            }
            
            // Display view differences
            if (result.ViewDifferences.Count > 0)
            {
                Logger.LogInformation("View differences:");
                
                foreach (var viewDiff in result.ViewDifferences)
                {
                    if (viewDiff.DifferenceType == SchemaDifferenceType.Missing)
                    {
                        Logger.LogInformation("  - View {ViewName} is missing in target", viewDiff.ViewName);
                    }
                    else if (viewDiff.DifferenceType == SchemaDifferenceType.Different)
                    {
                        Logger.LogInformation("  - View {ViewName} has different definition", viewDiff.ViewName);
                    }
                }
            }
            
            // Display stored procedure differences
            if (result.StoredProcedureDifferences.Count > 0)
            {
                Logger.LogInformation("Stored procedure differences:");
                
                foreach (var procDiff in result.StoredProcedureDifferences)
                {
                    if (procDiff.DifferenceType == SchemaDifferenceType.Missing)
                    {
                        Logger.LogInformation("  - Stored procedure {ProcName} is missing in target", procDiff.ProcedureName);
                    }
                    else if (procDiff.DifferenceType == SchemaDifferenceType.Different)
                    {
                        Logger.LogInformation("  - Stored procedure {ProcName} has different definition", procDiff.ProcedureName);
                    }
                }
            }
            
            // Display function differences
            if (result.FunctionDifferences.Count > 0)
            {
                Logger.LogInformation("Function differences:");
                
                foreach (var funcDiff in result.FunctionDifferences)
                {
                    if (funcDiff.DifferenceType == SchemaDifferenceType.Missing)
                    {
                        Logger.LogInformation("  - Function {FuncName} is missing in target", funcDiff.FunctionName);
                    }
                    else if (funcDiff.DifferenceType == SchemaDifferenceType.Different)
                    {
                        Logger.LogInformation("  - Function {FuncName} has different definition", funcDiff.FunctionName);
                    }
                }
            }
            
            // Display trigger differences
            if (result.TriggerDifferences.Count > 0)
            {
                Logger.LogInformation("Trigger differences:");
                
                foreach (var triggerDiff in result.TriggerDifferences)
                {
                    if (triggerDiff.DifferenceType == SchemaDifferenceType.Missing)
                    {
                        Logger.LogInformation("  - Trigger {TriggerName} is missing in target", triggerDiff.TriggerName);
                    }
                    else if (triggerDiff.DifferenceType == SchemaDifferenceType.Different)
                    {
                        Logger.LogInformation("  - Trigger {TriggerName} has different definition", triggerDiff.TriggerName);
                    }
                }
            }
        }

        private void GenerateReport(SchemaComparisonResult result, string reportFile)
        {
            try
            {
                Logger.LogInformation("Generating report to {ReportFile}", reportFile);
                
                var report = new StringBuilder();
                report.AppendLine("# Schema Comparison Report");
                report.AppendLine();
                report.AppendLine($"Generated at: {DateTime.Now}");
                report.AppendLine();
                
                report.AppendLine("## Summary");
                report.AppendLine();
                report.AppendLine($"Tables with differences: {result.TableDifferences.Count}");
                report.AppendLine($"Views with differences: {result.ViewDifferences.Count}");
                report.AppendLine($"Stored procedures with differences: {result.StoredProcedureDifferences.Count}");
                report.AppendLine($"Functions with differences: {result.FunctionDifferences.Count}");
                report.AppendLine($"Triggers with differences: {result.TriggerDifferences.Count}");
                report.AppendLine();
                
                if (!result.HasDifferences)
                {
                    report.AppendLine("No schema differences found");
                }
                else
                {
                    // Table differences
                    if (result.TableDifferences.Count > 0)
                    {
                        report.AppendLine("## Table Differences");
                        report.AppendLine();
                        
                        foreach (var tableDiff in result.TableDifferences)
                        {
                            if (tableDiff.DifferenceType == SchemaDifferenceType.Missing)
                            {
                                report.AppendLine($"### Table `{tableDiff.TableName}` is missing in target");
                                report.AppendLine();
                                report.AppendLine("```sql");
                                report.AppendLine(tableDiff.SourceDefinition);
                                report.AppendLine("```");
                            }
                            else if (tableDiff.DifferenceType == SchemaDifferenceType.Different)
                            {
                                report.AppendLine($"### Table `{tableDiff.TableName}` has differences");
                                report.AppendLine();
                                
                                // Column differences
                                if (tableDiff.ColumnDifferences.Count > 0)
                                {
                                    report.AppendLine("#### Column Differences");
                                    report.AppendLine();
                                    report.AppendLine("| Column | Type | Source | Target |");
                                    report.AppendLine("|--------|------|--------|--------|");
                                    
                                    foreach (var colDiff in tableDiff.ColumnDifferences)
                                    {
                                        var diffType = colDiff.DifferenceType.ToString();
                                        var source = colDiff.DifferenceType == SchemaDifferenceType.Extra ? "-" : colDiff.SourceDefinition;
                                        var target = colDiff.DifferenceType == SchemaDifferenceType.Missing ? "-" : colDiff.TargetDefinition;
                                        
                                        report.AppendLine($"| {colDiff.ColumnName} | {diffType} | {source} | {target} |");
                                    }
                                    
                                    report.AppendLine();
                                }
                                
                                // Index differences
                                if (tableDiff.IndexDifferences.Count > 0)
                                {
                                    report.AppendLine("#### Index Differences");
                                    report.AppendLine();
                                    // Add index differences details
                                    report.AppendLine();
                                }
                                
                                // Foreign key differences
                                if (tableDiff.ForeignKeyDifferences.Count > 0)
                                {
                                    report.AppendLine("#### Foreign Key Differences");
                                    report.AppendLine();
                                    // Add foreign key differences details
                                    report.AppendLine();
                                }
                            }
                        }
                    }
                    
                    // Add sections for views, stored procedures, functions, and triggers
                    // Similar to the table differences section
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
