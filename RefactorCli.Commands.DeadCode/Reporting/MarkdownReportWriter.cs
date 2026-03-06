using System.Text;
using RefactorCli.Abstractions;
using RefactorCli.Commands.DeadCode.Contracts;

namespace RefactorCli.Commands.DeadCode.Reporting;

public sealed class MarkdownReportWriter : IReportWriter
{
    private readonly IFileSystem _fileSystem;

    public MarkdownReportWriter(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string Format => "md";

    public async Task<string> WriteAsync(DeadCodeReport report, string outputDir, CancellationToken ct)
    {
        _fileSystem.EnsureDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "deadcode.md");
        var markdown = BuildMarkdown(report);
        await _fileSystem.WriteAllTextAsync(outputPath, markdown, ct);
        return outputPath;
    }

    private static string BuildMarkdown(DeadCodeReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Dead Code Report");
        builder.AppendLine();
        builder.AppendLine($"- Generated (UTC): {report.GeneratedAtUtc:O}");
        builder.AppendLine($"- Solution: `{report.SolutionPath}`");
        builder.AppendLine($"- Projects analyzed: {report.ProjectsAnalyzed}");
        builder.AppendLine($"- Findings: {report.Findings.Count}");
        builder.AppendLine();

        foreach (var group in report.Findings
                     .GroupBy(f => f.Confidence)
                     .OrderBy(g => g.Key))
        {
            builder.AppendLine($"## {group.Key}");
            builder.AppendLine();
            builder.AppendLine("| Symbol | Kind | Project | Location | Message | Evidence |");
            builder.AppendLine("|---|---|---|---|---|---|");
            foreach (var finding in group.OrderBy(f => f.Symbol, StringComparer.Ordinal))
            {
                var location = finding.FilePath is null
                    ? "-"
                    : $"`{finding.FilePath}:{finding.Line ?? 0}:{finding.Column ?? 0}`";
                var evidence = finding.Evidence.Count == 0 ? "-" : string.Join("; ", finding.Evidence);
                builder.AppendLine($"| `{finding.Symbol}` | {finding.SymbolKind} | {finding.ProjectName} | {Escape(location)} | {Escape(finding.Message)} | {Escape(evidence)} |");
            }

            builder.AppendLine();
        }

        if (report.Findings.Count == 0)
        {
            builder.AppendLine("_No dead code findings for selected confidence threshold._");
        }

        return builder.ToString();
    }

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}
