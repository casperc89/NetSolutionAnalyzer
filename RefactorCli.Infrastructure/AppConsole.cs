using RefactorCli.Abstractions;

namespace RefactorCli.Infrastructure;

public sealed class AppConsole : IAppConsole
{
    public void Info(string message) => Console.WriteLine(message);

    public void Warn(string message) => Console.WriteLine($"[warn] {message}");

    public void Error(string message) => Console.Error.WriteLine(message);
}
