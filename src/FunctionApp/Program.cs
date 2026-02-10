using Contracts.Insights;
using Contracts.Signals;
using FunctionApp.Agents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FunctionApp
{
    public class Program
    {
        static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices(services =>
                {
                    // Application Insights for observability
                    services.AddApplicationInsightsTelemetryWorkerService();
                    services.ConfigureFunctionsApplicationInsights();

                    // Agent registrations (agent-oriented architecture)
                    services.AddSingleton<IAgent<string, BusinessSignalsV1>, SignalExtractionAgent>();
                    services.AddSingleton<IAgent<BusinessSignalsV1, BusinessInsightsV1>, InsightReasoningAgent>();
                })
                .Build();

            host.Run();
        }
    }
}
