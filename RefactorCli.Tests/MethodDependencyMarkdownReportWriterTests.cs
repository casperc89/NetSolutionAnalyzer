using RefactorCli.Commands.MethodDependency.Contracts;
using RefactorCli.Commands.MethodDependency.Reporting;
using RefactorCli.Infrastructure;

namespace RefactorCli.Tests;

public sealed class MethodDependencyMarkdownReportWriterTests
{
    [Fact]
    public async Task WriteAsync_Writes_CallTree_And_Unresolved_Table()
    {
        var rootId = "App|AppNs.Foo.Start()";
        var midId = "App|AppNs.Foo.Mid()";

        var report = new MethodDependencyReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            SolutionPath = "/tmp/sample.sln",
            RequestedFilePath = "/tmp/App/Foo.cs",
            RequestedLine = 6,
            RootMethod = new MethodNode
            {
                SymbolId = rootId,
                DisplayName = "AppNs.Foo.Start()",
                ProjectName = "App",
                ProjectPath = "/tmp/App/App.csproj",
                FilePath = "/tmp/App/Foo.cs",
                Line = 5,
                IsExternal = false
            },
            Methods =
            [
                new MethodNode
                {
                    SymbolId = rootId,
                    DisplayName = "AppNs.Foo.Start()",
                    ProjectName = "App",
                    ProjectPath = "/tmp/App/App.csproj",
                    FilePath = "/tmp/App/Foo.cs",
                    Line = 5,
                    IsExternal = false
                },
                new MethodNode
                {
                    SymbolId = midId,
                    DisplayName = "AppNs.Foo.Mid()",
                    ProjectName = "App",
                    ProjectPath = "/tmp/App/App.csproj",
                    FilePath = "/tmp/App/Foo.cs",
                    Line = 10,
                    IsExternal = false
                }
            ],
            Edges =
            [
                new MethodDependencyEdge
                {
                    CallerSymbolId = rootId,
                    CalleeSymbolId = midId,
                    FilePath = "/tmp/App/Foo.cs",
                    Line = 7
                }
            ],
            UnresolvedCallSites =
            [
                new UnresolvedCallSite
                {
                    CallerSymbolId = midId,
                    Reason = "Unable to resolve invocation symbol.",
                    Expression = "d.Run()",
                    FilePath = "/tmp/App/Foo.cs",
                    Line = 12
                }
            ]
        };

        var outputDir = Path.Combine(Path.GetTempPath(), "refactorcli-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var writer = new MarkdownReportWriter(new FileSystem());
            var path = await writer.WriteAsync(report, outputDir, CancellationToken.None);
            var markdown = await File.ReadAllTextAsync(path);

            Assert.Contains("# Method Dependency Report", markdown, StringComparison.Ordinal);
            Assert.Contains("AppNs.Foo.Start()", markdown, StringComparison.Ordinal);
            Assert.Contains("-> AppNs.Foo.Mid()", markdown, StringComparison.Ordinal);
            Assert.Contains("## Unresolved Call Sites", markdown, StringComparison.Ordinal);
            Assert.Contains("d.Run()", markdown, StringComparison.Ordinal);
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
