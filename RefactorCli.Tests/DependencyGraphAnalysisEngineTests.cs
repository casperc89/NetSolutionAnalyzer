using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorCli.Commands.DependencyGraph.Analysis;

namespace RefactorCli.Tests;

public sealed class DependencyGraphAnalysisEngineTests
{
    [Fact]
    public async Task AnalyzeAsync_Produces_LeafToRoot_PostOrder()
    {
        var solution = CreateGraphSolution(
            ("Core", "namespace CoreNs; public class CoreType {}", []),
            ("Data", "namespace DataNs; public class DataType : CoreNs.CoreType {}", ["Core"]),
            ("App", "namespace AppNs; public class AppType { public DataNs.DataType D { get; } = new(); }", ["Data"]));

        var engine = new DependencyGraphAnalysisEngine();
        var report = await engine.AnalyzeAsync(solution, "/tmp/test.sln", CancellationToken.None);

        var order = report.UpgradeOrderLeafToRoot.Select(x => x.ProjectName).ToArray();
        Assert.Equal(["Core", "Data", "App"], order);
    }

    [Fact]
    public async Task AnalyzeAsync_Captures_Transitive_Upstream_Class_Dependencies()
    {
        var solution = CreateGraphSolution(
            ("Core", "namespace CoreNs; public class CoreType {}", []),
            ("Data", "namespace DataNs; public class DataType { public CoreNs.CoreType? C { get; set; } }", ["Core"]),
            ("App", "namespace AppNs; public class AppType { public DataNs.DataType D { get; } = new(); public CoreNs.CoreType? C { get; set; } }", ["Data"]));

        var engine = new DependencyGraphAnalysisEngine();
        var report = await engine.AnalyzeAsync(solution, "/tmp/test.sln", CancellationToken.None);

        var app = report.Projects.Single(p => p.ProjectName == "App");
        Assert.Contains("Data", app.TransitiveDependencies);
        Assert.Contains("Core", app.TransitiveDependencies);

        Assert.Contains(app.UpstreamClasses, c => c.ClassName == "DataNs.DataType" && c.DeclaringProjectName == "Data");
        Assert.Equal(1, app.UniqueTransitiveUpstreamClassCount);

        var data = report.Projects.Single(p => p.ProjectName == "Data");
        Assert.Contains(data.UpstreamClasses, c => c.ClassName == "CoreNs.CoreType" && c.DeclaringProjectName == "Core");
        Assert.Equal(1, data.UniqueTransitiveUpstreamClassCount);
    }

    private static Solution CreateGraphSolution(params (string Name, string Source, string[] Dependencies)[] projects)
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        var projectIds = new Dictionary<string, ProjectId>(StringComparer.Ordinal);

        foreach (var project in projects)
        {
            var projectId = ProjectId.CreateNewId(debugName: project.Name);
            projectIds[project.Name] = projectId;

            solution = solution
                .AddProject(ProjectInfo.Create(
                    projectId,
                    VersionStamp.Create(),
                    project.Name,
                    project.Name,
                    LanguageNames.CSharp,
                    filePath: $"/tmp/{project.Name}/{project.Name}.csproj",
                    parseOptions: new CSharpParseOptions(LanguageVersion.CSharp12)))
                .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));
        }

        foreach (var project in projects)
        {
            var projectId = projectIds[project.Name];
            foreach (var dependency in project.Dependencies)
            {
                solution = solution.AddProjectReference(projectId, new ProjectReference(projectIds[dependency]));
            }

            solution = solution.AddDocument(
                DocumentId.CreateNewId(projectId),
                $"{project.Name}.cs",
                project.Source);
        }

        return solution;
    }
}
