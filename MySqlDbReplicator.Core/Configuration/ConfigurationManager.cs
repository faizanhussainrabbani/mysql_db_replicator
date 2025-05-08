using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using MySqlDbReplicator.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MySqlDbReplicator.Core.Configuration
{
    /// <summary>
    /// Manages configuration loading and parsing from various sources
    /// </summary>
    public class ConfigurationManager
    {
        private readonly IConfiguration _configuration;
        private const string ENV_PREFIX = "MYSQL_REPLICATOR_";

        /// <summary>
        /// Initializes a new instance of the ConfigurationManager class
        /// </summary>
        /// <param name="configFilePath">Path to the configuration file</param>
        /// <param name="commandLineArgs">Command line arguments</param>
        /// <param name="useUserSecrets">Whether to use user secrets (for development)</param>
        /// <param name="userSecretsId">User secrets ID (optional)</param>
        public ConfigurationManager(string configFilePath, string[] commandLineArgs = null,
            bool useUserSecrets = false, string userSecretsId = "mysql-db-replicator")
        {
            var builder = new ConfigurationBuilder()
                // Environment variables have higher priority than config files
                .AddEnvironmentVariables(ENV_PREFIX);

            // Add configuration file if provided
            if (!string.IsNullOrEmpty(configFilePath) && File.Exists(configFilePath))
            {
                var extension = Path.GetExtension(configFilePath).ToLowerInvariant();

                if (extension == ".json")
                {
                    builder.AddJsonFile(configFilePath, optional: false, reloadOnChange: true);
                }
                else if (extension == ".yml" || extension == ".yaml")
                {
                    // YAML files need to be parsed manually and added as memory source
                    var yamlString = File.ReadAllText(configFilePath);
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();

                    var yamlObject = deserializer.Deserialize<object>(yamlString);
                    var jsonString = System.Text.Json.JsonSerializer.Serialize(yamlObject);

                    builder.AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonString)));
                }
                else
                {
                    throw new ArgumentException($"Unsupported configuration file format: {extension}");
                }
            }

            // Add user secrets for development environments
            // Note: This requires Microsoft.Extensions.Configuration.UserSecrets package
            // Commented out until the package is added to the project
            /*
            if (useUserSecrets && !string.IsNullOrEmpty(userSecretsId))
            {
                builder.AddUserSecrets(userSecretsId);
            }
            */

            // Command line args have highest priority
            if (commandLineArgs != null)
            {
                builder.AddCommandLine(commandLineArgs);
            }

            _configuration = builder.Build();
        }

        /// <summary>
        /// Loads the replication configuration from the configuration sources
        /// </summary>
        /// <returns>The replication configuration</returns>
        public ReplicationConfig LoadReplicationConfig()
        {
            var config = new ReplicationConfig();
            _configuration.Bind(config);
            return config;
        }

        /// <summary>
        /// Saves the replication configuration to a file
        /// </summary>
        /// <param name="config">The replication configuration to save</param>
        /// <param name="filePath">The file path to save to</param>
        public void SaveReplicationConfig(ReplicationConfig config, string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension == ".json")
            {
                var jsonString = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(filePath, jsonString);
            }
            else if (extension == ".yml" || extension == ".yaml")
            {
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                var yamlString = serializer.Serialize(config);
                File.WriteAllText(filePath, yamlString);
            }
            else
            {
                throw new ArgumentException($"Unsupported configuration file format: {extension}");
            }
        }

        /// <summary>
        /// Creates a default configuration file
        /// </summary>
        /// <param name="filePath">The file path to save to</param>
        public void CreateDefaultConfigFile(string filePath)
        {
            var config = new ReplicationConfig
            {
                Source = new DatabaseConnectionConfig
                {
                    Host = "localhost",
                    Port = 3306,
                    Database = "source_db",
                    Username = string.Empty, // Do not use default credentials
                    Password = string.Empty, // Do not use default credentials
                    UseSSL = true // Enable SSL by default for security
                },
                Target = new DatabaseConnectionConfig
                {
                    Host = "localhost",
                    Port = 3306,
                    Database = "target_db",
                    Username = string.Empty, // Do not use default credentials
                    Password = string.Empty, // Do not use default credentials
                    UseSSL = true // Enable SSL by default for security
                },
                Mode = ReplicationMode.Full,
                SyncSchema = true,
                BatchSize = 1000,
                MaxRetryAttempts = 3
            };

            SaveReplicationConfig(config, filePath);
        }
    }
}
