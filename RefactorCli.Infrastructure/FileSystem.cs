using RefactorCli.Abstractions;

namespace RefactorCli.Infrastructure;

public sealed class FileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public Task WriteAllTextAsync(string path, string content, CancellationToken ct) =>
        File.WriteAllTextAsync(path, content, ct);

    public void EnsureDirectory(string path) => Directory.CreateDirectory(path);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption option)
    {
        if (!Directory.Exists(path))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(path, searchPattern, option);
    }
}
