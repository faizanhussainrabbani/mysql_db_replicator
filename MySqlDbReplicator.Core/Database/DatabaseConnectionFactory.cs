using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using MySql.Data.MySqlClient;
using MySqlDbReplicator.Core.Models;
using MySqlDbReplicator.Core.Security;

namespace MySqlDbReplicator.Core.Database
{
    /// <summary>
    /// Factory for creating and managing database connections
    /// </summary>
    public class DatabaseConnectionFactory
    {
        private readonly ILogger<DatabaseConnectionFactory> _logger;
        private readonly SecureCredentialProvider _credentialProvider;

        /// <summary>
        /// Initializes a new instance of the DatabaseConnectionFactory class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="configuration">Configuration instance</param>
        public DatabaseConnectionFactory(ILogger<DatabaseConnectionFactory> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Create a logger factory to get the correct logger type
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var credentialLogger = loggerFactory.CreateLogger<SecureCredentialProvider>();

            _credentialProvider = new SecureCredentialProvider(
                configuration ?? throw new ArgumentNullException(nameof(configuration)),
                credentialLogger);
        }

        /// <summary>
        /// Creates a new database connection
        /// </summary>
        /// <param name="config">Database connection configuration</param>
        /// <param name="connectionType">Type of connection (source/target)</param>
        /// <returns>MySqlConnection instance</returns>
        public MySqlConnection CreateConnection(DatabaseConnectionConfig config, string connectionType = "database")
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // Get secure credentials
            var secureConfig = _credentialProvider.GetSecureCredentials(config, connectionType);

            // Build connection string
            var connectionString = secureConfig.BuildConnectionString();

            // Log masked connection string for debugging
            _logger.LogDebug("Creating connection to {Database} with connection string: {ConnectionString}",
                secureConfig.Database, SecureCredentialProvider.MaskConnectionString(connectionString));

            return new MySqlConnection(connectionString);
        }

        /// <summary>
        /// Tests a database connection
        /// </summary>
        /// <param name="config">Database connection configuration</param>
        /// <param name="connectionType">Type of connection (source/target)</param>
        /// <returns>True if connection is successful, false otherwise</returns>
        public async Task<bool> TestConnectionAsync(DatabaseConnectionConfig config, string connectionType = "database")
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            try
            {
                using var connection = CreateConnection(config, connectionType);
                await connection.OpenAsync();
                _logger.LogInformation("Successfully connected to database {Database} on {Host}:{Port}",
                    config.Database, config.Host, config.Port);
                return true;
            }
            catch (MySqlException ex)
            {
                // Log error without exposing sensitive details
                _logger.LogError("Failed to connect to database {Database} on {Host}:{Port}: {ErrorType}",
                    config.Database, config.Host, config.Port, ex.GetType().Name);

                // Log detailed error at debug level
                _logger.LogDebug(ex, "Detailed connection error: {Message}", ex.Message);

                return false;
            }
        }

        /// <summary>
        /// Gets database server version
        /// </summary>
        /// <param name="config">Database connection configuration</param>
        /// <param name="connectionType">Type of connection (source/target)</param>
        /// <returns>Server version string</returns>
        public async Task<string> GetServerVersionAsync(DatabaseConnectionConfig config, string connectionType = "database")
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            try
            {
                using var connection = CreateConnection(config, connectionType);
                await connection.OpenAsync();
                return connection.ServerVersion;
            }
            catch (MySqlException ex)
            {
                // Log error without exposing sensitive details
                _logger.LogError("Failed to get server version for database {Database} on {Host}:{Port}: {ErrorType}",
                    config.Database, config.Host, config.Port, ex.GetType().Name);

                // Log detailed error at debug level
                _logger.LogDebug(ex, "Detailed server version error: {Message}", ex.Message);

                throw;
            }
        }
    }
}
