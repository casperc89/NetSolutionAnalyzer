namespace RefactorCli.Abstractions;

public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    string GetFullPath(string path);

    string ReadAllText(string path);

    Task WriteAllTextAsync(string path, string content, CancellationToken ct);

    void EnsureDirectory(string path);

    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption option);
}
