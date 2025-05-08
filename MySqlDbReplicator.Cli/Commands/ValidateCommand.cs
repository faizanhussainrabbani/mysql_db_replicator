using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlDbReplicator.Cli.Options;
using MySqlDbReplicator.Core.Replication;

namespace MySqlDbReplicator.Cli.Commands
{
    /// <summary>
    /// Command to validate configuration and database connections
    /// </summary>
    public class ValidateCommand : CommandBase<ValidateOptions>
    {
        /// <summary>
        /// Initializes a new instance of the ValidateCommand class
        /// </summary>
        /// <param name="serviceProvider">Service provider</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="options">Command options</param>
        public ValidateCommand(IServiceProvider serviceProvider, ILogger<ValidateCommand> logger, ValidateOptions options)
            : base(serviceProvider, logger, options)
        {
        }

        /// <summary>
        /// Executes the validate command
        /// </summary>
        /// <returns>Exit code</returns>
        public override async Task<int> ExecuteAsync()
        {
            try
            {
                // Load configuration
                var config = LoadConfiguration();
                
                // Get replication service
                var replicationService = ServiceProvider.GetRequiredService<ReplicationService>();
                
                // Validate configuration
                Logger.LogInformation("Validating configuration and database connections");
                
                var result = await replicationService.ValidateConfigurationAsync(config);
                
                if (result.Success)
                {
                    Logger.LogInformation("Configuration is valid");
                    Logger.LogInformation("Source database: {Host}:{Port}/{Database} (MySQL {Version})",
                        config.Source.Host, config.Source.Port, config.Source.Database, result.SourceVersion);
                    Logger.LogInformation("Target database: {Host}:{Port}/{Database} (MySQL {Version})",
                        config.Target.Host, config.Target.Port, config.Target.Database, result.TargetVersion);
                    
                    return 0;
                }
                else
                {
                    Logger.LogError("Configuration validation failed: {ErrorMessage}", result.ErrorMessage);
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Validation command failed: {Message}", ex.Message);
                return 1;
            }
        }
    }
}
