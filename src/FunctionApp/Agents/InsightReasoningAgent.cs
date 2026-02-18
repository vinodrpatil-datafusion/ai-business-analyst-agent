using Azure;
using Azure.AI.OpenAI;
using Contracts.Insights;
using Contracts.Signals;
using FunctionApp.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.Diagnostics;
using System.Text.Json;

namespace FunctionApp.Agents;

public sealed class InsightReasoningAgent
    : IAgent<(Guid JobId, InsightSignalSummaryV1 Summary), BusinessInsightsV1>
{
    private readonly ChatClient _chatClient;
    private readonly string _promptTemplate;
    private readonly ILogger<InsightReasoningAgent> _logger;
    private readonly InsightStore _insightStore;
    private readonly string _deploymentName;

    private const string PromptVersion = "v1.1";
    private const int MaxResponseLength = 100_000;
    private const int MaxSafePromptTokens = 6000;

    public InsightReasoningAgent(
        IConfiguration config,
        ILogger<InsightReasoningAgent> logger,
        InsightStore insightStore)
    {
        _logger = logger;
        _insightStore = insightStore;

        var endpoint = config["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("Missing OpenAI endpoint");

        var apiKey = config["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("Missing OpenAI API key");

        _deploymentName = config["AzureOpenAI:DeploymentName"]
            ?? throw new InvalidOperationException("Missing deployment name");

        var client = new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey));

        _chatClient = client.GetChatClient(_deploymentName);

        var promptPath = Path.Combine(
            AppContext.BaseDirectory,
            "Prompts",
            "insight.prompt.txt");

        if (!File.Exists(promptPath))
            throw new FileNotFoundException("Prompt file not found", promptPath);

        _promptTemplate = File.ReadAllText(promptPath);
    }

    public async Task<BusinessInsightsV1> ExecuteAsync(
        (Guid JobId, InsightSignalSummaryV1 Summary) input,
        CancellationToken ct)
    {
        var (jobId, summary) = input;

        using (_logger.BeginScope("JobId:{JobId}", jobId))
        {
            try
            {
                _logger.LogInformation(
                    "Starting AI reasoning. Model {Model}, PromptVersion {Version}",
                    _deploymentName,
                    PromptVersion);

                var prompt = BuildPrompt(summary);

                EnforcePromptBudget(prompt);

                var stopwatch = Stopwatch.StartNew();

                var response = await _chatClient.CompleteChatAsync(
                    new ChatMessage[]
                    {
                        new SystemChatMessage(
                            "You must respond ONLY with valid JSON matching the expected schema. " +
                            "Do not include markdown, commentary, or explanations."),
                        new UserChatMessage(prompt)
                    },
                    new ChatCompletionOptions
                    {
                        Temperature = 0.2f,
                        MaxOutputTokenCount = 800,
                        ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
                    },
                    cancellationToken: ct
                );

                stopwatch.Stop();

                _logger.LogInformation(
                    "AI reasoning completed in {ElapsedMs} ms",
                    stopwatch.ElapsedMilliseconds);

                var textPart = response.Value.Content
                    .FirstOrDefault(p => p.Kind == ChatMessageContentPartKind.Text);

                if (textPart == null || string.IsNullOrWhiteSpace(textPart.Text))
                    throw new InvalidOperationException(
                        "AI response did not contain valid text content.");

                var rawJson = textPart.Text.Trim();

                if (rawJson.Length > MaxResponseLength)
                    throw new InvalidOperationException(
                        "AI response exceeds expected size.");

                using var doc = JsonDocument.Parse(rawJson);

                var parsed = JsonSerializer.Deserialize<LLMInsightResponse>(
                    rawJson,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (parsed is null)
                    throw new InvalidOperationException(
                        "Failed to parse AI JSON response.");

                if (string.IsNullOrWhiteSpace(parsed.ExecutiveSummary))
                    throw new InvalidOperationException(
                        "AI response missing ExecutiveSummary.");

                var structured = new BusinessInsightsV1(
                    ExecutiveSummary: parsed.ExecutiveSummary,
                    KeyRisks: parsed.KeyRisks ?? Array.Empty<string>(),
                    Opportunities: parsed.Opportunities ?? Array.Empty<string>(),
                    Recommendations: parsed.Recommendations ?? Array.Empty<string>(),
                    GeneratedAt: DateTimeOffset.UtcNow
                );

                await _insightStore.SaveAsync(
                    jobId,
                    structured,
                    rawJson,
                    structured.GeneratedAt,
                    PromptVersion,
                    _deploymentName,
                    response.Value.Usage?.InputTokenCount,
                    response.Value.Usage?.OutputTokenCount,
                    response.Value.Usage?.TotalTokenCount,
                    ct);

                if (response.Value.Usage != null)
                {
                    _logger.LogDebug(
                        "Token usage - Input:{Input}, Output:{Output}, Total:{Total}",
                        response.Value.Usage.InputTokenCount,
                        response.Value.Usage.OutputTokenCount,
                        response.Value.Usage.TotalTokenCount);
                }

                return structured;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(
                    ex,
                    "Azure OpenAI request failed with Status {StatusCode}",
                    ex.Status);

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Insight reasoning failed");
                throw;
            }
        }
    }

    private void EnforcePromptBudget(string prompt)
    {
        var approxTokens = prompt.Length / 4;

        if (approxTokens > MaxSafePromptTokens)
            throw new InvalidOperationException(
                $"Prompt too large ({approxTokens} tokens).");

        _logger.LogInformation(
            "Prompt size approx {Tokens} tokens",
            approxTokens);
    }

    private string BuildPrompt(InsightSignalSummaryV1 summary)
    {
        return _promptTemplate
            .Replace("{{RecordCount}}", summary.RecordCount.ToString())
            .Replace("{{TopNumericTotals}}",
                JsonSerializer.Serialize(summary.TopNumericTotals))
            .Replace("{{TopNumericAverages}}",
                JsonSerializer.Serialize(summary.TopNumericAverages))
            .Replace("{{CategoryHighlights}}",
                JsonSerializer.Serialize(summary.CategoryHighlights))
            .Replace("{{AnomalyCount}}",
                summary.AnomalyCount.ToString())
            .Replace("{{SampleAnomalies}}",
                JsonSerializer.Serialize(summary.SampleAnomalies))
            .Replace("{{ColumnCount}}",
                summary.ColumnCount.ToString())
            .Replace("{{NumericColumnCount}}",
                summary.NumericColumnCount.ToString())
            .Replace("{{CategoricalColumnCount}}",
                summary.CategoricalColumnCount.ToString());
    }

    private sealed class LLMInsightResponse
    {
        public string? ExecutiveSummary { get; set; }
        public string[]? KeyRisks { get; set; }
        public string[]? Opportunities { get; set; }
        public string[]? Recommendations { get; set; }
    }
}