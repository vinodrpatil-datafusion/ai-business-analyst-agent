namespace FunctionApp.Configurations;

/// <summary>
/// Controls adaptive token budgeting and model execution safety.
/// </summary>
public sealed class AIExecutionOptions
{
    public int MaxContextTokens { get; set; } = 8192;
    public int MaxPromptTokens { get; set; } = 6000;
    public int MaxOutputTokens { get; set; } = 800;
    public int SafetyMargin { get; set; } = 500;
    public bool EnableAdaptiveBudgeting { get; set; } = true;
}