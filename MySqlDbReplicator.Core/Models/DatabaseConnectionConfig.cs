using System;
using System.Collections.Generic;

namespace MySqlDbReplicator.Core.Models
{
    /// <summary>
    /// Configuration for a MySQL database connection
    /// </summary>
    public class DatabaseConnectionConfig
    {
        /// <summary>
        /// Server hostname or IP address
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// Server port
        /// </summary>
        public int Port { get; set; } = 3306;

        /// <summary>
        /// Database name
        /// </summary>
        public string Database { get; set; } = string.Empty;

        /// <summary>
        /// Username for authentication
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Password for authentication
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Connection timeout in seconds
        /// </summary>
        public int ConnectionTimeout { get; set; } = 30;

        /// <summary>
        /// Maximum number of connections in the pool
        /// </summary>
        public int MaxPoolSize { get; set; } = 100;

        /// <summary>
        /// Minimum number of connections in the pool
        /// </summary>
        public int MinPoolSize { get; set; } = 0;

        /// <summary>
        /// Whether to use SSL for the connection
        /// </summary>
        public bool UseSSL { get; set; } = false;

        /// <summary>
        /// Path to SSL certificate file
        /// </summary>
        public string SslCertPath { get; set; } = string.Empty;

        /// <summary>
        /// Path to SSL key file
        /// </summary>
        public string SslKeyPath { get; set; } = string.Empty;

        /// <summary>
        /// Path to SSL CA certificate file
        /// </summary>
        public string SslCaPath { get; set; } = string.Empty;

        /// <summary>
        /// Additional connection string parameters
        /// </summary>
        public Dictionary<string, string> AdditionalParameters { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Builds a connection string from the configuration
        /// </summary>
        /// <returns>MySQL connection string</returns>
        public string BuildConnectionString()
        {
            var connectionString = $"Server={Host};Port={Port};Database={Database};User ID={Username};Password={Password};";
            
            connectionString += $"Connect Timeout={ConnectionTimeout};";
            connectionString += $"MaximumPoolSize={MaxPoolSize};";
            connectionString += $"MinimumPoolSize={MinPoolSize};";
            
            if (UseSSL)
            {
                connectionString += "SslMode=Required;";
                
                if (!string.IsNullOrEmpty(SslCertPath))
                    connectionString += $"SslCert={SslCertPath};";
                
                if (!string.IsNullOrEmpty(SslKeyPath))
                    connectionString += $"SslKey={SslKeyPath};";
                
                if (!string.IsNullOrEmpty(SslCaPath))
                    connectionString += $"SslCa={SslCaPath};";
            }
            else
            {
                connectionString += "SslMode=None;";
            }
            
            foreach (var param in AdditionalParameters)
            {
                connectionString += $"{param.Key}={param.Value};";
            }
            
            return connectionString;
        }
    }
}
