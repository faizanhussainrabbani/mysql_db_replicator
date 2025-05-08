using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlDbReplicator.Cli.Options;
using MySqlDbReplicator.Core.Configuration;

namespace MySqlDbReplicator.Cli.Commands
{
    /// <summary>
    /// Command to initialize a new configuration file
    /// </summary>
    public class InitCommand : CommandBase<InitOptions>
    {
        /// <summary>
        /// Initializes a new instance of the InitCommand class
        /// </summary>
        /// <param name="serviceProvider">Service provider</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="options">Command options</param>
        public InitCommand(IServiceProvider serviceProvider, ILogger<InitCommand> logger, InitOptions options)
            : base(serviceProvider, logger, options)
        {
        }

        /// <summary>
        /// Executes the init command
        /// </summary>
        /// <returns>Exit code</returns>
        public override async Task<int> ExecuteAsync()
        {
            try
            {
                Logger.LogInformation("Initializing new configuration file at {OutputFile}", Options.OutputFile);

                var configManager = new ConfigurationManager(string.Empty);
                configManager.CreateDefaultConfigFile(Options.OutputFile);

                Logger.LogInformation("Configuration file created successfully");
                Logger.LogInformation("Edit the file to configure your database connections and replication settings");

                return 0;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to create configuration file: {Message}", ex.Message);
                return 1;
            }
        }
    }
}
