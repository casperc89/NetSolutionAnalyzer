using RefactorCli.Commands.DeadCode.Contracts;
using RefactorCli.Commands.DeadCode.Reporting;
using RefactorCli.Infrastructure;

namespace RefactorCli.Tests;

public sealed class DeadCodeMarkdownReportWriterTests
{
    [Fact]
    public async Task WriteAsync_WritesConfidenceSections()
    {
        var report = new DeadCodeReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            SolutionPath = "/tmp/sample.sln",
            ProjectsAnalyzed = 1,
            Diagnostics = new DeadCodeDiagnostics
            {
                CandidateSymbols = 1,
                RootSymbols = 0,
                ProjectsWithDynamicPatterns = 0,
                Timing = new DeadCodeTiming()
            },
            Findings =
            [
                new DeadCodeFinding
                {
                    Id = "DC0001",
                    Confidence = DeadCodeConfidence.DefinitelyDead,
                    Symbol = "Demo.A.Hidden()",
                    SymbolKind = "Method",
                    ProjectName = "App",
                    ProjectPath = "/tmp/App/App.csproj",
                    Message = "Unreachable",
                    FilePath = "/tmp/App/A.cs",
                    Line = 10,
                    Column = 5,
                    Evidence = ["No static references found."]
                }
            ]
        };

        var outputDir = Path.Combine(Path.GetTempPath(), "refactorcli-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var writer = new MarkdownReportWriter(new FileSystem());
            var path = await writer.WriteAsync(report, outputDir, CancellationToken.None);
            var markdown = await File.ReadAllTextAsync(path);

            Assert.Contains("## DefinitelyDead", markdown, StringComparison.Ordinal);
            Assert.Contains("`Demo.A.Hidden()`", markdown, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }
}
