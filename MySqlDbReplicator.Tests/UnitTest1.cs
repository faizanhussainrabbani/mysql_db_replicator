using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using MySqlDbReplicator.Core.Models;

namespace MySqlDbReplicator.Tests
{
    public class DatabaseConnectionConfigTests
    {
        [Fact]
        public void BuildConnectionString_ShouldIncludeAllParameters()
        {
            // Arrange
            var config = new DatabaseConnectionConfig
            {
                Host = "testhost",
                Port = 3307,
                Database = "testdb",
                Username = "testuser",
                Password = "testpass",
                ConnectionTimeout = 60,
                MaxPoolSize = 200,
                MinPoolSize = 10,
                UseSSL = true,
                SslCertPath = "/path/to/cert",
                SslKeyPath = "/path/to/key",
                SslCaPath = "/path/to/ca"
            };

            // Act
            var connectionString = config.BuildConnectionString();

            // Assert
            connectionString.Should().Contain("Server=testhost");
            connectionString.Should().Contain("Port=3307");
            connectionString.Should().Contain("Database=testdb");
            connectionString.Should().Contain("User ID=testuser");
            connectionString.Should().Contain("Password=testpass");
            connectionString.Should().Contain("Connect Timeout=60");
            connectionString.Should().Contain("MaximumPoolSize=200");
            connectionString.Should().Contain("MinimumPoolSize=10");
            connectionString.Should().Contain("SslMode=Required");
            connectionString.Should().Contain("SslCert=/path/to/cert");
            connectionString.Should().Contain("SslKey=/path/to/key");
            connectionString.Should().Contain("SslCa=/path/to/ca");
        }

        [Fact]
        public void BuildConnectionString_WithoutSSL_ShouldSetSslModeToNone()
        {
            // Arrange
            var config = new DatabaseConnectionConfig
            {
                Host = "testhost",
                Database = "testdb",
                Username = "testuser",
                Password = "testpass",
                UseSSL = false
            };

            // Act
            var connectionString = config.BuildConnectionString();

            // Assert
            connectionString.Should().Contain("SslMode=None");
            connectionString.Should().NotContain("SslCert=");
            connectionString.Should().NotContain("SslKey=");
            connectionString.Should().NotContain("SslCa=");
        }

        [Fact]
        public void BuildConnectionString_WithAdditionalParameters_ShouldIncludeThemInConnectionString()
        {
            // Arrange
            var config = new DatabaseConnectionConfig
            {
                Host = "testhost",
                Database = "testdb",
                Username = "testuser",
                Password = "testpass",
                AdditionalParameters = new Dictionary<string, string>
                {
                    { "AllowUserVariables", "true" },
                    { "UseCompression", "true" }
                }
            };

            // Act
            var connectionString = config.BuildConnectionString();

            // Assert
            connectionString.Should().Contain("AllowUserVariables=true");
            connectionString.Should().Contain("UseCompression=true");
        }
    }
}