using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlDbReplicator.Cli.Options;
using MySqlDbReplicator.Core.Configuration;
using MySqlDbReplicator.Core.Models;

namespace MySqlDbReplicator.Cli.Commands
{
    /// <summary>
    /// Base class for all commands
    /// </summary>
    public abstract class CommandBase<TOptions> where TOptions : BaseOptions
    {
        protected readonly IServiceProvider ServiceProvider;
        protected readonly ILogger Logger;
        protected readonly TOptions Options;

        /// <summary>
        /// Initializes a new instance of the CommandBase class
        /// </summary>
        /// <param name="serviceProvider">Service provider</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="options">Command options</param>
        protected CommandBase(IServiceProvider serviceProvider, ILogger logger, TOptions options)
        {
            ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Executes the command
        /// </summary>
        /// <returns>Exit code</returns>
        public abstract Task<int> ExecuteAsync();

        /// <summary>
        /// Loads the replication configuration
        /// </summary>
        /// <returns>Replication configuration</returns>
        protected ReplicationConfig LoadConfiguration()
        {
            if (string.IsNullOrEmpty(Options.ConfigFile))
            {
                Logger.LogError("Configuration file not specified. Use the --config option to specify a configuration file.");
                throw new InvalidOperationException("Configuration file not specified");
            }

            Logger.LogInformation("Loading configuration from {ConfigFile}", Options.ConfigFile);
            
            var configManager = new ConfigurationManager(Options.ConfigFile);
            return configManager.LoadReplicationConfig();
        }

        /// <summary>
        /// Applies command line overrides to the configuration
        /// </summary>
        /// <param name="config">Replication configuration</param>
        /// <param name="options">Command options</param>
        protected void ApplyCommandLineOverrides(ReplicationConfig config, ReplicateOptions options)
        {
            if (options.SkipSchema)
            {
                config.SyncSchema = false;
            }

            if (!string.IsNullOrEmpty(options.Mode))
            {
                if (options.Mode.Equals("full", StringComparison.OrdinalIgnoreCase))
                {
                    config.Mode = ReplicationMode.Full;
                }
                else if (options.Mode.Equals("incremental", StringComparison.OrdinalIgnoreCase))
                {
                    config.Mode = ReplicationMode.Incremental;
                }
                else
                {
                    Logger.LogWarning("Invalid replication mode: {Mode}. Using default mode.", options.Mode);
                }
            }

            if (options.BatchSize > 0)
            {
                config.BatchSize = options.BatchSize;
            }

            if (!string.IsNullOrEmpty(options.IncludeTables))
            {
                config.IncludeTables.Clear();
                foreach (var table in options.IncludeTables.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    config.IncludeTables.Add(table.Trim());
                }
            }

            if (!string.IsNullOrEmpty(options.ExcludeTables))
            {
                config.ExcludeTables.Clear();
                foreach (var table in options.ExcludeTables.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    config.ExcludeTables.Add(table.Trim());
                }
            }

            if (options.ParallelThreads > 0)
            {
                config.ParallelThreads = options.ParallelThreads;
            }
        }
    }
}
