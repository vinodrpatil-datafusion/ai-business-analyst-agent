using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

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

                // Shared Entra credential for all resource access (OpenAI, Blob).
                // In Azure, binds to the user-assigned managed identity by client ID
                // (set ManagedIdentityClientId in app settings). Locally, when that
                // setting is absent, DefaultAzureCredential falls back to developer
                // sign-in (az login / Visual Studio / VS Code). No keys in config.
                var managedIdentityClientId = configuration["ManagedIdentityClientId"];

                var credentialOptions = new DefaultAzureCredentialOptions();
                if (!string.IsNullOrWhiteSpace(managedIdentityClientId))
                {
                    credentialOptions.ManagedIdentityClientId = managedIdentityClientId;
                }

                var credential = new DefaultAzureCredential(credentialOptions);

                // Create and register ChatClient for Azure OpenAI (Entra auth)
                var endpoint = configuration["AzureOpenAI:Endpoint"]
                    ?? throw new InvalidOperationException("Missing OpenAI endpoint");

                var deploymentName = configuration["AzureOpenAI:DeploymentName"]
                    ?? throw new InvalidOperationException("Missing deployment name");

                var azureClient = new AzureOpenAIClient(
                    new Uri(endpoint),
                    credential);

                var chatClient = azureClient.GetChatClient(deploymentName);

                services.AddSingleton(chatClient);

                // Read prompt template from file and register as singleton
                var promptPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Prompts",
                    "insight.prompt.txt");

                if (!File.Exists(promptPath))
                    throw new FileNotFoundException("Prompt file not found", promptPath);

                var promptTemplate = File.ReadAllText(promptPath);
                services.AddSingleton<string>(promptTemplate);

                services.AddSingleton<
                    IAgent<(Guid JobId, InsightSignalSummaryV1 Summary), BusinessInsightsV1>>(sp =>
                {
                    var client = sp.GetRequiredService<ChatClient>();
                    var prompt = sp.GetRequiredService<string>();
                    var logger = sp.GetRequiredService<ILogger<InsightReasoningAgent>>();
                    var store = sp.GetRequiredService<InsightStore>();
                    var options = sp.GetRequiredService<IOptions<AIExecutionOptions>>();

                    return new InsightReasoningAgent(
                        client,
                        prompt,
                        deploymentName,
                        logger,
                        store,
                        options);
                });

                // Nullable because JobStore may return null (404 case)
                services.AddSingleton<
                    IAgent<Guid, JobStatusResponseV1?>,
                    JobStatusQueryAgent>();

                // Read path for generated insights (GET /jobs/{id}/insights).
                // Nullable: returns null when no insights exist yet (404 case).
                services.AddSingleton<
                    IAgent<Guid, BusinessInsightsV1?>,
                    InsightQueryAgent>();

                // ------------------------------------------------------------
                // File Parsing Infrastructure
                // ------------------------------------------------------------
                services.AddSingleton<CsvFileParser>();
                services.AddSingleton<ExcelFileParser>();
                services.AddSingleton<FileParserFactory>();

                // ------------------------------------------------------------
                // Blob Infrastructure
                // ------------------------------------------------------------
                var blobServiceUri =
                    configuration["BlobServiceUri"]
                    ?? throw new InvalidOperationException(
                        "BlobServiceUri is not configured.");

                services.AddSingleton<BlobFileReader>(sp =>
                    new BlobFileReader(new Uri(blobServiceUri), credential));

                // ------------------------------------------------------------
                // Database Infrastructure
                // ------------------------------------------------------------
                // SQL uses Entra auth via the connection string
                // (Authentication=Active Directory Default) — no SQL credentials.
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