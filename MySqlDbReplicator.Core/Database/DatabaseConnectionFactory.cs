using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using MySqlDbReplicator.Core.Models;

namespace MySqlDbReplicator.Core.Database
{
    /// <summary>
    /// Factory for creating and managing database connections
    /// </summary>
    public class DatabaseConnectionFactory
    {
        private readonly ILogger<DatabaseConnectionFactory> _logger;

        /// <summary>
        /// Initializes a new instance of the DatabaseConnectionFactory class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public DatabaseConnectionFactory(ILogger<DatabaseConnectionFactory> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new database connection
        /// </summary>
        /// <param name="config">Database connection configuration</param>
        /// <returns>MySqlConnection instance</returns>
        public MySqlConnection CreateConnection(DatabaseConnectionConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var connectionString = config.BuildConnectionString();
            return new MySqlConnection(connectionString);
        }

        /// <summary>
        /// Tests a database connection
        /// </summary>
        /// <param name="config">Database connection configuration</param>
        /// <returns>True if connection is successful, false otherwise</returns>
        public async Task<bool> TestConnectionAsync(DatabaseConnectionConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            try
            {
                using var connection = CreateConnection(config);
                await connection.OpenAsync();
                _logger.LogInformation("Successfully connected to database {Database} on {Host}:{Port}", 
                    config.Database, config.Host, config.Port);
                return true;
            }
            catch (MySqlException ex)
            {
                _logger.LogError(ex, "Failed to connect to database {Database} on {Host}:{Port}: {Message}", 
                    config.Database, config.Host, config.Port, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Gets database server version
        /// </summary>
        /// <param name="config">Database connection configuration</param>
        /// <returns>Server version string</returns>
        public async Task<string> GetServerVersionAsync(DatabaseConnectionConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            try
            {
                using var connection = CreateConnection(config);
                await connection.OpenAsync();
                return connection.ServerVersion;
            }
            catch (MySqlException ex)
            {
                _logger.LogError(ex, "Failed to get server version for database {Database} on {Host}:{Port}: {Message}", 
                    config.Database, config.Host, config.Port, ex.Message);
                throw;
            }
        }
    }
}
