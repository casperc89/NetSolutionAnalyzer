using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging.Abstractions;
using RefactorCli.Commands.SystemWebCatalog.Analysis;

namespace RefactorCli.Tests;

public class SystemWebAnalyzersTests
{
    [Fact]
    public async Task SW0001_Finds_Using_SystemWeb()
    {
        var project = CreateProject("""
            using System.Web;
            namespace Demo;
            public class HomeController {}
            """);

        var analyzer = new UsingSystemWebCatalogAnalyzer();
        var acc = new CatalogAccumulator();
        await analyzer.AnalyzeAsync(project, acc, CancellationToken.None);

        Assert.Contains(acc.Findings, f => f.Id == "SW0001" && f.Symbol == "System.Web");
    }

    [Fact]
    public async Task SW0002_Finds_FullyQualified_SystemWeb_Usage_WithoutUsing()
    {
        var project = CreateProject("""
            namespace System.Web { public class HttpContext {} }
            namespace Demo;
            public class C
            {
                private System.Web.HttpContext _ctx = new System.Web.HttpContext();
            }
            """);

        var analyzer = new SemanticSystemWebSymbolCatalogAnalyzer();
        var acc = new CatalogAccumulator();
        await analyzer.AnalyzeAsync(project, acc, CancellationToken.None);

        Assert.Contains(acc.Findings, f => f.Id == "SW0002" && f.Symbol is not null && f.Symbol.Contains("System.Web.HttpContext", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CatalogEngine_Produces_Stable_Finding_Order()
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "AProject",
                "AProject",
                LanguageNames.CSharp,
                parseOptions: new CSharpParseOptions(LanguageVersion.CSharp12)))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

        solution = solution
            .AddDocument(DocumentId.CreateNewId(projectId), "b.cs", "class B {}")
            .AddDocument(DocumentId.CreateNewId(projectId), "a.cs", "class A {}");

        var project = solution.GetProject(projectId)!;
        var engine = new CatalogEngine([new DeterministicOrderProbeAnalyzer()], NullLogger<CatalogEngine>.Instance);
        var report = await engine.AnalyzeAsync(project.Solution, "/tmp/test.sln", CancellationToken.None);

        var findings = report.Projects.Single().Findings;
        var first = findings.First();
        Assert.Equal("SWX001", first.Id);
        Assert.EndsWith("a.cs", first.FilePath, StringComparison.Ordinal);
    }

    private static Project CreateProject(string source)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "TestProject",
                "TestProject",
                LanguageNames.CSharp,
                parseOptions: new CSharpParseOptions(LanguageVersion.CSharp12)))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddDocument(DocumentId.CreateNewId(projectId), "test.cs", source);

        return solution.GetProject(projectId)!;
    }

    private sealed class DeterministicOrderProbeAnalyzer : ICatalogAnalyzer
    {
        public string Id => "SWX001";

        public Task AnalyzeAsync(Project project, CatalogAccumulator acc, CancellationToken ct)
        {
            foreach (var doc in project.Documents.OrderByDescending(d => d.Name, StringComparer.Ordinal))
            {
                acc.Add(
                    id: "SWX001",
                    category: "Member",
                    severity: "Info",
                    message: "probe",
                    filePath: doc.Name,
                    line: 10,
                    column: 1,
                    symbol: doc.Name);
            }

            return Task.CompletedTask;
        }
    }
}
