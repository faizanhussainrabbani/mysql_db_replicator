# MySQL Database Replicator

A comprehensive utility tool for MySQL database replication that automatically synchronizes both schema and data from a source database to a target database.

## Features

- **Connection Management**
  - Secure connection to both source and target MySQL databases
  - Authentication with username/password and SSL options
  - Connection pooling for optimized performance
  - Configurable connection timeout parameters

- **Schema Synchronization**
  - Automatic comparison of source and target database schemas
  - Generation of DDL scripts for missing tables, views, stored procedures, and functions
  - Synchronization of table structures including columns, data types, and constraints
  - Handling of database object dependencies (foreign keys, views, triggers)
  - Preview of schema changes before execution
  - Schema versioning and migration tracking

- **Data Replication**
  - Both full and incremental data replication modes
  - Batch processing with configurable batch sizes
  - Transaction management to ensure data consistency
  - Error handling with retry mechanisms for failed transfers
  - Conflict resolution strategies for data merge conflicts
  - Performance optimization for large data transfers

- **Configuration Options**
  - YAML/JSON based configuration file support
  - Command-line parameter override capability
  - Environment variable integration
  - Inclusion/exclusion patterns for tables and schemas
  - Configurable logging levels

- **Monitoring and Reporting**
  - Detailed logging of all operations
  - Progress tracking with percentage complete
  - Performance metrics collection (rows/second, total bytes transferred)
  - Summary reports showing success/failure statistics
  - Email notifications for job completion or errors

- **Security Features**
  - Support for encrypted connections
  - Secure credential management
  - Data masking options for sensitive fields

## Installation

### Prerequisites

- .NET 8.0 or later
- MySQL 8.0 or later

### Building from Source

1. Clone the repository:
   ```
   git clone https://github.com/yourusername/mysql-db-replicator.git
   cd mysql-db-replicator
   ```

2. Build the solution:
   ```
   dotnet build
   ```

3. Run the tests:
   ```
   dotnet test
   ```

4. Publish the application:
   ```
   dotnet publish -c Release -o ./publish
   ```

## Usage

### Command Line Interface

The utility provides several commands for different operations:

#### Initialize a Configuration File

```bash
dotnet run --project MySqlDbReplicator.Cli/MySqlDbReplicator.Cli.csproj -- init -o config.yml
```

#### Create a Configuration File Interactively

```bash
dotnet run --project MySqlDbReplicator.Cli/MySqlDbReplicator.Cli.csproj -- create-config -o config.yml
```

#### Validate Configuration and Database Connections

```bash
dotnet run --project MySqlDbReplicator.Cli/MySqlDbReplicator.Cli.csproj -- validate -c config.yml
```

#### Compare Source and Target Database Schemas

```bash
dotnet run --project MySqlDbReplicator.Cli/MySqlDbReplicator.Cli.csproj -- compare -c config.yml -o comparison-report.md
```

#### Synchronize Schema from Source to Target

Preview mode (generates script without executing):
```bash
dotnet run --project MySqlDbReplicator.Cli/MySqlDbReplicator.Cli.csproj -- sync-schema -c config.yml -p -o sync-script.sql
```

Execute schema synchronization:
```bash
dotnet run --project MySqlDbReplicator.Cli/MySqlDbReplicator.Cli.csproj -- sync-schema -c config.yml
```

Use `-p` or `--preview` to preview changes without executing them.

#### Replicate Data from Source to Target

```bash
dotnet run --project MySqlDbReplicator.Cli/MySqlDbReplicator.Cli.csproj -- replicate -c config.yml -m full -b 1000 -r report.md
```

After publishing the application, you can use the simpler form:
```bash
dotnet MySqlDbReplicator.Cli.dll replicate -c config.yml
```

Options:
- `-s` or `--skip-schema`: Skip schema synchronization
- `-m` or `--mode`: Replication mode (full or incremental)
- `-b` or `--batch-size`: Batch size for data replication
- `-i` or `--include`: Tables to include (comma-separated)
- `-e` or `--exclude`: Tables to exclude (comma-separated)
- `-p` or `--parallel`: Number of parallel threads
- `-r` or `--report`: Output file for replication report

### Configuration File

The configuration file can be in YAML or JSON format. Here's an example YAML configuration:

```yaml
# Source database connection
source:
  host: localhost
  port: 3306
  database: source_db
  username: root
  password: password
  useSSL: false

# Target database connection
target:
  host: localhost
  port: 3306
  database: target_db
  username: root
  password: password
  useSSL: false

# Replication settings
mode: Full
syncSchema: true
batchSize: 1000
parallelThreads: 1

# Tables to include/exclude
includeTables: []
excludeTables: []

# Data masking rules
dataMaskingRules:
  - table: users
    column: password
    type: FullMask
```

See the `config.template.yml` file for a complete example with all available options.

**Important:** Never commit configuration files with sensitive information to version control. The repository includes a `.gitignore` file that excludes configuration files (*.yml, *.yaml, *.json) to prevent accidental exposure of database credentials.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Acknowledgements

- [MySql.Data](https://www.nuget.org/packages/MySql.Data/) - Official MySQL client library for .NET
- [CommandLineParser](https://www.nuget.org/packages/CommandLineParser/) - Command line parsing library
- [YamlDotNet](https://www.nuget.org/packages/YamlDotNet/) - YAML parser and serializer for .NET
