# Serilog.Sinks.VictoriaLogs
Allows logging from your .NET app directly to [VictoriaLogs](https://docs.victoriametrics.com/victorialogs/) using [JSON Stream API](https://docs.victoriametrics.com/victorialogs/data-ingestion/#json-stream-api). Depends on [Serilog](https://github.com/serilog/serilog-aspnetcore) and [Serilog.Sinks.Http](https://github.com/FantasticFiasco/serilog-sinks-http). 
## Requirements
.NET 8.0+
## How to use
### Reference in your project

```
 dotnet add package Serilog.Sinks.VictoriaLogs
```
For ASP.NET Core web applications, you may also want to add `Serilog.AspNetCore` if you want to set Serilog as your default logging provider:
```
 dotnet add package Serilog.AspNetCore
```


### Configure
Like other Serilog sinks, `VictoriaLogsHttp` can be configured via configuration file, in code or combining both. Below is an example of the minimum working configuration. Note that the instructions below are for ASP.NET Core web applications. Refer to [Serilog wiki](https://github.com/serilog/serilog/wiki/Getting-Started) for other application types, such as console application.
In your `appsettings.json`, add the `Serilog` section (must be in the root):

```json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.VictoriaLogs" ],
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "VictoriaLogsHttp",
        "Args": {
          "victoriaLogsEndpoint": "http://localhost:9428/insert/jsonline",
          "restrictedToMinimumLevel": "Error"
        }
      }
    ]
  }
}
```
Replace `victoriaLogsEndpoint` with your Victoria Logs JSON Stream API endpoint and `restrictedToMinimumLevel` with whatever log level you wish to be logged to VictoriaLogs. You can add additional parameters to `VictoriaLogsHttp:Args` as per [Serilog.Sinks.Http documenation](https://github.com/FantasticFiasco/serilog-sinks-http). Below is the description of parameters specific to `VictoriaLogsHttp` sink only. 

| Parameter | Description |
|----------|--------|
| **victoriaLogsEndpoint** | Required. URL to VictoriaLogs [HTTP JSON Stream API](https://docs.victoriametrics.com/victorialogs/data-ingestion/#json-stream-api). |
| **lowerCasePropertyKeys** | Optional. Whether to convert log field names to lower case to conform to VictoriaLogs convention. Default value: true |
| **streamFields** | Optional. Comma-separated field names that consitute a [stream](https://docs.victoriametrics.com/victorialogs/keyconcepts/#stream-fields) in VictoriaLogs. By default, this is using `hostname,app_name` which are injected automatically by the sink. `Environment.MachineName` is used for `hostname` and current assembly name is used for `app_name`. You can override this by providing your own field names to be used as stream fields. |


### Register the logging provider
In your `Program.cs`
```c#
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) =>
{   
    configuration.ReadFrom.Configuration(context.Configuration);
});
```
You are done, the application should now send all logs at or above your specified minimum level to VictoriaLogs. 

If you wish to enrich the logs with context-specific fields you can add any Serilog enrichers by referencing an appropriate enricher package such as `Serilog.Enrichers.Environment` or `Serilog.Enrichers.ClientInfo` and adding the enrichers in code or configuration. For example:
```c#
builder.Services.AddHttpContextAccessor();
builder.Host.UseSerilog((context, services, configuration) =>
{   
    configuration.ReadFrom.Configuration(context.Configuration)
    .Enrich.WithEnvironmentName()
    .Enrich.WithEnvironmentUserName()
    .Enrich.WithClientIp()
    .Enrich.WithUserClaims();
    });
```
Other Serilog enrichers can be applied as well as per https://github.com/serilog/serilog/wiki/Enrichment 

For separate configuration per environment, you can either put your environment-specific settings in `appsettings.[Environment].json` or preface `UseSerilog()` call with environment check:
```c#
if (!builder.Environment.IsDevelopment())
{
  ...
}
```


