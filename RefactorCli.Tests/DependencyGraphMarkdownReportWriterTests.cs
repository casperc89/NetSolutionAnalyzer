using RefactorCli.Commands.DependencyGraph.Contracts;
using RefactorCli.Commands.DependencyGraph.Reporting;
using RefactorCli.Infrastructure;

namespace RefactorCli.Tests;

public sealed class DependencyGraphMarkdownReportWriterTests
{
    [Fact]
    public async Task WriteAsync_DependencyTree_DoesNotDuplicate_Nodes()
    {
        var report = new DependencyGraphReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            SolutionPath = "/tmp/sample.sln",
            Projects =
            [
                new ProjectDependencyNode
                {
                    ProjectName = "A",
                    ProjectPath = "/tmp/A/A.csproj",
                    DirectDependencies = ["B"],
                    TransitiveDependencies = ["B"],
                    UniqueTransitiveUpstreamClassCount = 0,
                    UpstreamClasses = []
                },
                new ProjectDependencyNode
                {
                    ProjectName = "B",
                    ProjectPath = "/tmp/B/B.csproj",
                    DirectDependencies = [],
                    TransitiveDependencies = [],
                    UniqueTransitiveUpstreamClassCount = 0,
                    UpstreamClasses = []
                }
            ],
            Edges =
            [
                new ProjectDependencyEdge
                {
                    ProjectName = "A",
                    ProjectPath = "/tmp/A/A.csproj",
                    DependsOnProjectName = "B",
                    DependsOnProjectPath = "/tmp/B/B.csproj"
                }
            ],
            UpgradeOrderLeafToRoot =
            [
                new UpgradeOrderEntry { Index = 1, ProjectName = "B", ProjectPath = "/tmp/B/B.csproj" },
                new UpgradeOrderEntry { Index = 2, ProjectName = "A", ProjectPath = "/tmp/A/A.csproj" }
            ],
            Cycles = []
        };

        var outputDir = Path.Combine(Path.GetTempPath(), "refactorcli-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var writer = new MarkdownReportWriter(new FileSystem());
            var path = await writer.WriteAsync(report, outputDir, maxClassesPerProject: 20, CancellationToken.None);
            var markdown = await File.ReadAllTextAsync(path);

            Assert.Contains("A\n  -> B", markdown, StringComparison.Ordinal);
            Assert.DoesNotContain("  -> B\n     B", markdown, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteAsync_TransitiveUpstreamClasses_Uses_ProjectSummaryTable()
    {
        var report = new DependencyGraphReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            SolutionPath = "/tmp/sample.sln",
            Projects =
            [
                new ProjectDependencyNode
                {
                    ProjectName = "App",
                    ProjectPath = "/tmp/App/App.csproj",
                    DirectDependencies = ["Core"],
                    TransitiveDependencies = ["Core", "Data"],
                    UniqueTransitiveUpstreamClassCount = 4,
                    UpstreamClasses =
                    [
                        new UpstreamClassDependency { ClassName = "CoreNs.AuthService", DeclaringProjectName = "Core", DeclaringProjectPath = "/tmp/Core/Core.csproj", ReferenceCount = 7 },
                        new UpstreamClassDependency { ClassName = "CoreNs.Options", DeclaringProjectName = "Core", DeclaringProjectPath = "/tmp/Core/Core.csproj", ReferenceCount = 3 },
                        new UpstreamClassDependency { ClassName = "DataNs.UserEntity", DeclaringProjectName = "Data", DeclaringProjectPath = "/tmp/Data/Data.csproj", ReferenceCount = 5 },
                        new UpstreamClassDependency { ClassName = "DataNs.SessionEntity", DeclaringProjectName = "Data", DeclaringProjectPath = "/tmp/Data/Data.csproj", ReferenceCount = 2 }
                    ]
                }
            ],
            Edges =
            [
                new ProjectDependencyEdge
                {
                    ProjectName = "App",
                    ProjectPath = "/tmp/App/App.csproj",
                    DependsOnProjectName = "Core",
                    DependsOnProjectPath = "/tmp/Core/Core.csproj"
                }
            ],
            UpgradeOrderLeafToRoot =
            [
                new UpgradeOrderEntry { Index = 1, ProjectName = "Core", ProjectPath = "/tmp/Core/Core.csproj" },
                new UpgradeOrderEntry { Index = 2, ProjectName = "App", ProjectPath = "/tmp/App/App.csproj" }
            ],
            Cycles = []
        };

        var outputDir = Path.Combine(Path.GetTempPath(), "refactorcli-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var writer = new MarkdownReportWriter(new FileSystem());
            var path = await writer.WriteAsync(report, outputDir, maxClassesPerProject: 20, CancellationToken.None);
            var markdown = await File.ReadAllTextAsync(path);

            Assert.Contains("### App", markdown, StringComparison.Ordinal);
            Assert.Contains("- Transitive upstream projects: 2", markdown, StringComparison.Ordinal);
            Assert.Contains("| Upstream Project | Unique Classes | Total References | Class Samples |", markdown, StringComparison.Ordinal);
            Assert.Contains("| Core | 2 | 10 |", markdown, StringComparison.Ordinal);
            Assert.Contains("| Data | 2 | 7 |", markdown, StringComparison.Ordinal);
            Assert.Contains("## Most Referenced Upstream Classes (Portfolio)", markdown, StringComparison.Ordinal);
            Assert.Contains("| Class | Declaring Project | Total References | Referencing Projects |", markdown, StringComparison.Ordinal);
            Assert.Contains("| CoreNs.AuthService | Core | 7 | 1 |", markdown, StringComparison.Ordinal);
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
