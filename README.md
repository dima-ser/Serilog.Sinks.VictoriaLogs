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
Like other Serilog sinks, `VictoriaLogsHttp` can be configured via configuration file, in code or combining both. Below is an example of the minimum working configuration. 
In your `appsettings.json`, add the `Serilog` section (must be in the root):

```json
{
  ...,
  "Serilog": {
    "Using":  [ "Serilog.Sinks.Console", "Serilog.Sinks.VictoriaLogs" ],
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "VictoriaLogsHttp", "Args": { "victoriaLogsEndpoint": "http://localhost:9428/insert/jsonline"} }
    ],
    "Enrich": [ "WithMachineName" ],
    "Properties": {
        "Application": "AppName"
    }
  }
}
```
Replace `victoriaLogsEndpoint` and `Application` with your Victoria Logs JSON Stream API endpoint and your application name accordingly. You can also add additional settings as per [Serilog documentation](https://github.com/serilog/serilog-settings-configuration). Similarly, you can add or override arguments to `VictoriaLogsHttp:Args` as per [Serilog.Sinks.Http documenation](https://github.com/FantasticFiasco/serilog-sinks-http). Below is the description of arguments specific to `VictoriaLogsHttp` sink only. 

| Parameter | Description |
|----------|--------|
| **victoriaLogsEndpoint** | Required. URL to VictoriaLogs [HTTP JSON Stream API](https://docs.victoriametrics.com/victorialogs/data-ingestion/#json-stream-api). |
| **lowerCasePropertyKeys** | Optional. Whether to convert log event property keys to lower case to conform to VictoriaLogs convention. Default value: true |
| **streamFields** | Optional. Comma-separated field names that consitute a [stream](https://docs.victoriametrics.com/victorialogs/keyconcepts/#stream-fields) in VictoriaLogs. Default value: `MachineName,Application` |
| **overridePropertyKeys** | Optional. A Dictionary with property keys/log field names to override. The key is the original name, and the value is the new name. For example, if you want to override the property key `Application` to `app`, you would specify a dictionary with a single entry: `{ "Application", "app" }`. Default value: `null` |


### Register the logging provider
In your `Program.cs`
```c#
using Serilog;
using Serilog.Sinks.VictoriaLogs

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) =>
  {   
      configuration.ReadFrom.Configuration(context.Configuration);
  });
```
You are done, the application should now log everything to VictoriaLogs.

If you wish to enrich the logs with context-specific fields (RemoteIp, RequestMethod, UserAgent, etc), add `VictoriaLogsEnricher`. This can only be configured in code:
```c#
builder.Services.AddHttpContextAccessor();
builder.Host.UseSerilog((context, services, configuration) =>
  {   var accessor = services.GetRequiredService<IHttpContextAccessor>();
      configuration.ReadFrom.Configuration(context.Configuration)
      .Enrich.With(new VictoriaLogsEnricher(accessor));
  });
```


