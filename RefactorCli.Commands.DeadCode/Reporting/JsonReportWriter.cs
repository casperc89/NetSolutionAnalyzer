using System.Text.Json;
using RefactorCli.Abstractions;
using RefactorCli.Commands.DeadCode.Contracts;

namespace RefactorCli.Commands.DeadCode.Reporting;

public sealed class JsonReportWriter : IReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly IFileSystem _fileSystem;

    public JsonReportWriter(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string Format => "json";

    public async Task<string> WriteAsync(DeadCodeReport report, string outputDir, CancellationToken ct)
    {
        _fileSystem.EnsureDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "deadcode.json");
        var json = JsonSerializer.Serialize(report, SerializerOptions);
        await _fileSystem.WriteAllTextAsync(outputPath, json, ct);
        return outputPath;
    }
}
