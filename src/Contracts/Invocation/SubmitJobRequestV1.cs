namespace Contracts.Invocation;

public sealed record SubmitJobRequestV1(
    string FileName,
    string FileType,            // csv | xlsx
    long FileSizeInBytes
);

