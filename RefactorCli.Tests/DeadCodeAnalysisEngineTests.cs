using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorCli.Commands.DeadCode.Analysis;
using RefactorCli.Commands.DeadCode.Configuration;
using RefactorCli.Commands.DeadCode.Contracts;

namespace RefactorCli.Tests;

public sealed class DeadCodeAnalysisEngineTests
{
    [Fact]
    public async Task AnalyzeAsync_PrivateUnusedMethod_IsDefinitelyDead()
    {
        var solution = CreateSolution(
            ("App", "namespace Demo; public class A { private void Hidden() {} public void Live() { } }", []));

        var engine = new DeadCodeAnalysisEngine();
        var report = await engine.AnalyzeAsync(
            solution,
            "/tmp/sample.sln",
            new DeadCodeOptions
            {
                SolutionPath = "/tmp/sample.sln",
                OutputPath = "/tmp",
                Formats = ["json"],
                ExcludeTestProjects = true,
                MinConfidence = DeadCodeConfidence.Unknown,
                ConfigPath = null
            },
            DeadCodeConfig.Empty,
            CancellationToken.None);

        var hidden = report.Findings.Single(f => f.Symbol.Contains("A.Hidden()", StringComparison.Ordinal));
        Assert.Equal(DeadCodeConfidence.DefinitelyDead, hidden.Confidence);
    }

    [Fact]
    public async Task AnalyzeAsync_PublicUnusedMethod_IsLikelyDead()
    {
        var solution = CreateSolution(
            ("App", "namespace Demo; public class A { public void MaybeUsed() {} }", []));

        var engine = new DeadCodeAnalysisEngine();
        var report = await engine.AnalyzeAsync(
            solution,
            "/tmp/sample.sln",
            new DeadCodeOptions
            {
                SolutionPath = "/tmp/sample.sln",
                OutputPath = "/tmp",
                Formats = ["json"],
                ExcludeTestProjects = true,
                MinConfidence = DeadCodeConfidence.Unknown,
                ConfigPath = null
            },
            DeadCodeConfig.Empty,
            CancellationToken.None);

        var maybeUsed = report.Findings.Single(f => f.Symbol.Contains("A.MaybeUsed()", StringComparison.Ordinal));
        Assert.Equal(DeadCodeConfidence.LikelyDead, maybeUsed.Confidence);
    }

    [Fact]
    public async Task AnalyzeAsync_DynamicPatternMatched_MarksUnknown()
    {
        var solution = CreateSolution(
            ("App", "namespace Demo; public class A { public void MaybeUsed() {} public void X(){ var t = Assembly.GetType(\"Demo.A\"); } }", []));

        var engine = new DeadCodeAnalysisEngine();
        var report = await engine.AnalyzeAsync(
            solution,
            "/tmp/sample.sln",
            new DeadCodeOptions
            {
                SolutionPath = "/tmp/sample.sln",
                OutputPath = "/tmp",
                Formats = ["json"],
                ExcludeTestProjects = true,
                MinConfidence = DeadCodeConfidence.Unknown,
                ConfigPath = null
            },
            new DeadCodeConfig
            {
                DynamicUsage = new DynamicUsageConfig
                {
                    MarkUnknownIfMatched = true,
                    ReflectionPatterns = ["Assembly.GetType"]
                }
            },
            CancellationToken.None);

        var maybeUsed = report.Findings.Single(f => f.Symbol.Contains("A.MaybeUsed()", StringComparison.Ordinal));
        Assert.Equal(DeadCodeConfidence.Unknown, maybeUsed.Confidence);
    }

    [Fact]
    public async Task AnalyzeAsync_AspNetControllerAction_IsRooted()
    {
        var source = """
                     namespace Microsoft.AspNetCore.Mvc;
                     public class ControllerBase { }
                     public sealed class NonActionAttribute : System.Attribute { }
                     namespace Demo;
                     public class HomeController : Microsoft.AspNetCore.Mvc.ControllerBase
                     {
                         public void Index() { }
                         [Microsoft.AspNetCore.Mvc.NonAction]
                         public void Helper() { }
                     }
                     """;

        var solution = CreateSolution(("App", source, []));

        var engine = new DeadCodeAnalysisEngine();
        var report = await engine.AnalyzeAsync(
            solution,
            "/tmp/sample.sln",
            new DeadCodeOptions
            {
                SolutionPath = "/tmp/sample.sln",
                OutputPath = "/tmp",
                Formats = ["json"],
                ExcludeTestProjects = true,
                MinConfidence = DeadCodeConfidence.Unknown,
                ConfigPath = null
            },
            DeadCodeConfig.Empty,
            CancellationToken.None);

        Assert.DoesNotContain(report.Findings, f => f.Symbol.Contains("HomeController.Index()", StringComparison.Ordinal));
        Assert.Contains(report.Findings, f => f.Symbol.Contains("HomeController.Helper()", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnalyzeAsync_ExcludeTestProjects_RemovesTests()
    {
        var solution = CreateSolution(
            ("App", "namespace Demo; public class A { public void MaybeUsed() {} }", []),
            ("App.Tests", "namespace Demo.Tests; public class T { }", ["App"]));

        var engine = new DeadCodeAnalysisEngine();
        var report = await engine.AnalyzeAsync(
            solution,
            "/tmp/sample.sln",
            new DeadCodeOptions
            {
                SolutionPath = "/tmp/sample.sln",
                OutputPath = "/tmp",
                Formats = ["json"],
                ExcludeTestProjects = true,
                MinConfidence = DeadCodeConfidence.Unknown,
                ConfigPath = null
            },
            DeadCodeConfig.Empty,
            CancellationToken.None);

        Assert.Equal(1, report.ProjectsAnalyzed);
        Assert.DoesNotContain(report.Findings, f => f.ProjectName == "App.Tests");
    }

    private static Solution CreateSolution(params (string Name, string Source, string[] Dependencies)[] projects)
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
