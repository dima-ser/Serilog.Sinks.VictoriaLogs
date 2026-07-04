using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.Http;
using Serilog.Sinks.Http.HttpClients;
using System.Text.Json;
using System.Reflection;

namespace Serilog.Sinks.VictoriaLogs
{
    /// <summary>
    /// A custom <see cref="ITextFormatter"/> implementation that formats log events as JSON objects to conform 
    /// to VictoriaLogs <a href="https://docs.victoriametrics.com/victorialogs/data-ingestion/#json-stream-api">JSON Stream API</a>. 
    /// Adds VictoriaLogs specific fields such as "_msg", "_time". Automatically injects "hostname" (Environment.MachineName) and 
    /// "app_name"(assembly name) which are used as stream fields for optimal VictoriaLogs functionality (can be overridden). Optionally adds 
    /// "exception" and "exception_type" if an exception is present in the log event.
    /// </summary>
    public sealed class VictoriaLogsFormatter : ITextFormatter
    {
        public const string DEFAULT_STREAM_FIELDS= "hostname,app_name";
        bool lowerCasePropertyKeys;
        //IReadOnlyDictionary<string, string>? overridePropertyKeys;
        public VictoriaLogsFormatter(bool lowerCasePropertyKeys)
        {
            this.lowerCasePropertyKeys = lowerCasePropertyKeys;
            //this.overridePropertyKeys = overridePropertyKeys;
        }

        public void Format(LogEvent logEvent, TextWriter output)
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            string assemblyName = assembly.GetName().Name ?? "UnknownApplication";
            var record = new Dictionary<string, object?>
            {
                ["_msg"] = logEvent.Exception != null ? logEvent.Exception.Message : logEvent.RenderMessage(),
                ["level"] = logEvent.Level.ToString(),
                ["_time"] = logEvent.Timestamp.UtcDateTime.ToString("O"),   
                ["hostname"] = Environment.MachineName,
                ["app_name"] = assemblyName      
            };

            if (logEvent.Exception != null)
            {
                record["exception"] = logEvent.Exception.ToString();
                record["exception_type"] = logEvent.Exception.GetType().FullName;
            }

            foreach (var property in logEvent.Properties)
            {
                // if (overridePropertyKeys != null && overridePropertyKeys.ContainsKey(property.Key))
                // {
                //     record[overridePropertyKeys[property.Key]] = property.Value.ToString().Trim('"');
                // }
                //else 
                if (lowerCasePropertyKeys)
                {
                    record[property.Key.ToLower()] = property.Value.ToString().Trim('"');
                }
                else 
                {
                    record[property.Key] = property.Value.ToString().Trim('"');
                }
            }   

            output.WriteLine(JsonSerializer.Serialize(record));
        }

    }
    /// <summary>
    /// A custom <see cref="IBatchFormatter"/> implementation that formats a batch of log events as a sequence of JSON objects, 
    /// each on a new line to conform to VictoriaLogs <a href="https://docs.victoriametrics.com/victorialogs/data-ingestion/#json-stream-api">JSON Stream API</a>
    /// </summary>
    public sealed class VictoriaLogsBatchFormatter : Serilog.Sinks.Http.IBatchFormatter
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
                    separator = Environment.NewLine;
                }
            }
        }
    }

    /// <summary>
    /// A custom <see cref="IHttpClient"/> implementation that adds the "VL-Stream-Fields" header to the HTTP request.
    /// </summary>
    public sealed class VictoriaLogsHttpClient : JsonHttpClient
    {

        public VictoriaLogsHttpClient(string streamFields)
            : base(CreateHttpClient(streamFields))
        {
        }

        private static HttpClient CreateHttpClient(string streamFields)
        {
            var client = new HttpClient();

            client.DefaultRequestHeaders.Add("VL-Stream-Fields", streamFields);

            return client;
        }
    }

    public static class VictoriaLogsLoggerConfigurationExtensions
    {
        /// <summary>
        /// Adds a sink that sends logs to VictoriaLogs using JSON Stream API: 
        /// https://docs.victoriametrics.com/victorialogs/data-ingestion/#json-stream-api
        /// </summary>
        /// <param name="sinkConfiguration">The logger configuration.</param>
        /// <param name="victoriaLogsEndpoint">The URL to VictoriaLogs JSON Strem API. For example: 
        /// http://localhost:9428/insert/jsonline</param>
        /// <param name="lowerCasePropertyKeys">Whether to convert property keys to lower case to 
        /// conform to VictoriaLogs convention. Default value is <see langword="true"/>.</param>
        /// <param name="streamFields">Comma-separated field names that consitute a 
        /// <a href="https://docs.victoriametrics.com/victorialogs/keyconcepts/#stream-fields">stream</a> 
        /// in VictoriaLogs. Default value is <see cref="VictoriaLogsFormatter.DEFAULT_STREAM_FIELDS"/>.</param>
        /// <param name="queueLimitBytes">
        /// The maximum size, in bytes, of events stored in memory, waiting to be sent over the
        /// network. Specify <see langword="null"/> for no limit.
        /// </param>
        /// <param name="logEventLimitBytes">
        /// The maximum size, in bytes, for a serialized representation of a log event. Log events
        /// exceeding this size will be dropped. Specify <see langword="null"/> for no limit. Default
        /// value is <see langword="null"/>.
        /// </param>
        /// <param name="logEventsInBatchLimit">
        /// The maximum number of log events sent as a single batch over the network. Default
        /// value is 1000.
        /// </param>
        /// <param name="batchSizeLimitBytes">
        /// The approximate maximum size, in bytes, for a single batch. The value is an
        /// approximation because only the size of the log events are considered. The extra
        /// characters added by the batch formatter, where the sequence of serialized log events
        /// are transformed into a payload, are not considered. Please make sure to accommodate for
        /// those.
        /// <para />
        /// Another thing to mention is that although the sink does its best to optimize for this
        /// limit, if you decide to use an implementation of <seealso cref="IHttpClient"/> that is
        /// compressing the payload, e.g. <seealso cref="JsonGzipHttpClient"/>, this parameter
        /// describes the uncompressed size of the log events. The compressed size might be
        /// significantly smaller depending on the compression algorithm and the repetitiveness of
        /// the log events.
        /// <para />
        /// Default value is <see langword="null"/>.
        /// </param>
        /// <param name="period">
        /// The time to wait between checking for event batches. Default value is 2 seconds.
        /// </param>
        /// <param name="flushOnClose">
        /// Whether to send the log events stored in memory during the sink's disposal, thus ensuring
        /// that all generated log event are sent to the log server before sink closes. Default value
        /// is <see langword="true"/>.
        /// </param>
        /// <param name="textFormatter">
        /// The formatter rendering individual log events into text, for example JSON. Default
        /// value is <see cref="VictoriaLogsTextFormatter"/>.
        /// </param>
        /// <param name="batchFormatter">
        /// The formatter batching multiple log events into a payload that can be sent over the
        /// network. Default value is <see cref="VictoriaLogsBatchFormatter"/>.
        /// </param>
        /// <param name="restrictedToMinimumLevel">
        /// The minimum level for events passed through the sink. Ignored when
        /// <paramref name="levelSwitch"/> is specified. Default value is
        /// <see cref="LevelAlias.Minimum"/>.
        /// </param>
        /// <param name="levelSwitch">
        /// A switch allowing the pass-through minimum level to be changed at runtime.
        /// </param>
        /// <param name="httpClient">
        /// A custom <see cref="IHttpClient"/> implementation. Default value is
        /// <see cref="VictoriaLogsHttpClient"/>.
        /// </param>
        /// <param name="configuration">
        /// Configuration passed to <paramref name="httpClient"/>. Parameter is either manually
        /// specified when configuring the sink in source code or automatically passed in when
        /// configuring the sink using
        /// <see href="https://www.nuget.org/packages/Serilog.Settings.Configuration">Serilog.Settings.Configuration</see>.
        /// </param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        public static LoggerConfiguration VictoriaLogsHttp(
            this LoggerSinkConfiguration sinkConfiguration,
            string victoriaLogsEndpoint,
            bool lowerCasePropertyKeys = true,
            string streamFields = VictoriaLogsFormatter.DEFAULT_STREAM_FIELDS,      
            long? queueLimitBytes = null,
            long? logEventLimitBytes = null,
            int? logEventsInBatchLimit = 1000,
            long? batchSizeLimitBytes = null,
            TimeSpan? period = null,
            bool flushOnClose = true,
            ITextFormatter? textFormatter = null,
            VictoriaLogsBatchFormatter? batchFormatter = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            LoggingLevelSwitch? levelSwitch = null,
            VictoriaLogsHttpClient? httpClient = null,
            IConfiguration? configuration = null)
        {
            if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
            if (victoriaLogsEndpoint == null) throw new ArgumentNullException(nameof(victoriaLogsEndpoint));

            // // Default values
             period ??= TimeSpan.FromSeconds(2);
             textFormatter ??= new VictoriaLogsFormatter(lowerCasePropertyKeys);
             batchFormatter ??= new VictoriaLogsBatchFormatter();
             httpClient ??= new VictoriaLogsHttpClient(lowerCasePropertyKeys ? streamFields.ToLower() : streamFields);

            if (configuration != null)
            {
                httpClient.Configure(configuration);
            }

            return sinkConfiguration.Http(
                requestUri: victoriaLogsEndpoint,
                textFormatter: textFormatter,
                batchFormatter: batchFormatter,
                httpClient: httpClient,
                restrictedToMinimumLevel: restrictedToMinimumLevel,
                levelSwitch: levelSwitch,
                queueLimitBytes: queueLimitBytes,
                logEventLimitBytes: logEventLimitBytes,
                logEventsInBatchLimit: logEventsInBatchLimit,
                batchSizeLimitBytes: batchSizeLimitBytes,
                period: period.Value,
                flushOnClose: flushOnClose);
        }
    }
   

}
