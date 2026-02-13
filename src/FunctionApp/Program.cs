using Contracts.Insights;
using Contracts.Invocation;
using Contracts.Signals;
using FunctionApp.Agents;
using FunctionApp.Parsing;
using FunctionApp.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FunctionApp;

public class Program
{
    public static void Main(string[] args)
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureAppConfiguration(config =>
            {
                // Ensures local.settings.json + Azure App Settings are available
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;

                // ------------------------------------------------------------
                // Observability
                // ------------------------------------------------------------
                services.AddApplicationInsightsTelemetryWorkerService();
                services.ConfigureFunctionsApplicationInsights();

                // ------------------------------------------------------------
                // Agent registrations (agent-oriented architecture)
                // ------------------------------------------------------------
                services.AddSingleton<IAgent<string, BusinessSignalsV1>, SignalExtractionAgent>();
                services.AddSingleton<
                    IAgent<(Guid JobId, BusinessSignalsV1 Signals), BusinessInsightsV1>,
                    InsightReasoningAgent>();

                services.AddSingleton<
                    IAgent<Guid, JobStatusResponseV1>,
                    JobStatusQueryAgent>();

                // ------------------------------------------------------------
                // File parsing infrastructure
                // ------------------------------------------------------------
                services.AddSingleton<CsvFileParser>();
                services.AddSingleton<ExcelFileParser>();
                services.AddSingleton<FileParserFactory>();

                // ------------------------------------------------------------
                // Blob infrastructure
                // ------------------------------------------------------------
                var blobConnection =
                    configuration["BlobConnectionString"]
                    ?? throw new InvalidOperationException(
                        "BlobConnectionString is not configured.");

                services.AddSingleton(new BlobFileReader(blobConnection));

                // ------------------------------------------------------------
                // Database infrastructure
                // ------------------------------------------------------------
                var sqlConnection =
                    configuration["SqlConnectionString"]
                    ?? throw new InvalidOperationException(
                        "SqlConnectionString is not configured.");

                services.AddSingleton(new JobStore(sqlConnection));
                services.AddSingleton(new SignalStore(sqlConnection));
                services.AddSingleton(new InsightStore(sqlConnection));
            })
            .Build();

        host.Run();
    }
}