# Serilog.Sinks.VictoriaLogs
Allows logging from your .NET app directly to [VictoriaLogs](https://docs.victoriametrics.com/victorialogs/) using [JSON Stream API](https://docs.victoriametrics.com/victorialogs/data-ingestion/#json-stream-api). Depends on [Serilog](https://github.com/serilog/serilog-aspnetcore) and 
[Serilog.Sinks.Http](https://github.com/FantasticFiasco/serilog-sinks-http). 
## Requirements
.NET 10.0+
## How to use
### Reference in your project

If adding from nuget.org:

```
 dotnet add package Serilog.Sinks.VictoriaLogs
```

If building from source, clone the repo and build the project. Create a local nuget package source, then publish the .nupkg to your local package store. Then add it to your project:

```
dotnet add package Serilog.Sinks.VictoriaLogs --source [/your-local/package-source]
```

### Configure
In your `appsettings.json`, add section called `VictoriaLogs`:

```json
{
  ...,
  "VictoriaLogs": {
    "Endpoint": "http://localhost:9428/insert/jsonline",
    "AppName": "MyApp",
    "LogLevel": "Warning"
  }
}
```
| Config parameter | Description |
|----------|--------|
| **Endpoint** | Required. URL to VictoriaLogs [HTTP JSON Stream API](https://docs.victoriametrics.com/victorialogs/data-ingestion/#json-stream-api). |
| **AppName** | Optional. Overrides the `app_name` field that gets passed to VictoriaLogs. If not provided, `builder.Environment.ApplicationName` will be used. |
| **LogLevel** | Optional. Minimum [log event level](https://github.com/serilog/serilog/blob/dev/src/Serilog/Events/LogEventLevel.cs) to log to VictoriaLogs. Default value: Information. See important note [below](#seriloglevel) |


### Register the logging provider
In your `Program.cs`
```c#
using Serilog.Sinks.VictoriaLogs

var builder = WebApplication.CreateBuilder(args);
builder.AddVictoriaLogs();
```

The application will now log everything to VictoriaLogs. 

## <a name="seriloglevel"></a>Logging below `Information` level
Note that if you want to log more verbose (e.g., Debug) you will need to override Serilog's global log level as well. Add this to your configuration:

```json
{
  ...,
  "Serilog": {
    "MinimumLevel": { "Default": "Debug" }
  }
}
```

## Log fields
The library will log the following fields to VictoriaLogs.  

### General fields
These fields will be present in all logs. `_time` and `_msg`  are required by VictoriaLogs.

| Field name  | Description |
| ----------- | ----------- |
| _time       | Log timestamp in UTC. |
| _msg        | Log message. In case of exceptions, this will contain `Exception.Message` |
| _stream     | Combination of `hostname` and `app_name` to uniquely identify application as per [VictoriaLogs docs](https://docs.victoriametrics.com/victorialogs/keyconcepts/#stream-fields). This reduces disk space usage and improves search performance in VictoriaLogs.
| level       | Log level  as defined in https://github.com/serilog/serilog/blob/dev/src/Serilog/Events/LogEventLevel.cs |
| hostname    | Machine name from `Environment.MachineName` |
| app_name    | `AppName` as specified in configuration or `Environment.ApplicationName` |

### Optional fields
These fields may or may not be present in the log depending on the type of log
| Field name  | Description |
| ----------- | ----------- |
| url         | Full URL of the current request, including query string |
| method      | Current request HTTP method |
| remote_ip   | IP address of the current request |
| user_id     | Currently authenticated user  |
| user_agent  | User agent of the current request |
| exception     | Exception stack trace   |
| exception_type | Exception type |