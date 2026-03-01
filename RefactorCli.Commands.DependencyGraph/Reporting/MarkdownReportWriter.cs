using System.Text;
using RefactorCli.Abstractions;
using RefactorCli.Commands.DependencyGraph.Contracts;

namespace RefactorCli.Commands.DependencyGraph.Reporting;

public sealed class MarkdownReportWriter : IReportWriter
{
    private readonly IFileSystem _fileSystem;

    public MarkdownReportWriter(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string Format => "md";

    public async Task<string> WriteAsync(DependencyGraphReport report, string outputDir, int maxClassesPerProject, CancellationToken ct)
    {
        _fileSystem.EnsureDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "dependency-graph.md");
        var markdown = BuildMarkdown(report, maxClassesPerProject);
        await _fileSystem.WriteAllTextAsync(outputPath, markdown, ct);
        return outputPath;
    }

    private static string BuildMarkdown(DependencyGraphReport report, int maxClassesPerProject)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Dependency Graph Report");
        builder.AppendLine();
        builder.AppendLine($"- Generated (UTC): {report.GeneratedAtUtc:O}");
        builder.AppendLine($"- Solution: `{report.SolutionPath}`");
        builder.AppendLine($"- Projects: {report.Projects.Count}");
        builder.AppendLine($"- Dependency edges: {report.Edges.Count}");
        builder.AppendLine();

        if (report.Cycles.Count > 0)
        {
            builder.AppendLine("## Cycles");
            builder.AppendLine();
            builder.AppendLine("Cycles were detected. Leaf-to-root order is still emitted, but cycle members require manual sequencing.");
            builder.AppendLine();
            foreach (var cycle in report.Cycles.OrderBy(c => string.Join('|', c.ProjectNames), StringComparer.Ordinal))
            {
                builder.AppendLine($"- {string.Join(" -> ", cycle.ProjectNames)}");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Upgrade Order (Leaf -> Root)");
        builder.AppendLine();
        builder.AppendLine("| # | Project | Path |");
        builder.AppendLine("|---:|---|---|");
        foreach (var entry in report.UpgradeOrderLeafToRoot)
        {
            builder.AppendLine($"| {entry.Index} | {entry.ProjectName} | `{entry.ProjectPath ?? ""}` |");
        }

        builder.AppendLine();
        builder.AppendLine("## Project Dependency Tree (Text)");
        builder.AppendLine();
        builder.AppendLine("```text");
        foreach (var line in BuildDependencyTreeLines(report))
        {
            builder.AppendLine(line);
        }

        builder.AppendLine("```");

        builder.AppendLine();
        builder.AppendLine("## Transitive Upstream Class Dependencies");
        builder.AppendLine();
        foreach (var project in report.Projects.OrderByDescending(p => p.UniqueTransitiveUpstreamClassCount).ThenBy(p => p.ProjectName, StringComparer.Ordinal))
        {
            builder.AppendLine($"### {project.ProjectName}");
            builder.AppendLine();
            builder.AppendLine($"- Transitive upstream projects: {project.TransitiveDependencies.Count}");
            builder.AppendLine($"- Unique upstream classes referenced: {project.UniqueTransitiveUpstreamClassCount}");
            builder.AppendLine();

            if (project.UpstreamClasses.Count == 0)
            {
                builder.AppendLine("_No transitive upstream class dependencies found._");
                builder.AppendLine();
                continue;
            }

            var grouped = project.UpstreamClasses
                .GroupBy(c => c.DeclaringProjectName)
                .Select(group => new
                {
                    UpstreamProject = group.Key,
                    UniqueClassCount = group.Count(),
                    TotalReferences = group.Sum(c => c.ReferenceCount),
                    Classes = group
                        .OrderByDescending(c => c.ReferenceCount)
                        .ThenBy(c => c.ClassName, StringComparer.Ordinal)
                        .ToList()
                })
                .OrderByDescending(g => g.UniqueClassCount)
                .ThenByDescending(g => g.TotalReferences)
                .ThenBy(g => g.UpstreamProject, StringComparer.Ordinal)
                .ToList();

            builder.AppendLine("| Upstream Project | Unique Classes | Total References | Class Samples |");
            builder.AppendLine("|---|---:|---:|---|");

            var totalPrinted = 0;
            foreach (var group in grouped)
            {
                if (totalPrinted >= maxClassesPerProject)
                {
                    break;
                }

                var remaining = maxClassesPerProject - totalPrinted;
                var classesShown = Math.Min(remaining, Math.Min(3, group.Classes.Count));
                var classes = group.Classes
                    .Take(classesShown)
                    .Select(c => $"{c.ClassName} ({c.ReferenceCount})")
                    .ToList();
                var classesText = string.Join(", ", classes);
                if (group.Classes.Count > classesShown)
                {
                    classesText += $", ... (+{group.Classes.Count - classesShown} more)";
                }

                builder.AppendLine(
                    $"| {group.UpstreamProject} | {group.UniqueClassCount} | {group.TotalReferences} | {EscapeMarkdownTableCell(classesText)} |");
                totalPrinted += classesShown;
            }

            if (project.UpstreamClasses.Count > totalPrinted)
            {
                builder.AppendLine();
                builder.AppendLine($"_Class samples truncated by {project.UpstreamClasses.Count - totalPrinted} entries (set `--max-classes-per-project` to increase)._");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Top Coupling Hotspots");
        builder.AppendLine();
        builder.AppendLine("| Project | Unique Upstream Classes | Transitive Upstream Projects |");
        builder.AppendLine("|---|---:|---:|");
        foreach (var project in report.Projects
                     .OrderByDescending(p => p.UniqueTransitiveUpstreamClassCount)
                     .ThenBy(p => p.ProjectName, StringComparer.Ordinal)
                     .Take(20))
        {
            builder.AppendLine($"| {project.ProjectName} | {project.UniqueTransitiveUpstreamClassCount} | {project.TransitiveDependencies.Count} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Most Referenced Upstream Classes (Portfolio)");
        builder.AppendLine();
        var portfolioClasses = report.Projects
            .SelectMany(project => project.UpstreamClasses)
            .GroupBy(c => c.ClassName)
            .Select(group => new
            {
                ClassName = group.Key,
                DeclaringProject = group
                    .OrderByDescending(x => x.ReferenceCount)
                    .ThenBy(x => x.DeclaringProjectName, StringComparer.Ordinal)
                    .Select(x => x.DeclaringProjectName)
                    .FirstOrDefault() ?? "-",
                TotalReferences = group.Sum(x => x.ReferenceCount),
                ReferencingProjects = group.Count()
            })
            .OrderByDescending(x => x.TotalReferences)
            .ThenByDescending(x => x.ReferencingProjects)
            .ThenBy(x => x.ClassName, StringComparer.Ordinal)
            .Take(30)
            .ToList();

        if (portfolioClasses.Count == 0)
        {
            builder.AppendLine("_No upstream class references found across the portfolio._");
        }
        else
        {
            builder.AppendLine("| Class | Declaring Project | Total References | Referencing Projects |");
            builder.AppendLine("|---|---|---:|---:|");
            foreach (var cls in portfolioClasses)
            {
                builder.AppendLine(
                    $"| {EscapeMarkdownTableCell(cls.ClassName)} | {EscapeMarkdownTableCell(cls.DeclaringProject)} | {cls.TotalReferences} | {cls.ReferencingProjects} |");
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> BuildDependencyTreeLines(DependencyGraphReport report)
    {
        var dependenciesByProject = report.Edges
            .GroupBy(e => e.ProjectName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.DependsOnProjectName).OrderBy(x => x, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        var allProjects = report.Projects.Select(p => p.ProjectName).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var dependedUpon = new HashSet<string>(report.Edges.Select(e => e.DependsOnProjectName), StringComparer.Ordinal);
        var roots = allProjects.Where(p => !dependedUpon.Contains(p)).ToList();
        if (roots.Count == 0)
        {
            roots = allProjects;
        }

        var lines = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            Render(root, indent: "", withArrow: false, ancestors: new HashSet<string>(StringComparer.Ordinal));
        }

        return lines;

        void Render(string project, string indent, bool withArrow, HashSet<string> ancestors)
        {
            if (!seen.Add(project) && indent.Length == 0)
            {
                return;
            }

            var line = withArrow ? $"{indent}-> {project}" : $"{indent}{project}";
            lines.Add(line);

            if (!dependenciesByProject.TryGetValue(project, out var deps) || deps.Count == 0)
            {
                return;
            }

            ancestors.Add(project);
            foreach (var dep in deps)
            {
                if (ancestors.Contains(dep))
                {
                    var cycleIndent = withArrow ? indent + "   " : indent + "  ";
                    lines.Add($"{cycleIndent}-> {dep} [cycle]");
                    continue;
                }

                var childIndent = withArrow ? indent + "   " : indent + "  ";
                Render(dep, childIndent, withArrow: true, new HashSet<string>(ancestors, StringComparer.Ordinal));
            }
        }
    }

    private static string EscapeMarkdownTableCell(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }
}
