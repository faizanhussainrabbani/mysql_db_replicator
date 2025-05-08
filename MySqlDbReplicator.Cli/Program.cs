using System;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlDbReplicator.Cli.Commands;
using MySqlDbReplicator.Cli.Options;
using MySqlDbReplicator.Core.Database;
using MySqlDbReplicator.Core.Replication;
using MySqlDbReplicator.Core.Schema;

namespace MySqlDbReplicator.Cli
{
    /// <summary>
    /// Main program class
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Entry point
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Exit code</returns>
        public static async Task<int> Main(string[] args)
        {
            // Setup dependency injection
            var serviceProvider = ConfigureServices();

            // Parse command line
            return await Parser.Default.ParseArguments<
                    InitOptions,
                    ValidateOptions,
                    CompareOptions,
                    SyncSchemaOptions,
                    ReplicateOptions,
                    CreateConfigOptions>(args)
                .MapResult(
                    (InitOptions opts) => ExecuteCommand<InitCommand, InitOptions>(serviceProvider, opts),
                    (ValidateOptions opts) => ExecuteCommand<ValidateCommand, ValidateOptions>(serviceProvider, opts),
                    (CompareOptions opts) => ExecuteCommand<CompareCommand, CompareOptions>(serviceProvider, opts),
                    (SyncSchemaOptions opts) => ExecuteCommand<SyncSchemaCommand, SyncSchemaOptions>(serviceProvider, opts),
                    (ReplicateOptions opts) => ExecuteCommand<ReplicateCommand, ReplicateOptions>(serviceProvider, opts),
                    (CreateConfigOptions opts) => ExecuteCommand<CreateConfigCommand, CreateConfigOptions>(serviceProvider, opts),
                    errs => Task.FromResult(1));
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();

                // Default to Information level, can be overridden with --verbose flag
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Add core services
            services.AddSingleton<DatabaseConnectionFactory>();
            services.AddSingleton<SchemaComparer>();
            services.AddSingleton<SchemaSynchronizer>();
            services.AddSingleton<DataReplicator>();
            services.AddSingleton<ReplicationService>();

            // Add commands
            services.AddTransient<InitCommand>();
            services.AddTransient<ValidateCommand>();
            services.AddTransient<CompareCommand>();
            services.AddTransient<SyncSchemaCommand>();
            services.AddTransient<ReplicateCommand>();
            services.AddTransient<CreateConfigCommand>();

            return services.BuildServiceProvider();
        }

        private static async Task<int> ExecuteCommand<TCommand, TOptions>(IServiceProvider serviceProvider, TOptions options)
            where TCommand : CommandBase<TOptions>
            where TOptions : BaseOptions
        {
            // Note: Logging level is configured when building the service provider
            // We can't change it dynamically after the provider is built

            // Get command instance
            var command = ActivatorUtilities.CreateInstance<TCommand>(serviceProvider, options);

            try
            {
                // Execute command
                return await command.ExecuteAsync();
            }
            catch (Exception ex)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Command execution failed: {Message}", ex.Message);
                return 1;
            }
        }
    }
}
