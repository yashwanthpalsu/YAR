# Logging Implementation Guide

This project uses **Serilog** as the primary logging framework with structured logging capabilities. The implementation provides comprehensive logging across multiple outputs with proper configuration for different environments.

## Features

### 🎯 **Structured Logging**
- JSON-formatted logs with structured data
- Property enrichment for better traceability
- Context-aware logging with correlation IDs

### 📊 **Multiple Output Sinks**
- **Console**: Real-time logging during development
- **File**: Daily rolling log files with size limits
- **Database**: SQL Server table for production monitoring
- **Custom**: Extensible for additional sinks (Elasticsearch, Seq, etc.)

### 🔧 **Environment-Specific Configuration**
- **Development**: Verbose logging with file and console output
- **Production**: Structured logging with database storage and file backup

## Configuration

### Production Configuration (`appsettings.json`)
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "Logs/app-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      },
      {
        "Name": "MSSqlServer",
        "Args": {
          "connectionString": "...",
          "tableName": "Logs",
          "autoCreateSqlTable": true
        }
      }
    ]
  }
}
```

### Development Configuration (`appsettings.Development.json`)
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "Microsoft.AspNetCore": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "Logs/app-dev-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ]
  }
}
```

## Usage

### 1. **Dependency Injection**
```csharp
public class MyService
{
    private readonly ILoggingService _loggingService;

    public MyService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }
}
```

### 2. **Basic Logging**
```csharp
// Information logging
_loggingService.LogInformation("User {UserId} logged in successfully", userId);

// Warning logging
_loggingService.LogWarning("High memory usage detected: {MemoryUsage}MB", memoryUsage);

// Error logging with exception
_loggingService.LogError(exception, "Failed to process payment for user {UserId}", userId);

// Debug logging
_loggingService.LogDebug("Processing item {ItemId} with data {ItemData}", itemId, itemData);
```

### 3. **Context-Aware Logging**
```csharp
using var logContext = _loggingService.PushProperty("UserId", userId)
    .Combine(_loggingService.PushProperty("RequestId", requestId));

_loggingService.LogInformation("Processing request for user");
// All subsequent logs in this scope will include UserId and RequestId
```

### 4. **Structured Data Logging**
```csharp
var reminder = new ReminderViewModel { UserId = 1, Name = "Meeting" };
_loggingService.LogInformation("Creating reminder {@ReminderData}", reminder);
```

## Database Logging

The application automatically creates a `Logs` table in the database with the following structure:

```sql
CREATE TABLE Logs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Message NVARCHAR(MAX),
    Level NVARCHAR(128),
    TimeStamp DATETIME2,
    Exception NVARCHAR(MAX),
    Properties NVARCHAR(MAX),
    LogEvent NVARCHAR(MAX),
    UserId INT NULL,
    ReminderId INT NULL,
    MachineName NVARCHAR(128),
    ThreadId INT,
    ProcessId INT
);
```

## Log Levels

| Level | Description | Usage |
|-------|-------------|-------|
| **Fatal** | Application crash | System failures, startup failures |
| **Error** | Error conditions | Exceptions, business logic failures |
| **Warning** | Warning conditions | Deprecated features, high resource usage |
| **Information** | General information | User actions, business events |
| **Debug** | Debug information | Method entry/exit, variable values |
| **Verbose** | Verbose information | Detailed debugging |

## Best Practices

### 1. **Use Structured Logging**
```csharp
// ✅ Good
_loggingService.LogInformation("User {UserId} created reminder {ReminderId}", userId, reminderId);

// ❌ Avoid
_loggingService.LogInformation($"User {userId} created reminder {reminderId}");
```

### 2. **Include Context**
```csharp
using var logContext = _loggingService.PushProperty("UserId", userId);
// All logs in this scope will include the UserId
```

### 3. **Log Exceptions Properly**
```csharp
try
{
    // Business logic
}
catch (Exception ex)
{
    _loggingService.LogError(ex, "Failed to process {Operation} for user {UserId}", operation, userId);
    throw; // Re-throw if needed
}
```

### 4. **Use Appropriate Log Levels**
```csharp
// Debug for detailed troubleshooting
_loggingService.LogDebug("Processing item {ItemId}", itemId);

// Information for business events
_loggingService.LogInformation("User {UserId} completed order {OrderId}", userId, orderId);

// Warning for potential issues
_loggingService.LogWarning("High CPU usage detected: {CpuUsage}%", cpuUsage);

// Error for actual problems
_loggingService.LogError(exception, "Database connection failed");
```

## Monitoring and Analysis

### 1. **File Logs**
- Location: `Logs/app-YYYY-MM-DD.log`
- Format: Structured JSON with timestamps
- Retention: 30 days (production), 7 days (development)

### 2. **Database Logs**
- Table: `Logs`
- Query for errors: `SELECT * FROM Logs WHERE Level = 'Error' ORDER BY TimeStamp DESC`
- Query for user activity: `SELECT * FROM Logs WHERE UserId = @userId ORDER BY TimeStamp DESC`

### 3. **Performance Monitoring**
The `RequestLoggingMiddleware` automatically logs:
- Request duration
- HTTP status codes
- Request paths and methods
- Client IP addresses

## Extending the Logging

### Adding New Sinks
1. Install the required NuGet package
2. Add sink configuration to `appsettings.json`
3. Update the `Using` array in Serilog configuration

### Custom Enrichers
```csharp
public class CustomEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var property = propertyFactory.CreateProperty("CustomProperty", "CustomValue");
        logEvent.AddPropertyIfAbsent(property);
    }
}
```

## Troubleshooting

### Common Issues

1. **Logs not appearing in database**
   - Check connection string
   - Verify table exists
   - Check SQL Server permissions

2. **File logs not created**
   - Ensure `Logs` directory exists
   - Check file permissions
   - Verify disk space

3. **Performance impact**
   - Use appropriate log levels
   - Avoid logging in tight loops
   - Consider async logging for high-volume scenarios

## Security Considerations

1. **Sensitive Data**: Never log passwords, tokens, or PII
2. **File Permissions**: Ensure log files are not publicly accessible
3. **Database Access**: Use appropriate database permissions for logging
4. **Log Rotation**: Implement proper log retention policies

## Performance Optimization

1. **Async Logging**: Use async logging methods when available
2. **Batching**: Configure sinks to batch writes
3. **Filtering**: Use log level filtering to reduce noise
4. **Sampling**: Implement sampling for high-volume scenarios 