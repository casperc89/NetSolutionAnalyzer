namespace RefactorCli.Abstractions;

public interface IAppConsole
{
    void Info(string message);

    void Warn(string message);

    void Error(string message);
}
