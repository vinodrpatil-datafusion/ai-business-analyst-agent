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
using Microsoft.Extensions.Options;
using FunctionApp.Configurations;
using System.Linq;

namespace FunctionApp.Agents;

public sealed class InsightReasoningAgent
    : IAgent<(Guid JobId, InsightSignalSummaryV1 Summary), BusinessInsightsV1>
{
    private readonly ChatClient _chatClient;
    private readonly string _promptTemplate;
    private readonly ILogger<InsightReasoningAgent> _logger;
    private readonly InsightStore _insightStore;
    private readonly AIExecutionOptions _aiOptions;
    private readonly string _deploymentName;

    private const int TokenCharRatio = 4;
    private const string PromptVersion = "v1.1";
    private const int MaxResponseLength = 100_000;

    public InsightReasoningAgent(
        IConfiguration config,
        ILogger<InsightReasoningAgent> logger,
        InsightStore insightStore,
        IOptions<AIExecutionOptions> aiOptions)
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
        _aiOptions = aiOptions.Value;
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

                ct.ThrowIfCancellationRequested();

                var prompt = BuildPrompt(summary);

                _logger.LogDebug(
                    "Prompt length chars:{Chars}, approxTokens:{Tokens}",
                    prompt.Length,
                    prompt.Length / TokenCharRatio);

                var maxOutputTokens = CalculateAdaptiveOutputTokens(prompt);

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
                        MaxOutputTokenCount = maxOutputTokens,
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

                if (rawJson.StartsWith("```"))
                    throw new InvalidOperationException(
                        "Markdown detected in AI output.");

                if (rawJson.Length > MaxResponseLength)
                    throw new InvalidOperationException(
                        "AI response exceeds expected size.");

                using var doc = JsonDocument.Parse(rawJson);

                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException(
                        "AI response is not a JSON object.");

                if (!doc.RootElement.TryGetProperty("ExecutiveSummary", out _) ||
                    !doc.RootElement.TryGetProperty("KeyRisks", out var risksProp) ||
                    !doc.RootElement.TryGetProperty("Opportunities", out var oppProp) ||
                    !doc.RootElement.TryGetProperty("Recommendations", out var recProp))
                {
                    throw new InvalidOperationException(
                        "AI JSON missing required properties.");
                }

                if (risksProp.ValueKind != JsonValueKind.Array ||
                    oppProp.ValueKind != JsonValueKind.Array ||
                    recProp.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException(
                        "AI JSON properties must be arrays.");
                }

                var parsed = JsonSerializer.Deserialize<LLMInsightResponse>(
                    rawJson,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (parsed is null ||
                    string.IsNullOrWhiteSpace(parsed.ExecutiveSummary))
                {
                    throw new InvalidOperationException(
                        "AI response failed validation.");
                }

                var structured = new BusinessInsightsV1(
                    ExecutiveSummary: parsed.ExecutiveSummary,
                    KeyRisks: parsed.KeyRisks ?? Array.Empty<string>(),
                    Opportunities: parsed.Opportunities ?? Array.Empty<string>(),
                    Recommendations: parsed.Recommendations ?? Array.Empty<string>(),
                    GeneratedAt: DateTimeOffset.UtcNow
                );

                var summaryJson = JsonSerializer.Serialize(summary);

                await _insightStore.SaveAsync(
                    jobId,
                    structured,
                    summaryJson,
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
            catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex,
                    "AI reasoning cancelled due to timeout.");
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

    private int CalculateAdaptiveOutputTokens(string prompt)
    {
        if (!_aiOptions.EnableAdaptiveBudgeting)
            return _aiOptions.MaxOutputTokens;

        var approxPromptTokens = prompt.Length / TokenCharRatio;

        _logger.LogInformation(
            "Prompt approx tokens: {PromptTokens}",
            approxPromptTokens);

        if (approxPromptTokens > _aiOptions.MaxPromptTokens)
            throw new InvalidOperationException(
                $"Prompt exceeds configured MaxPromptTokens ({_aiOptions.MaxPromptTokens}).");

        var availableForOutput =
            _aiOptions.MaxContextTokens
            - approxPromptTokens
            - _aiOptions.SafetyMargin;

        if (availableForOutput <= 0)
            throw new InvalidOperationException(
                "Insufficient context window remaining for model output.");

        var finalOutput =
            Math.Min(availableForOutput, _aiOptions.MaxOutputTokens);

        _logger.LogInformation(
            "Adaptive output tokens calculated: {OutputTokens}",
            finalOutput);

        return finalOutput;
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