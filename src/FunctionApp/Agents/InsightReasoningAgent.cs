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
    : IAgent<(Guid JobId, BusinessSignalsV1 Signals), BusinessInsightsV1>
{
    private readonly ChatClient _chatClient;
    private readonly string _promptTemplate;
    private readonly ILogger<InsightReasoningAgent> _logger;
    private readonly InsightStore _insightStore;
    private readonly string _deploymentName;

    private const string PromptVersion = "v1.0";
    private const int MaxResponseLength = 100_000; // safety guard

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
        (Guid JobId, BusinessSignalsV1 Signals) input,
        CancellationToken ct)
    {
        var (jobId, signals) = input;

        try
        {
            _logger.LogInformation(
                "Starting AI reasoning for JobId {JobId}, Model {Model}, PromptVersion {Version}",
                jobId,
                _deploymentName,
                PromptVersion);

            var prompt = BuildPrompt(signals);

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
                "AI reasoning completed for JobId {JobId} in {ElapsedMs} ms",
                jobId,
                stopwatch.ElapsedMilliseconds);

            var textPart = response.Value.Content
                .FirstOrDefault(p => p.Kind == ChatMessageContentPartKind.Text);

            if (textPart == null || string.IsNullOrWhiteSpace(textPart.Text))
                throw new InvalidOperationException(
                    "AI response did not contain valid text content.");

            var rawJson = textPart.Text.Trim();

            // Guard against oversized or malicious output
            if (rawJson.Length > MaxResponseLength)
                throw new InvalidOperationException(
                    "AI response exceeds expected size.");

            // Validate JSON structure
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

            var structured = new BusinessInsightsV1(
                ExecutiveSummary: parsed.ExecutiveSummary ?? string.Empty,
                KeyRisks: parsed.KeyRisks ?? Array.Empty<string>(),
                Opportunities: parsed.Opportunities ?? Array.Empty<string>(),
                Recommendations: parsed.Recommendations ?? Array.Empty<string>(),
                GeneratedAt: DateTimeOffset.UtcNow
            );

            // Persist structured + raw response + metadata
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

            // Token telemetry (debug-level only)
            if (response.Value.Usage != null)
            {
                _logger.LogDebug(
                    "OpenAI usage for JobId {JobId} - Input: {Input}, Output: {Output}, Total: {Total}",
                    jobId,
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
                "Azure OpenAI request failed for JobId {JobId} with Status {StatusCode}",
                jobId,
                ex.Status);

            throw; // Let orchestration mark job as Failed
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Insight reasoning failed for JobId {JobId}",
                jobId);

            return new BusinessInsightsV1(
                ExecutiveSummary: "Insights generation temporarily unavailable.",
                KeyRisks: Array.Empty<string>(),
                Opportunities: Array.Empty<string>(),
                Recommendations: Array.Empty<string>(),
                GeneratedAt: DateTimeOffset.UtcNow
            );
        }
    }

    private string BuildPrompt(BusinessSignalsV1 signals)
    {
        return _promptTemplate
            .Replace("{{RecordCount}}", signals.RecordCount.ToString())
            .Replace("{{NumericTotals}}",
                JsonSerializer.Serialize(signals.NumericTotals))
            .Replace("{{NumericAverages}}",
                JsonSerializer.Serialize(signals.NumericAverages))
            .Replace("{{CategoryCounts}}",
                JsonSerializer.Serialize(signals.CategoryCounts));
    }

    private sealed class LLMInsightResponse
    {
        public string? ExecutiveSummary { get; set; }
        public string[]? KeyRisks { get; set; }
        public string[]? Opportunities { get; set; }
        public string[]? Recommendations { get; set; }
    }
}