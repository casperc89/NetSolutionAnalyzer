namespace RefactorCli.Abstractions;

public interface ICommandHandler<in TOptions>
{
    Task<int> ExecuteAsync(TOptions options, CancellationToken ct);
}
