using Azure;
using Azure.AI.OpenAI;
using Contracts.Insights;
using Contracts.Signals;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.Text.Json;

namespace FunctionApp.Agents;

public sealed class InsightReasoningAgent
    : IAgent<BusinessSignalsV1, BusinessInsightsV1>
{
    private readonly ChatClient _chatClient;
    private readonly string _promptTemplate;
    private readonly ILogger<InsightReasoningAgent> _logger;

    public InsightReasoningAgent(
        IConfiguration config,
        ILogger<InsightReasoningAgent> logger)
    {
        _logger = logger;

        var endpoint = config["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("Missing OpenAI endpoint");

        var apiKey = config["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("Missing OpenAI API key");

        var deployment = config["AzureOpenAI:DeploymentName"]
            ?? throw new InvalidOperationException("Missing deployment name");

        var options = new AzureOpenAIClientOptions();

        var openAiClient = new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey),
            options);

        _chatClient = openAiClient.GetChatClient(deployment);

        var promptPath = Path.Combine(
            AppContext.BaseDirectory,
            "Prompts",
            "insight.prompt.txt");

        if (!File.Exists(promptPath))
            throw new FileNotFoundException("Prompt file not found", promptPath);

        _promptTemplate = File.ReadAllText(promptPath);
    }

    public async Task<BusinessInsightsV1> ExecuteAsync(
        BusinessSignalsV1 signals,
        CancellationToken ct)
    {
        try
        {
            var prompt = BuildPrompt(signals);

            var response = await _chatClient.CompleteChatAsync(
                new ChatMessage[]
                {
                    new SystemChatMessage(
                        "You produce structured executive-level business insights."),
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

            // Extract first TEXT content part safely
            var textPart = response.Value.Content
                .FirstOrDefault(p => p.Kind == ChatMessageContentPartKind.Text);

            if (textPart == null || string.IsNullOrWhiteSpace(textPart.Text))
            {
                throw new InvalidOperationException(
                    "AI response did not contain valid text content.");
            }

            var content = textPart.Text;

            // Validate JSON by parsing
            using var document = JsonDocument.Parse(content);

            var parsed = JsonSerializer.Deserialize<LLMInsightResponse>(
                content,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (parsed is null)
                throw new InvalidOperationException("Failed to parse AI JSON response");

            // Log token usage (if available)
            if (response.Value.Usage != null)
            {
                _logger.LogInformation(
                    "OpenAI usage - InputTokens: {Input}, OutputTokens: {Output}, TotalTokens: {Total}",
                    response.Value.Usage.InputTokenCount,
                    response.Value.Usage.OutputTokenCount,
                    response.Value.Usage.TotalTokenCount);
            }

            return new BusinessInsightsV1(
                ExecutiveSummary: parsed.ExecutiveSummary ?? string.Empty,
                KeyRisks: parsed.KeyRisks ?? Array.Empty<string>(),
                Opportunities: parsed.Opportunities ?? Array.Empty<string>(),
                Recommendations: parsed.Recommendations ?? Array.Empty<string>(),
                GeneratedAt: DateTimeOffset.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Insight reasoning failed.");

            return new BusinessInsightsV1(
                ExecutiveSummary: "Insights generation pending.",
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
