public interface IFileParser
{
    Task<IReadOnlyList<IDictionary<string, string>>> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken);
}