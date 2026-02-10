namespace FunctionApp.Agents;

public interface IAgent<in TInput, TOutput>
{
    Task<TOutput> ExecuteAsync(
        TInput input,
        CancellationToken cancellationToken = default
    );
}
