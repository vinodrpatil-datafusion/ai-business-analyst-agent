namespace Contracts.Signals;

/// <summary>
/// Version 1 of deterministic column-level metadata.
/// Part of the immutable contract layer.
/// Any future changes must result in ColumnMetadataV2.
/// </summary>
public sealed record ColumnMetadataV1(
    string ColumnName,
    InferredColumnType ColumnType,
    int NullCount,
    int UniqueCount,
    decimal? Min,
    decimal? Max,
    decimal? Average
);

