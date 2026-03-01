using System.Text.Json;
using RefactorCli.Abstractions;
using RefactorCli.Commands.DependencyGraph.Contracts;

namespace RefactorCli.Commands.DependencyGraph.Reporting;

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

    public async Task<string> WriteAsync(DependencyGraphReport report, string outputDir, int maxClassesPerProject, CancellationToken ct)
    {
        _fileSystem.EnsureDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "dependency-graph.json");
        var json = JsonSerializer.Serialize(report, SerializerOptions);
        await _fileSystem.WriteAllTextAsync(outputPath, json, ct);
        return outputPath;
    }
}
