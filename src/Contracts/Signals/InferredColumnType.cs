namespace Contracts.Signals;

/// <summary>
/// Deterministic column type inferred during signal extraction.
/// This is part of the stable contract and must not be string-based.
/// </summary>
public enum InferredColumnType
{
    Unknown = 0,
    Numeric = 1,
    Categorical = 2,
    DateTime = 3,
    Boolean = 4,
    Text = 5
}