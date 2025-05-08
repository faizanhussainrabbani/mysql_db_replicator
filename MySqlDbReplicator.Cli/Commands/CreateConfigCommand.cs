using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlDbReplicator.Cli.Options;
using MySqlDbReplicator.Core.Configuration;
using MySqlDbReplicator.Core.Models;

namespace MySqlDbReplicator.Cli.Commands
{
    /// <summary>
    /// Command to create a configuration file interactively
    /// </summary>
    public class CreateConfigCommand : CommandBase<CreateConfigOptions>
    {
        /// <summary>
        /// Initializes a new instance of the CreateConfigCommand class
        /// </summary>
        /// <param name="serviceProvider">Service provider</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="options">Command options</param>
        public CreateConfigCommand(IServiceProvider serviceProvider, ILogger<CreateConfigCommand> logger, CreateConfigOptions options)
            : base(serviceProvider, logger, options)
        {
        }

        /// <summary>
        /// Executes the create-config command
        /// </summary>
        /// <returns>Exit code</returns>
        public override async Task<int> ExecuteAsync()
        {
            try
            {
                Logger.LogInformation("Creating configuration file interactively");

                var config = new ReplicationConfig();

                // Get source database configuration
                Logger.LogInformation("Source database configuration:");
                config.Source = GetDatabaseConfiguration("source");

                // Get target database configuration
                Logger.LogInformation("Target database configuration:");
                config.Target = GetDatabaseConfiguration("target");

                // Get replication mode
                Logger.LogInformation("Replication mode (full/incremental) [full]:");
                var modeInput = Console.ReadLine();

                if (!string.IsNullOrEmpty(modeInput) && modeInput.Equals("incremental", StringComparison.OrdinalIgnoreCase))
                {
                    config.Mode = ReplicationMode.Incremental;
                }
                else
                {
                    config.Mode = ReplicationMode.Full;
                }

                // Get schema sync option
                Logger.LogInformation("Synchronize schema before data replication (y/n) [y]:");
                var syncSchemaInput = Console.ReadLine();

                config.SyncSchema = string.IsNullOrEmpty(syncSchemaInput) ||
                                   !syncSchemaInput.Equals("n", StringComparison.OrdinalIgnoreCase);

                // Get batch size
                Logger.LogInformation("Batch size for data replication [1000]:");
                var batchSizeInput = Console.ReadLine();

                if (!string.IsNullOrEmpty(batchSizeInput) && int.TryParse(batchSizeInput, out var batchSize))
                {
                    config.BatchSize = batchSize;
                }

                // Get parallel threads
                Logger.LogInformation("Number of parallel threads [1]:");
                var threadsInput = Console.ReadLine();

                if (!string.IsNullOrEmpty(threadsInput) && int.TryParse(threadsInput, out var threads))
                {
                    config.ParallelThreads = threads;
                }

                // Save configuration
                Logger.LogInformation("Saving configuration to {OutputFile}", Options.OutputFile);

                var configManager = new ConfigurationManager(string.Empty);
                configManager.SaveReplicationConfig(config, Options.OutputFile);

                Logger.LogInformation("Configuration file created successfully");

                return 0;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Create-config command failed: {Message}", ex.Message);
                return 1;
            }
        }

        private DatabaseConnectionConfig GetDatabaseConfiguration(string type)
        {
            var config = new DatabaseConnectionConfig();

            Logger.LogInformation("  Host [localhost]:");
            var hostInput = Console.ReadLine();

            if (!string.IsNullOrEmpty(hostInput))
            {
                config.Host = hostInput;
            }

            Logger.LogInformation("  Port [3306]:");
            var portInput = Console.ReadLine();

            if (!string.IsNullOrEmpty(portInput) && int.TryParse(portInput, out var port))
            {
                config.Port = port;
            }

            Logger.LogInformation("  Database name:");
            var dbInput = Console.ReadLine();

            if (!string.IsNullOrEmpty(dbInput))
            {
                config.Database = dbInput;
            }

            Logger.LogInformation("  Username:");
            var userInput = Console.ReadLine();

            if (!string.IsNullOrEmpty(userInput))
            {
                config.Username = userInput;
            }

            Logger.LogInformation("  Password:");
            var passwordInput = Console.ReadLine();

            if (!string.IsNullOrEmpty(passwordInput))
            {
                config.Password = passwordInput;
            }

            Logger.LogInformation("  Use SSL (y/n) [n]:");
            var sslInput = Console.ReadLine();

            config.UseSSL = !string.IsNullOrEmpty(sslInput) &&
                           sslInput.Equals("y", StringComparison.OrdinalIgnoreCase);

            if (config.UseSSL)
            {
                Logger.LogInformation("  SSL Certificate Path:");
                config.SslCertPath = Console.ReadLine() ?? string.Empty;

                Logger.LogInformation("  SSL Key Path:");
                config.SslKeyPath = Console.ReadLine() ?? string.Empty;

                Logger.LogInformation("  SSL CA Path:");
                config.SslCaPath = Console.ReadLine() ?? string.Empty;
            }

            return config;
        }
    }
}
