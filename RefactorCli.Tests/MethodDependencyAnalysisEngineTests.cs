using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using RefactorCli.Commands.MethodDependency.Analysis;

namespace RefactorCli.Tests;

public sealed class MethodDependencyAnalysisEngineTests
{
    [Fact]
    public async Task AnalyzeAsync_LineInsideMethod_Produces_Transitive_Dependency_Graph()
    {
        const string appSource = """
namespace AppNs;

public class Foo
{
    public void Start()
    {
        Mid();
    }

    private void Mid()
    {
        var svc = new CoreNs.CoreService();
        svc.Leaf();
    }
}
""";

        const string coreSource = """
namespace CoreNs;

public class CoreService
{
    public void Leaf()
    {
    }
}
""";

        var filePath = "/tmp/App/Foo.cs";
        var line = FindLine(appSource, "Mid();");

        var solution = CreateSolution(
            ("Core", "/tmp/Core/Core.csproj", "/tmp/Core/CoreService.cs", coreSource, Array.Empty<string>()),
            ("App", "/tmp/App/App.csproj", filePath, appSource, ["Core"]));

        var engine = new MethodDependencyAnalysisEngine();
        var report = await engine.AnalyzeAsync(solution, "/tmp/Test.sln", filePath, line, CancellationToken.None);

        Assert.Contains("Foo.Start()", report.RootMethod.DisplayName, StringComparison.Ordinal);
        Assert.Contains(report.Methods, m => m.DisplayName.Contains("Foo.Mid()", StringComparison.Ordinal));
        Assert.Contains(report.Methods, m => m.DisplayName.Contains("CoreService.Leaf()", StringComparison.Ordinal));

        var start = report.Methods.Single(m => m.DisplayName.Contains("Foo.Start()", StringComparison.Ordinal));
        var mid = report.Methods.Single(m => m.DisplayName.Contains("Foo.Mid()", StringComparison.Ordinal));
        var leaf = report.Methods.Single(m => m.DisplayName.Contains("CoreService.Leaf()", StringComparison.Ordinal));

        Assert.Contains(report.Edges, e => e.CallerSymbolId == start.SymbolId && e.CalleeSymbolId == mid.SymbolId);
        Assert.Contains(report.Edges, e => e.CallerSymbolId == mid.SymbolId && e.CalleeSymbolId == leaf.SymbolId);
    }

    [Fact]
    public async Task AnalyzeAsync_LineAtSignature_Resolves_Root_Method()
    {
        const string source = """
namespace AppNs;

public class Foo
{
    public void Start()
    {
        Helper();
    }

    private void Helper()
    {
    }
}
""";

        var filePath = "/tmp/App/Foo.cs";
        var line = FindLine(source, "public void Start()");
        var solution = CreateSolution(("App", "/tmp/App/App.csproj", filePath, source, Array.Empty<string>()));

        var engine = new MethodDependencyAnalysisEngine();
        var report = await engine.AnalyzeAsync(solution, "/tmp/Test.sln", filePath, line, CancellationToken.None);

        Assert.Contains("Foo.Start()", report.RootMethod.DisplayName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeAsync_Captures_Unresolved_Call_Sites()
    {
        const string source = """
namespace AppNs;

public class Foo
{
    public void Start(dynamic d)
    {
        d.Run();
    }
}
""";

        var filePath = "/tmp/App/Foo.cs";
        var line = FindLine(source, "d.Run();");
        var solution = CreateSolution(("App", "/tmp/App/App.csproj", filePath, source, Array.Empty<string>()));

        var engine = new MethodDependencyAnalysisEngine();
        var report = await engine.AnalyzeAsync(solution, "/tmp/Test.sln", filePath, line, CancellationToken.None);

        Assert.NotEmpty(report.UnresolvedCallSites);
        Assert.Contains(report.UnresolvedCallSites, u => u.Expression == "d.Run()");
    }

    private static int FindLine(string source, string snippet)
    {
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].Contains(snippet, StringComparison.Ordinal))
            {
                return index + 1;
            }
        }

        throw new InvalidOperationException($"Snippet '{snippet}' was not found in source.");
    }

    private static Solution CreateSolution(params (string Name, string ProjectPath, string DocumentPath, string Source, string[] Dependencies)[] projects)
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
                    filePath: project.ProjectPath,
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

            var documentId = DocumentId.CreateNewId(projectId, debugName: project.DocumentPath);
            solution = solution.AddDocument(DocumentInfo.Create(
                documentId,
                Path.GetFileName(project.DocumentPath),
                filePath: project.DocumentPath,
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(project.Source), VersionStamp.Create()))));
        }

        return solution;
    }
}
