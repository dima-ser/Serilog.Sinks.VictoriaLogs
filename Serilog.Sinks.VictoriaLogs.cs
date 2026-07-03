using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.Http.HttpClients;
using System.Text.Json;

namespace Serilog.Sinks.VictoriaLogs
{
    public sealed class VictoriaLogFormatter : ITextFormatter
    {
        string hostname = "";
        string app_name = "";
        private readonly IHttpContextAccessor contextAccessor;
        public VictoriaLogFormatter(string hostname, string app_name, IHttpContextAccessor contextAccessor)
        {
            this.hostname = hostname;
            this.app_name = app_name;
            this.contextAccessor = contextAccessor;
        }

        public void Format(LogEvent logEvent, TextWriter output)
        {
            var context = contextAccessor.HttpContext;
            var request = context?.Request;
            var record = new Dictionary<string, object?>
            {
                ["_msg"] = logEvent.Exception != null ? logEvent.Exception.Message : logEvent.RenderMessage(),
                ["level"] = logEvent.Level.ToString().ToLower(),
                ["_time"] = logEvent.Timestamp.UtcDateTime.ToString("O"),
                ["hostname"] = hostname,
                ["app_name"] = app_name,
                ["url"] = request != null ? Microsoft.AspNetCore.Http.Extensions.UriHelper.GetDisplayUrl(request) : null,
                ["method"] = request?.Method,
                ["remote_ip"] = context?.Connection?.RemoteIpAddress?.ToString(),
                ["user_id"] = context?.User?.Identity?.IsAuthenticated == true ? context.User.Identity.Name : null,
                ["user_agent"] = request?.Headers["User-Agent"].ToString(),
            };


            if (logEvent.Exception != null)
            {
                record["exception"] = logEvent.Exception.ToString();
                record["exception_type"] = logEvent.Exception.GetType().FullName;
            }
            output.WriteLine(JsonSerializer.Serialize(record));
        }

    }

    public sealed class VictoriaLogBatchFormatter : Serilog.Sinks.Http.IBatchFormatter
    {
        public void Format(IEnumerable<string> logEvents, TextWriter output)
        {
            if (logEvents == null)
            {
                throw new ArgumentNullException(nameof(logEvents));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (!logEvents.Any())
            {
                return;
            }

            string separator = string.Empty;
            foreach (string logEvent in logEvents)
            {
                if (!string.IsNullOrWhiteSpace(logEvent))
                {
                    output.Write(separator);
                    output.Write(logEvent);
                    separator = "\n";
                }
            }
        }
    }


    public sealed class VictoriaLogsHttpClient : JsonHttpClient
    {
        public VictoriaLogsHttpClient()
            : base(CreateHttpClient())
        {
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();

            client.DefaultRequestHeaders.Add("VL-Stream-Fields", "hostname,app_name");

            return client;
        }
    }

    public class VictoriaLogsOptions
    {
        public const string CONFIG_NAME = "VictoriaLogs";
        public string Endpoint { get; set; } = string.Empty;
        public string AppName { get; set; } = string.Empty;
        public LogEventLevel LogLevel { get; set; } = LogEventLevel.Information;
    }

    public static class VictoriaLogsLoggerConfigurationExtensions
    {
        public static LoggerConfiguration VictoriaLogsHttp(
            this LoggerSinkConfiguration sinkConfiguration,
            IConfiguration configuration,
            IServiceProvider services,
            string configurationSection = VictoriaLogsOptions.CONFIG_NAME,
            string defaultAppName = "")
        {
            if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (services == null) throw new ArgumentNullException(nameof(services));

            var options = configuration
                .GetSection(configurationSection)
                .Get<VictoriaLogsOptions>() ?? new VictoriaLogsOptions();

            if (string.IsNullOrWhiteSpace(options.Endpoint))
            {
                throw new InvalidOperationException($"Configuration section '{configurationSection}:{nameof(VictoriaLogsOptions.Endpoint)}' is required.");
            }

            var httpContextAccessor = services.GetService<IHttpContextAccessor>();
            if (httpContextAccessor == null)
            {
                throw new InvalidOperationException("IHttpContextAccessor is not registered. Call AddHttpContextAccessor().");
            }

            var appName = string.IsNullOrWhiteSpace(options.AppName)
                ? defaultAppName
                : options.AppName;
            Console.Write("log level is " + options.LogLevel.ToString());
            return sinkConfiguration.Http(
                requestUri: options.Endpoint,
                textFormatter: new VictoriaLogFormatter(
                    Environment.MachineName,
                    string.IsNullOrWhiteSpace(appName) ? string.Empty : appName,
                    httpContextAccessor),
                batchFormatter: new VictoriaLogBatchFormatter(),
                httpClient: new VictoriaLogsHttpClient(),
                restrictedToMinimumLevel: options.LogLevel,
                queueLimitBytes: null);
        }

        // public static LoggerConfiguration VictoriaLogsHttp(
        //     this LoggerConfiguration loggerConfiguration,
        //     IConfiguration configuration,
        //     IServiceProvider services,
        //     string configurationSection = "VictoriaLogs")
        // {
        //     if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
        //     if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        //     if (services == null) throw new ArgumentNullException(nameof(services));

        //     return loggerConfiguration.WriteTo.VictoriaLogsHttp(configuration, services, configurationSection);
        // }

        public static LoggerConfiguration VictoriaLogsHttp(
            this LoggerConfiguration loggerConfiguration,
            IConfiguration configuration,
            IServiceProvider services,
            string configurationSection,
            string defaultAppName)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (services == null) throw new ArgumentNullException(nameof(services));

            return loggerConfiguration.WriteTo.VictoriaLogsHttp(configuration, services, configurationSection, defaultAppName);
        }
    }

    public static class VictoriaLogsWebApplicationBuilderExtensions
    {

        public static WebApplicationBuilder AddVictoriaLogs(
            this WebApplicationBuilder builder,
            string configurationSection = VictoriaLogsOptions.CONFIG_NAME)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.Services.AddHttpContextAccessor();
            builder.Host.UseSerilog((context, services, configuration) =>
            {
                configuration
                .ReadFrom.Configuration(context.Configuration)
                .VictoriaLogsHttp(builder.Configuration, services, configurationSection, builder.Environment.ApplicationName);
            });

            return builder;
        }
    }
}
