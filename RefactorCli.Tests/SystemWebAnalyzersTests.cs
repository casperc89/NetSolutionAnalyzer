using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging.Abstractions;
using RefactorCli.Abstractions;
using RefactorCli.Commands.SystemWebCatalog.Analysis;
using RefactorCli.Commands.SystemWebCatalog.Analyzers;
using RefactorCli.Commands.SystemWebCatalog.Contracts;

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

        Assert.Contains(acc.Findings, f => f.Id == "SW0002" && f.Symbol is not null && f.Symbol.Contains("HttpContext", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SW0101_Finds_HttpApplication_And_Lifecycle_Handler_Methods()
    {
        var project = CreateProject("""
            namespace System.Web
            {
                public class HttpApplication {}
            }

            namespace Demo;

            public sealed class Global : System.Web.HttpApplication
            {
                protected void Application_BeginRequest() {}
            }
            """);

        var analyzer = new HttpApplicationLifecycleCatalogAnalyzer();
        var acc = new CatalogAccumulator();
        await analyzer.AnalyzeAsync(project, acc, CancellationToken.None);

        Assert.Contains(acc.Findings, f => f.Id == "SW0101" && f.Symbol == "System.Web.HttpApplication");
        Assert.Contains(acc.Findings, f => f.Id == "SW0101" && f.Symbol == "Application_BeginRequest");
    }

    [Fact]
    public async Task SW0100_Finds_HttpContextCurrent_And_Ambient_Chain_Members()
    {
        var project = CreateProject("""
            namespace System.Web
            {
                public class HttpRequest {}
                public class HttpServerUtility {}
                public class HttpContext
                {
                    public static HttpContext Current => new();
                    public HttpRequest Request => new();
                    public HttpServerUtility Server => new();
                }
            }

            namespace Demo;

            public class C
            {
                public void M()
                {
                    _ = System.Web.HttpContext.Current;
                    _ = System.Web.HttpContext.Current.Request;
                    _ = System.Web.HttpContext.Current.Server;
                }
            }
            """);

        var analyzer = new HttpContextCurrentCatalogAnalyzer();
        var acc = new CatalogAccumulator();
        await analyzer.AnalyzeAsync(project, acc, CancellationToken.None);

        Assert.Contains(acc.Findings, f => f.Id == "SW0100" && f.Symbol == "System.Web.HttpContext.Current");
        Assert.Contains(acc.Findings, f => f.Id == "SW0100" && f.Symbol == "System.Web.HttpContext.Current.Request");
        Assert.Contains(acc.Findings, f => f.Id == "SW0100" && f.Symbol == "System.Web.HttpContext.Current.Server");
        Assert.Equal(3, acc.Findings.Count(f => f.Id == "SW0100"));
    }

    [Fact]
    public async Task SW0100_DoesNotDuplicate_Current_When_Reporting_CurrentSession()
    {
        var project = CreateProject("""
            namespace System.Web
            {
                public class HttpSessionState {}
                public class HttpContext
                {
                    public static HttpContext Current => new();
                    public HttpSessionState Session => new();
                }
            }

            namespace Demo;

            public class C
            {
                public void M()
                {
                    _ = System.Web.HttpContext.Current.Session;
                }
            }
            """);

        var analyzer = new HttpContextCurrentCatalogAnalyzer();
        var acc = new CatalogAccumulator();
        await analyzer.AnalyzeAsync(project, acc, CancellationToken.None);

        Assert.DoesNotContain(acc.Findings, f => f.Id == "SW0100" && f.Symbol == "System.Web.HttpContext.Current");
        Assert.Contains(acc.Findings, f => f.Id == "SW0100" && f.Symbol == "System.Web.HttpContext.Current.Session");
        Assert.Equal(1, acc.Findings.Count(f => f.Id == "SW0100"));
    }

    [Fact]
    public async Task SW0100_DoesNotFlag_NonSystemWeb_HttpContext()
    {
        var project = CreateProject("""
            namespace Demo;

            public class HttpContext
            {
                public static HttpContext Current => new();
                public string Request => "/";
            }

            public class C
            {
                public void M()
                {
                    _ = HttpContext.Current.Request;
                }
            }
            """);

        var analyzer = new HttpContextCurrentCatalogAnalyzer();
        var acc = new CatalogAccumulator();
        await analyzer.AnalyzeAsync(project, acc, CancellationToken.None);

        Assert.DoesNotContain(acc.Findings, f => f.Id == "SW0100");
    }

    [Fact]
    public async Task SW0104_Finds_ServerMapPath_And_HttpContextCurrentServer()
    {
        var project = CreateProject("""
            namespace System.Web
            {
                public class HttpServerUtility
                {
                    public string MapPath(string path) => path;
                }

                public class HttpContext
                {
                    public static HttpContext Current => new();
                    public HttpServerUtility Server => new();
                }
            }

            namespace Demo;

            public class C
            {
                public string Resolve() => System.Web.HttpContext.Current.Server.MapPath("~/content");
            }
            """);

        var analyzer = new ServerMapPathCatalogAnalyzer();
        var acc = new CatalogAccumulator();
        await analyzer.AnalyzeAsync(project, acc, CancellationToken.None);

        Assert.Contains(acc.Findings, f => f.Id == "SW0104" && f.Symbol == "System.Web.HttpContext.Server");
        Assert.Contains(acc.Findings, f => f.Id == "SW0104" && f.Symbol == "System.Web.HttpServerUtility.MapPath");
    }

    [Fact]
    public async Task SW0400_Finds_Session_Across_Common_Shapes()
    {
        var project = CreateProject("""
            namespace System.Web.SessionState
            {
                public class SessionStateItemCollection {}
            }

            namespace System.Web
            {
                public class HttpSessionState
                {
                    public object this[string key]
                    {
                        get => key;
                        set {}
                    }
                }

                public class HttpContext
                {
                    public static HttpContext Current => new();
                    public HttpSessionState Session => new();
                }
            }

            namespace System.Web.Mvc
            {
                public abstract class Controller
                {
                    public System.Web.HttpSessionState Session => new();
                }
            }

            namespace Demo
            {
                public static class SessionExtensions
                {
                    public static string? GetString(this System.Web.HttpSessionState session, string key) => null;
                }

                public sealed class C : System.Web.Mvc.Controller
                {
                    public void M()
                    {
                        Session["K"] = "V";
                        _ = Session.GetString("K");
                        _ = System.Web.HttpContext.Current.Session;
                        System.Web.SessionState.SessionStateItemCollection? items = null;
                    }
                }
            }
            """);

        var analyzer = new SessionUsageCatalogAnalyzer();
        var acc = new CatalogAccumulator();
        await analyzer.AnalyzeAsync(project, acc, CancellationToken.None);

        Assert.Contains(acc.Findings, f => f.Id == "SW0400" && f.Symbol == "System.Web.Mvc.Controller.Session");
        Assert.Contains(acc.Findings, f => f.Id == "SW0400" && f.Symbol == "System.Web.HttpSessionState.this[]");
        Assert.Contains(acc.Findings, f => f.Id == "SW0400" && f.Symbol == "System.Web.HttpContext.Current.Session");
        Assert.Contains(acc.Findings, f => f.Id == "SW0400" && f.Symbol is not null && f.Symbol.Contains(".GetString(...)", StringComparison.Ordinal));
        Assert.Contains(acc.Findings, f => f.Id == "SW0400" && f.Symbol == "System.Web.SessionState.SessionStateItemCollection");
    }

    [Fact]
    public async Task SW0401_Finds_Session_Reads_And_Captures_Keys()
    {
        var project = CreateProject("""
            namespace System.Web
            {
                public class HttpSessionState
                {
                    public object this[string key]
                    {
                        get => key;
                        set {}
                    }
                }
            }

            namespace System.Web.Mvc
            {
                public abstract class Controller
                {
                    public System.Web.HttpSessionState Session => new();
                }
            }

            namespace Demo
            {
                public sealed class C : System.Web.Mvc.Controller
                {
                    public void M(string dynamicKey)
                    {
                        const string constKey = "ConstRead";
                        _ = Session["LiteralRead"];
                        _ = Session[constKey];
                        _ = Session[dynamicKey];
                        Session["WriteOnly"] = "v";
                    }
                }
            }
            """);

        var analyzer = new SessionReadCatalogAnalyzer();
        var acc = new CatalogAccumulator();
        await analyzer.AnalyzeAsync(project, acc, CancellationToken.None);

        Assert.Contains(acc.Findings, f => f.Id == "SW0401" &&
                                           f.Symbol == "System.Web.HttpSessionState.this[]" &&
                                           f.Properties is not null &&
                                           f.Properties.TryGetValue("sessionKey", out var key) &&
                                           key == "LiteralRead");
        Assert.Contains(acc.Findings, f => f.Id == "SW0401" &&
                                           f.Properties is not null &&
                                           f.Properties.TryGetValue("sessionKey", out var key) &&
                                           key == "ConstRead");
        Assert.Contains(acc.Findings, f => f.Id == "SW0401" &&
                                           f.Properties is not null &&
                                           f.Properties.TryGetValue("sessionKey", out var key) &&
                                           key == "<dynamic>");
        Assert.DoesNotContain(acc.Findings, f => f.Id == "SW0401" && f.Snippet == "Session[\"WriteOnly\"]");
    }

    [Fact]
    public async Task SW0402_Finds_Session_Writes_And_Captures_Keys()
    {
        var project = CreateProject("""
            namespace System.Web
            {
                public class HttpSessionState
                {
                    public object this[string key]
                    {
                        get => key;
                        set {}
                    }
                }
            }

            namespace System.Web.Mvc
            {
                public abstract class Controller
                {
                    public System.Web.HttpSessionState Session => new();
                }
            }

            namespace Demo
            {
                public sealed class C : System.Web.Mvc.Controller
                {
                    public void M(string dynamicKey)
                    {
                        const string constKey = "ConstWrite";
                        Session["LiteralWrite"] = "v1";
                        Session[constKey] = "v2";
                        Session[dynamicKey] = "v3";
                        _ = Session["ReadOnly"];
                    }
                }
            }
            """);

        var analyzer = new SessionWriteCatalogAnalyzer();
        var acc = new CatalogAccumulator();
        await analyzer.AnalyzeAsync(project, acc, CancellationToken.None);

        Assert.Contains(acc.Findings, f => f.Id == "SW0402" &&
                                           f.Symbol == "System.Web.HttpSessionState.this[]" &&
                                           f.Properties is not null &&
                                           f.Properties.TryGetValue("sessionKey", out var key) &&
                                           key == "LiteralWrite");
        Assert.Contains(acc.Findings, f => f.Id == "SW0402" &&
                                           f.Properties is not null &&
                                           f.Properties.TryGetValue("sessionKey", out var key) &&
                                           key == "ConstWrite");
        Assert.Contains(acc.Findings, f => f.Id == "SW0402" &&
                                           f.Properties is not null &&
                                           f.Properties.TryGetValue("sessionKey", out var key) &&
                                           key == "<dynamic>");
        Assert.DoesNotContain(acc.Findings, f => f.Id == "SW0402" && f.Snippet == "Session[\"ReadOnly\"]");
    }

    [Fact]
    public async Task SW0400_0401_0402_Find_RealWorld_HttpSessionState_And_HttpSessionStateBase_Patterns()
    {
        var project = CreateProject("""
            namespace System.Web.SessionState
            {
                public class HttpSessionState
                {
                    public object this[string key]
                    {
                        get => key;
                        set {}
                    }
                }
            }

            namespace System.Web
            {
                public class HttpSessionStateBase
                {
                    public virtual object this[string key]
                    {
                        get => key;
                        set {}
                    }
                }

                public class HttpContext
                {
                    public static HttpContext Current => new();
                    public System.Web.SessionState.HttpSessionState Session => new();
                }
            }

            namespace System.Web.Mvc
            {
                public class HttpContextBase
                {
                    public System.Web.HttpSessionStateBase Session => new();
                }

                public abstract class Controller
                {
                    public HttpContextBase HttpContext => new();
                }
            }

            namespace Demo
            {
                public static class K
                {
                    public const string LANG = "KAYTTOLIITTYMA_KIELI";
                    public const string PREFIX = "ONMINIKILPAILUTUS_";
                }

                public class AmbientUsage
                {
                    public void M(int id)
                    {
                        string language = (string)System.Web.HttpContext.Current.Session[K.LANG];
                        if (System.Web.HttpContext.Current.Session[K.PREFIX + id] == null) {}
                        System.Web.HttpContext.Current.Session["PROCUREMENT_DECISIONID"] = id;
                        System.Web.SessionState.HttpSessionState session = System.Web.HttpContext.Current.Session;
                        _ = session[K.LANG];
                    }
                }

                public class ControllerUsage : System.Web.Mvc.Controller
                {
                    public void M()
                    {
                        HttpContext.Session[K.LANG] = "fi";
                        HttpContext.Session["Moduulit"] = new object();
                        string lang = (string)HttpContext.Session[K.LANG];
                    }
                }
            }
            """);

        var usageAnalyzer = new SessionUsageCatalogAnalyzer();
        var readAnalyzer = new SessionReadCatalogAnalyzer();
        var writeAnalyzer = new SessionWriteCatalogAnalyzer();

        var usageAcc = new CatalogAccumulator();
        var readAcc = new CatalogAccumulator();
        var writeAcc = new CatalogAccumulator();

        await usageAnalyzer.AnalyzeAsync(project, usageAcc, CancellationToken.None);
        await readAnalyzer.AnalyzeAsync(project, readAcc, CancellationToken.None);
        await writeAnalyzer.AnalyzeAsync(project, writeAcc, CancellationToken.None);

        Assert.Contains(usageAcc.Findings, f => f.Id == "SW0400" && f.Symbol == "System.Web.HttpContext.Current.Session");
        Assert.Contains(usageAcc.Findings, f => f.Id == "SW0400" && f.Symbol == "System.Web.SessionState.HttpSessionState.this[]");
        Assert.Contains(usageAcc.Findings, f => f.Id == "SW0400" && f.Symbol == "System.Web.HttpSessionStateBase.this[]");

        Assert.Contains(readAcc.Findings, f => f.Id == "SW0401" &&
                                               f.Properties is not null &&
                                               f.Properties.TryGetValue("sessionKey", out var key) &&
                                               key == "KAYTTOLIITTYMA_KIELI");
        Assert.Contains(readAcc.Findings, f => f.Id == "SW0401" &&
                                               f.Properties is not null &&
                                               f.Properties.TryGetValue("sessionKey", out var key) &&
                                               key == "<dynamic>");

        Assert.Contains(writeAcc.Findings, f => f.Id == "SW0402" &&
                                                f.Properties is not null &&
                                                f.Properties.TryGetValue("sessionKey", out var key) &&
                                                key == "PROCUREMENT_DECISIONID");
        Assert.Contains(writeAcc.Findings, f => f.Id == "SW0402" &&
                                                f.Properties is not null &&
                                                f.Properties.TryGetValue("sessionKey", out var key) &&
                                                key == "Moduulit");
    }

    [Fact]
    public async Task SW0500_Finds_HttpPostedFileBase_RequestFiles_And_InputStream()
    {
        var project = CreateProject("""
            namespace System.Web
            {
                public class HttpPostedFileBase
                {
                    public System.IO.Stream InputStream => System.IO.Stream.Null;
                }

                public class HttpFileCollectionBase {}

                public class HttpRequest
                {
                    public HttpFileCollectionBase Files => new();
                }
            }

            namespace Demo
            {
                public sealed class C
                {
                    public void M(System.Web.HttpPostedFileBase file, System.Web.HttpRequest request)
                    {
                        _ = file.InputStream;
                        _ = request.Files;
                    }
                }
            }
            """);

        var analyzer = new PostedFileCatalogAnalyzer();
        var acc = new CatalogAccumulator();
        await analyzer.AnalyzeAsync(project, acc, CancellationToken.None);

        Assert.Contains(acc.Findings, f => f.Id == "SW0500" && f.Symbol == "System.Web.HttpPostedFileBase");
        Assert.Contains(acc.Findings, f => f.Id == "SW0500" && f.Symbol == "System.Web.HttpPostedFileBase.InputStream");
        Assert.Contains(acc.Findings, f => f.Id == "SW0500" && f.Symbol == "System.Web.HttpRequest.Files");
    }

    [Fact]
    public async Task SW0702_Finds_RequestResponseCookies_And_HttpCookie()
    {
        var project = CreateProject("""
            namespace System.Web
            {
                public class HttpCookie
                {
                    public HttpCookie(string name, string value) {}
                }

                public class HttpCookieCollection
                {
                    public HttpCookie this[string name]
                    {
                        get => new(name, "v");
                    }
                }

                public class HttpRequest
                {
                    public HttpCookieCollection Cookies => new();
                }

                public class HttpResponse
                {
                    public HttpCookieCollection Cookies => new();
                }
            }

            namespace Demo
            {
                public sealed class C
                {
                    public void M(System.Web.HttpRequest req, System.Web.HttpResponse res)
                    {
                        _ = req.Cookies["Auth"];
                        _ = res.Cookies["Auth"];
                        var c = new System.Web.HttpCookie("Theme", "Dark");
                    }
                }
            }
            """);

        var analyzer = new HeadersCookiesCatalogAnalyzer();
        var acc = new CatalogAccumulator();
        await analyzer.AnalyzeAsync(project, acc, CancellationToken.None);

        Assert.Contains(acc.Findings, f => f.Id == "SW0702" && f.Symbol == "System.Web.HttpRequest.Cookies");
        Assert.Contains(acc.Findings, f => f.Id == "SW0702" && f.Symbol == "System.Web.HttpResponse.Cookies");
        Assert.Contains(acc.Findings, f => f.Id == "SW0702" && f.Symbol == "System.Web.HttpCookie");
    }

    [Fact]
    public async Task SW0702_DoesNotFlag_NonSystemWeb_Cookies()
    {
        var project = CreateProject("""
            namespace Demo;

            public class HttpCookie {}

            public class HttpRequest
            {
                public string Cookies => "local";
            }

            public class C
            {
                public void M(HttpRequest req)
                {
                    _ = req.Cookies;
                    _ = new HttpCookie();
                }
            }
            """);

        var analyzer = new HeadersCookiesCatalogAnalyzer();
        var acc = new CatalogAccumulator();
        await analyzer.AnalyzeAsync(project, acc, CancellationToken.None);

        Assert.DoesNotContain(acc.Findings, f => f.Id == "SW0702");
    }

    [Fact]
    public async Task SW0703_Finds_Cookie_Reads_And_Captures_Keys()
    {
        var project = CreateProject("""
            namespace System.Web
            {
                public class HttpCookie {}

                public class HttpCookieCollection
                {
                    public HttpCookie this[string name]
                    {
                        get => new();
                        set {}
                    }
                }

                public class HttpRequest
                {
                    public HttpCookieCollection Cookies => new();
                }
            }

            namespace Demo
            {
                public class C
                {
                    public void M(System.Web.HttpRequest req, string dynamicKey)
                    {
                        const string constKey = "ConstRead";
                        _ = req.Cookies["Auth"];
                        _ = req.Cookies[constKey];
                        _ = req.Cookies[dynamicKey];
                        req.Cookies["WriteOnly"] = new System.Web.HttpCookie();
                    }
                }
            }
            """);

        var analyzer = new CookieReadCatalogAnalyzer();
        var acc = new CatalogAccumulator();
        await analyzer.AnalyzeAsync(project, acc, CancellationToken.None);

        Assert.Contains(acc.Findings, f => f.Id == "SW0703" &&
                                           f.Symbol == "System.Web.HttpCookieCollection.this[]" &&
                                           f.Properties is not null &&
                                           f.Properties.TryGetValue("cookieKey", out var key) &&
                                           key == "Auth");
        Assert.Contains(acc.Findings, f => f.Id == "SW0703" &&
                                           f.Properties is not null &&
                                           f.Properties.TryGetValue("cookieKey", out var key) &&
                                           key == "ConstRead");
        Assert.Contains(acc.Findings, f => f.Id == "SW0703" &&
                                           f.Properties is not null &&
                                           f.Properties.TryGetValue("cookieKey", out var key) &&
                                           key == "<dynamic>");
        Assert.DoesNotContain(acc.Findings, f => f.Id == "SW0703" && f.Snippet == "req.Cookies[\"WriteOnly\"]");
    }

    [Fact]
    public async Task SW0704_Finds_Cookie_Writes_And_Captures_Keys()
    {
        var project = CreateProject("""
            namespace System.Web
            {
                public class HttpCookie {}

                public class HttpCookieCollection
                {
                    public HttpCookie this[string name]
                    {
                        get => new();
                        set {}
                    }
                }

                public class HttpResponse
                {
                    public HttpCookieCollection Cookies => new();
                }
            }

            namespace Demo
            {
                public class C
                {
                    public void M(System.Web.HttpResponse res, string dynamicKey)
                    {
                        const string constKey = "ConstWrite";
                        res.Cookies["Auth"] = new System.Web.HttpCookie();
                        res.Cookies[constKey] = new System.Web.HttpCookie();
                        res.Cookies[dynamicKey] = new System.Web.HttpCookie();
                        _ = res.Cookies["ReadOnly"];
                    }
                }
            }
            """);

        var analyzer = new CookieWriteCatalogAnalyzer();
        var acc = new CatalogAccumulator();
        await analyzer.AnalyzeAsync(project, acc, CancellationToken.None);

        Assert.Contains(acc.Findings, f => f.Id == "SW0704" &&
                                           f.Symbol == "System.Web.HttpCookieCollection.this[]" &&
                                           f.Properties is not null &&
                                           f.Properties.TryGetValue("cookieKey", out var key) &&
                                           key == "Auth");
        Assert.Contains(acc.Findings, f => f.Id == "SW0704" &&
                                           f.Properties is not null &&
                                           f.Properties.TryGetValue("cookieKey", out var key) &&
                                           key == "ConstWrite");
        Assert.Contains(acc.Findings, f => f.Id == "SW0704" &&
                                           f.Properties is not null &&
                                           f.Properties.TryGetValue("cookieKey", out var key) &&
                                           key == "<dynamic>");
        Assert.DoesNotContain(acc.Findings, f => f.Id == "SW0704" && f.Snippet == "res.Cookies[\"ReadOnly\"]");
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
        var report = await engine.AnalyzeAsync(project.Solution, "/tmp/test.sln", [], CancellationToken.None);

        var findings = report.Projects.Single().Findings;
        var first = findings.First();
        Assert.Equal("SWX001", first.Id);
        Assert.EndsWith("a.cs", first.FilePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatalogEngine_IncludedRules_RunsOnlyRequestedAnalyzers()
    {
        var project = CreateProject("""
            namespace Demo;
            public class C {}
            """);

        var engine = new CatalogEngine(
            [new DeterministicOrderProbeAnalyzer(), new SecondaryProbeAnalyzer()],
            NullLogger<CatalogEngine>.Instance);

        var report = await engine.AnalyzeAsync(project.Solution, "/tmp/test.sln", ["swx001"], CancellationToken.None);
        var findings = report.Projects.Single().Findings;

        Assert.All(findings, finding => Assert.Equal("SWX001", finding.Id));
        Assert.Equal(["SWX001"], report.Rules.Select(rule => rule.Id).ToArray());
    }

    [Fact]
    public async Task CatalogEngine_IncludedRules_UnknownRule_Throws()
    {
        var project = CreateProject("""
            namespace Demo;
            public class C {}
            """);

        var engine = new CatalogEngine([new DeterministicOrderProbeAnalyzer()], NullLogger<CatalogEngine>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidCommandOptionsException>(
            () => engine.AnalyzeAsync(project.Solution, "/tmp/test.sln", ["SW9999"], CancellationToken.None));

        Assert.Contains("Unknown rule ID(s): SW9999", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Available rule IDs: SWX001", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatalogEngine_ExcludeTestProjects_False_Includes_DotTests_Projects()
    {
        var solution = CreateSolution(
            ("App", "namespace AppNs; public class AppType {}", []),
            ("App.Tests", "namespace AppTestsNs; public class AppTestsType {}", []));

        var engine = new CatalogEngine([new DeterministicOrderProbeAnalyzer()], NullLogger<CatalogEngine>.Instance);
        var report = await engine.AnalyzeAsync(solution, "/tmp/test.sln", [], excludeTestProjects: false, CancellationToken.None);

        Assert.Contains(report.Projects, project => project.ProjectName == "App");
        Assert.Contains(report.Projects, project => project.ProjectName == "App.Tests");
    }

    [Fact]
    public async Task CatalogEngine_ExcludeTestProjects_True_Excludes_DotTests_Projects()
    {
        var solution = CreateSolution(
            ("App", "namespace AppNs; public class AppType {}", []),
            ("App.Tests", "namespace AppTestsNs; public class AppTestsType {}", []));

        var engine = new CatalogEngine([new DeterministicOrderProbeAnalyzer()], NullLogger<CatalogEngine>.Instance);
        var report = await engine.AnalyzeAsync(solution, "/tmp/test.sln", [], excludeTestProjects: true, CancellationToken.None);

        Assert.Contains(report.Projects, project => project.ProjectName == "App");
        Assert.DoesNotContain(report.Projects, project => project.ProjectName == "App.Tests");
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
                    parseOptions: new CSharpParseOptions(LanguageVersion.CSharp12)))
                .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
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

    private sealed class DeterministicOrderProbeAnalyzer : ICatalogAnalyzer
    {
        public CatalogRuleDescriptor Descriptor { get; } = new()
        {
            Id = "SWX001",
            Title = "Probe",
            Category = "Member",
            Severity = "Info",
            WhatItDetects = "Test probe finding.",
            WhyItMatters = "Used to validate deterministic ordering."
        };

        public Task AnalyzeAsync(Project project, CatalogAccumulator acc, CancellationToken ct)
        {
            foreach (var doc in project.Documents.OrderByDescending(d => d.Name, StringComparer.Ordinal))
            {
                acc.Add(
                    id: Descriptor.Id,
                    category: Descriptor.Category,
                    severity: Descriptor.Severity,
                    message: "probe",
                    filePath: doc.Name,
                    line: 10,
                    column: 1,
                    symbol: doc.Name);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class SecondaryProbeAnalyzer : ICatalogAnalyzer
    {
        public CatalogRuleDescriptor Descriptor { get; } = new()
        {
            Id = "SWX002",
            Title = "Probe 2",
            Category = "Member",
            Severity = "Info",
            WhatItDetects = "Secondary test probe finding.",
            WhyItMatters = "Used to validate include-rule filtering."
        };

        public Task AnalyzeAsync(Project project, CatalogAccumulator acc, CancellationToken ct)
        {
            foreach (var doc in project.Documents)
            {
                acc.Add(
                    id: Descriptor.Id,
                    category: Descriptor.Category,
                    severity: Descriptor.Severity,
                    message: "probe2",
                    filePath: doc.Name,
                    line: 11,
                    column: 1,
                    symbol: doc.Name);
            }

            return Task.CompletedTask;
        }
    }
}
