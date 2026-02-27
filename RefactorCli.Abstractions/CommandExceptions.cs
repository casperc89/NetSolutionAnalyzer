namespace RefactorCli.Abstractions;

public sealed class InvalidCommandOptionsException : Exception
{
    public InvalidCommandOptionsException(string message) : base(message)
    {
    }
}

public sealed class SolutionLoadException : Exception
{
    public SolutionLoadException(string message) : base(message)
    {
    }
}
