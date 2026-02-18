using Contracts.Insights;
using Contracts.Invocation;
using Contracts.Signals;
using FunctionApp.Agents;
using FunctionApp.Configurations;
using FunctionApp.Parsing;
using FunctionApp.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FunctionApp;

/// <summary>
/// Host builder for the FunctionApp.
/// Configures configuration sources, dependency injection,
/// observability, and agent infrastructure.
/// </summary>
public class Program
{
    /// <summary>
    /// Application entry point. Builds and runs the Azure Functions worker host.
    /// </summary>
    public static void Main(string[] args)
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureAppConfiguration((context, config) =>
            {
                // Ensure environment variables are available
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;

                // ------------------------------------------------------------
                // Configurations
                // ------------------------------------------------------------
                services.Configure<AIExecutionOptions>(
                    configuration.GetSection("AIExecution"));

                // ------------------------------------------------------------
                // Observability & Logging
                // ------------------------------------------------------------
                services.AddLogging();
                services.AddApplicationInsightsTelemetryWorkerService();
                services.ConfigureFunctionsApplicationInsights();

                // ------------------------------------------------------------
                // Agent-Oriented Architecture
                // ------------------------------------------------------------
                services.AddSingleton<IAgent<string, BusinessSignalsV1>, SignalExtractionAgent>();

                services.AddSingleton<InsightSignalSummarizer>();

                services.AddSingleton<
                    IAgent<(Guid JobId, InsightSignalSummaryV1 Summary), BusinessInsightsV1>,
                    InsightReasoningAgent>();

                // Nullable because JobStore may return null (404 case)
                services.AddSingleton<
                    IAgent<Guid, JobStatusResponseV1?>,
                    JobStatusQueryAgent>();

                // ------------------------------------------------------------
                // File Parsing Infrastructure
                // ------------------------------------------------------------
                services.AddSingleton<CsvFileParser>();
                services.AddSingleton<ExcelFileParser>();
                services.AddSingleton<FileParserFactory>();

                // ------------------------------------------------------------
                // Blob Infrastructure
                // ------------------------------------------------------------
                var blobConnection =
                    configuration["BlobConnectionString"]
                    ?? throw new InvalidOperationException(
                        "BlobConnectionString is not configured.");

                services.AddSingleton<BlobFileReader>(sp =>
                    new BlobFileReader(blobConnection));

                // ------------------------------------------------------------
                // Database Infrastructure
                // ------------------------------------------------------------
                var sqlConnection =
                    configuration["SqlConnectionString"]
                    ?? throw new InvalidOperationException(
                        "SqlConnectionString is not configured.");

                services.AddSingleton<JobStore>(sp =>
                    new JobStore(sqlConnection));

                services.AddSingleton<SignalStore>(sp =>
                    new SignalStore(sqlConnection));

                services.AddSingleton<InsightStore>(sp =>
                    new InsightStore(sqlConnection));
            })
            .Build();

        host.Run();
    }
}