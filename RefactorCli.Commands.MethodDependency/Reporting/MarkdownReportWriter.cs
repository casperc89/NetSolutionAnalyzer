using System.Text;
using RefactorCli.Abstractions;
using RefactorCli.Commands.MethodDependency.Contracts;

namespace RefactorCli.Commands.MethodDependency.Reporting;

public sealed class MarkdownReportWriter : IReportWriter
{
    private readonly IFileSystem _fileSystem;

    public MarkdownReportWriter(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string Format => "md";

    public async Task<string> WriteAsync(MethodDependencyReport report, string outputDir, CancellationToken ct)
    {
        _fileSystem.EnsureDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "method-dependency.md");
        var markdown = BuildMarkdown(report);
        await _fileSystem.WriteAllTextAsync(outputPath, markdown, ct);
        return outputPath;
    }

    private static string BuildMarkdown(MethodDependencyReport report)
    {
        var builder = new StringBuilder();

        builder.AppendLine("# Method Dependency Report");
        builder.AppendLine();
        builder.AppendLine($"- Generated (UTC): {report.GeneratedAtUtc:O}");
        builder.AppendLine($"- Solution: `{report.SolutionPath}`");
        builder.AppendLine($"- File: `{report.RequestedFilePath}`");
        builder.AppendLine($"- Line: {report.RequestedLine}");
        builder.AppendLine($"- Root method: `{report.RootMethod.DisplayName}`");
        builder.AppendLine($"- Methods discovered: {report.Methods.Count}");
        builder.AppendLine($"- Call edges: {report.Edges.Count}");
        builder.AppendLine($"- Unresolved call sites: {report.UnresolvedCallSites.Count}");
        builder.AppendLine();

        builder.AppendLine("## Method Call Tree");
        builder.AppendLine();
        builder.AppendLine("```text");
        foreach (var line in BuildCallTreeLines(report))
        {
            builder.AppendLine(line);
        }

        builder.AppendLine("```");
        builder.AppendLine();

        builder.AppendLine("## Methods");
        builder.AppendLine();
        builder.AppendLine("| Method | Project | File | Line | External |\n|---|---|---|---:|---:|");

        foreach (var method in report.Methods.OrderBy(m => m.DisplayName, StringComparer.Ordinal))
        {
            builder.AppendLine(
                $"| {EscapeMarkdownTableCell(method.DisplayName)} | {EscapeMarkdownTableCell(method.ProjectName ?? "-")} | `{method.FilePath ?? "-"}` | {method.Line?.ToString() ?? "-"} | {(method.IsExternal ? "yes" : "no")} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Call Edges");
        builder.AppendLine();
        builder.AppendLine("| Caller | Callee | File | Line |\n|---|---|---|---:|");

        var methodById = report.Methods.ToDictionary(m => m.SymbolId, StringComparer.Ordinal);
        foreach (var edge in report.Edges)
        {
            var caller = methodById.TryGetValue(edge.CallerSymbolId, out var callerNode)
                ? callerNode.DisplayName
                : edge.CallerSymbolId;
            var callee = methodById.TryGetValue(edge.CalleeSymbolId, out var calleeNode)
                ? calleeNode.DisplayName
                : edge.CalleeSymbolId;

            builder.AppendLine(
                $"| {EscapeMarkdownTableCell(caller)} | {EscapeMarkdownTableCell(callee)} | `{edge.FilePath ?? "-"}` | {edge.Line?.ToString() ?? "-"} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Unresolved Call Sites");
        builder.AppendLine();

        if (report.UnresolvedCallSites.Count == 0)
        {
            builder.AppendLine("_No unresolved call sites._");
        }
        else
        {
            builder.AppendLine("| Caller | Reason | Expression | File | Line |\n|---|---|---|---|---:|");

            foreach (var unresolved in report.UnresolvedCallSites)
            {
                var caller = methodById.TryGetValue(unresolved.CallerSymbolId, out var callerNode)
                    ? callerNode.DisplayName
                    : unresolved.CallerSymbolId;

                builder.AppendLine(
                    $"| {EscapeMarkdownTableCell(caller)} | {EscapeMarkdownTableCell(unresolved.Reason)} | `{unresolved.Expression ?? "-"}` | `{unresolved.FilePath ?? "-"}` | {unresolved.Line?.ToString() ?? "-"} |");
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> BuildCallTreeLines(MethodDependencyReport report)
    {
        var edgesByCaller = report.Edges
            .GroupBy(e => e.CallerSymbolId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.CalleeSymbolId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);

        var methodById = report.Methods.ToDictionary(m => m.SymbolId, StringComparer.Ordinal);
        var lines = new List<string>();

        Render(report.RootMethod.SymbolId, "", new HashSet<string>(StringComparer.Ordinal));
        return lines;

        void Render(string methodId, string indent, HashSet<string> path)
        {
            var label = methodById.TryGetValue(methodId, out var method)
                ? method.DisplayName
                : methodId;

            lines.Add(indent.Length == 0 ? label : $"{indent}-> {label}");

            if (!edgesByCaller.TryGetValue(methodId, out var children) || children.Count == 0)
            {
                return;
            }

            path.Add(methodId);
            foreach (var child in children)
            {
                if (path.Contains(child))
                {
                    var cycleLabel = methodById.TryGetValue(child, out var childMethod)
                        ? childMethod.DisplayName
                        : child;
                    lines.Add($"{indent}   -> {cycleLabel} [cycle]");
                    continue;
                }

                Render(child, indent + "   ", new HashSet<string>(path, StringComparer.Ordinal));
            }
        }
    }

    private static string EscapeMarkdownTableCell(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }
}
