using System;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlDbReplicator.Core.Models;

namespace MySqlDbReplicator.Core.Security
{
    /// <summary>
    /// Provides secure access to database credentials
    /// </summary>
    public class SecureCredentialProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SecureCredentialProvider> _logger;
        private const string ENV_PREFIX = "MYSQL_REPLICATOR_";

        /// <summary>
        /// Initializes a new instance of the SecureCredentialProvider class
        /// </summary>
        /// <param name="configuration">Configuration instance</param>
        /// <param name="logger">Logger instance</param>
        public SecureCredentialProvider(IConfiguration configuration, ILogger<SecureCredentialProvider> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Securely retrieves database credentials from various sources
        /// </summary>
        /// <param name="config">Database connection configuration</param>
        /// <param name="connectionType">Type of connection (source/target)</param>
        /// <returns>Updated database connection configuration with secure credentials</returns>
        public DatabaseConnectionConfig GetSecureCredentials(DatabaseConnectionConfig config, string connectionType)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrEmpty(connectionType))
                throw new ArgumentException("Connection type must be specified", nameof(connectionType));

            // Create a copy of the config to avoid modifying the original
            var secureConfig = new DatabaseConnectionConfig
            {
                Host = config.Host,
                Port = config.Port,
                Database = config.Database,
                ConnectionTimeout = config.ConnectionTimeout,
                MaxPoolSize = config.MaxPoolSize,
                MinPoolSize = config.MinPoolSize,
                UseSSL = config.UseSSL,
                SslCertPath = config.SslCertPath,
                SslKeyPath = config.SslKeyPath,
                SslCaPath = config.SslCaPath,
                AdditionalParameters = new System.Collections.Generic.Dictionary<string, string>(config.AdditionalParameters)
            };

            // Try to get username from environment variables or user secrets
            string usernameKey = $"{connectionType}:username";
            string envUsernameKey = $"{ENV_PREFIX}{connectionType.ToUpperInvariant()}_USERNAME";

            secureConfig.Username = _configuration[usernameKey] ??
                                   Environment.GetEnvironmentVariable(envUsernameKey) ??
                                   config.Username;

            // Try to get password from environment variables or user secrets
            string passwordKey = $"{connectionType}:password";
            string envPasswordKey = $"{ENV_PREFIX}{connectionType.ToUpperInvariant()}_PASSWORD";

            secureConfig.Password = _configuration[passwordKey] ??
                                   Environment.GetEnvironmentVariable(envPasswordKey) ??
                                   config.Password;

            // Also check for source/target specific environment variables
            if (connectionType.Equals("database", StringComparison.OrdinalIgnoreCase))
            {
                // Check if this is actually a source or target connection based on the database name
                if (config.Database.Contains("source", StringComparison.OrdinalIgnoreCase))
                {
                    secureConfig.Username = _configuration["source:username"] ??
                                          Environment.GetEnvironmentVariable($"{ENV_PREFIX}SOURCE_USERNAME") ??
                                          secureConfig.Username;

                    secureConfig.Password = _configuration["source:password"] ??
                                          Environment.GetEnvironmentVariable($"{ENV_PREFIX}SOURCE_PASSWORD") ??
                                          secureConfig.Password;
                }
                else if (config.Database.Contains("target", StringComparison.OrdinalIgnoreCase))
                {
                    secureConfig.Username = _configuration["target:username"] ??
                                          Environment.GetEnvironmentVariable($"{ENV_PREFIX}TARGET_USERNAME") ??
                                          secureConfig.Username;

                    secureConfig.Password = _configuration["target:password"] ??
                                          Environment.GetEnvironmentVariable($"{ENV_PREFIX}TARGET_PASSWORD") ??
                                          secureConfig.Password;
                }
            }

            // Log warning if credentials are still empty
            if (string.IsNullOrEmpty(secureConfig.Username) || string.IsNullOrEmpty(secureConfig.Password))
            {
                _logger.LogWarning("Credentials for {ConnectionType} database are not set or empty", connectionType);
            }

            return secureConfig;
        }

        /// <summary>
        /// Masks sensitive information in a connection string for logging purposes
        /// </summary>
        /// <param name="connectionString">The connection string to mask</param>
        /// <returns>Masked connection string</returns>
        public static string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return connectionString;

            // Mask password
            var maskedString = System.Text.RegularExpressions.Regex.Replace(
                connectionString,
                @"Password=([^;]*)",
                "Password=********",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Mask user secrets if present
            maskedString = System.Text.RegularExpressions.Regex.Replace(
                maskedString,
                @"User ID=([^;]*)",
                match => {
                    var username = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(username))
                    {
                        return $"User ID={username[0]}****";
                    }
                    return match.Value;
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return maskedString;
        }
    }
}
