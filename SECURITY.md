# Security Best Practices for MySQL DB Replicator

This document outlines security best practices for using the MySQL DB Replicator tool. Following these guidelines will help ensure that your database credentials and sensitive information remain secure.

## Credential Management

### DO NOT store credentials in configuration files

Never store actual database credentials in configuration files that might be committed to version control. Instead:

1. Use environment variables:
   ```bash
   # For source database
   export MYSQL_REPLICATOR_SOURCE_USERNAME=your_username
   export MYSQL_REPLICATOR_SOURCE_PASSWORD=your_password
   
   # For target database
   export MYSQL_REPLICATOR_TARGET_USERNAME=your_username
   export MYSQL_REPLICATOR_TARGET_PASSWORD=your_password
   ```

2. Use .NET User Secrets for development:
   ```bash
   # Initialize user secrets
   dotnet user-secrets init --project MySqlDbReplicator.Cli
   
   # Set secrets
   dotnet user-secrets set "source:username" "your_username" --project MySqlDbReplicator.Cli
   dotnet user-secrets set "source:password" "your_password" --project MySqlDbReplicator.Cli
   dotnet user-secrets set "target:username" "your_username" --project MySqlDbReplicator.Cli
   dotnet user-secrets set "target:password" "your_password" --project MySqlDbReplicator.Cli
   ```

3. For production, consider using a secure vault solution like:
   - Azure Key Vault
   - HashiCorp Vault
   - AWS Secrets Manager

### Use dedicated database users

1. Create dedicated database users with only the necessary permissions
2. Never use the `root` user for database operations
3. Follow the principle of least privilege

Example MySQL commands to create a dedicated replication user:

```sql
-- For source database (read-only access)
CREATE USER 'repl_source'@'%' IDENTIFIED BY 'strong_password';
GRANT SELECT, SHOW VIEW, PROCESS, REPLICATION CLIENT ON *.* TO 'repl_source'@'%';
FLUSH PRIVILEGES;

-- For target database (needs write access)
CREATE USER 'repl_target'@'%' IDENTIFIED BY 'different_strong_password';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, INDEX, REFERENCES ON target_db.* TO 'repl_target'@'%';
FLUSH PRIVILEGES;
```

## Connection Security

### Always use SSL/TLS

1. Enable SSL/TLS for all database connections
2. Configure proper certificate validation
3. Ensure database servers require encrypted connections

In your configuration:

```yaml
source:
  useSSL: true
  sslCertPath: '/path/to/cert.pem'  # If using client certificates
  sslKeyPath: '/path/to/key.pem'    # If using client certificates
  sslCaPath: '/path/to/ca.pem'      # CA certificate for server validation
```

### Secure connection strings

The application now automatically masks connection strings in logs, but be careful not to expose them in:

1. Error messages
2. Application logs
3. Console output
4. Debugging information

## Configuration File Security

### .gitignore

Ensure your `.gitignore` file includes:

```
# Configuration files with sensitive information
*.yml
*.yaml
*.config
appsettings.*.json
appsettings.json
*secrets*.json

# Allow template files
!config.template.yml
```

### Template files

Use template files without real credentials:

```yaml
source:
  username: '' # DO NOT use 'root' in production
  password: '' # Store passwords securely, not in this file
```

## Audit and Monitoring

1. Regularly review database access logs
2. Monitor for unusual database access patterns
3. Implement alerts for failed authentication attempts
4. Rotate credentials regularly

## Error Handling

The application has been updated to prevent sensitive information leakage in error messages. However, you should:

1. Review application logs regularly
2. Set appropriate log levels (use Debug only when necessary)
3. Ensure logs are stored securely and rotated regularly

## Additional Security Measures

1. Use network security groups or firewalls to restrict database access
2. Implement IP allowlisting for database connections
3. Consider using SSH tunneling for additional security
4. Keep the application and all dependencies updated

## Reporting Security Issues

If you discover a security vulnerability in MySQL DB Replicator, please report it responsibly by contacting the maintainers directly rather than creating a public issue.
