using RefactorCli.Commands.SystemWebCatalog.Contracts;
using RefactorCli.Commands.SystemWebCatalog.Reporting;
using RefactorCli.Infrastructure;

namespace RefactorCli.Tests;

public class MarkdownReportWriterTests
{
    [Fact]
    public async Task WriteAsync_Lists_Findings_Per_Rule_For_Each_Project()
    {
        var report = new CatalogReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            SolutionPath = "/repo/sample.sln",
            Rules =
            [
                new CatalogRuleDescriptor
                {
                    Id = "SW0001",
                    Title = "System.Web using",
                    Category = "Namespace",
                    Severity = "Info",
                    WhatItDetects = "Detects System.Web namespace imports.",
                    WhyItMatters = "Identifies direct namespace dependency."
                },
                new CatalogRuleDescriptor
                {
                    Id = "SW0002",
                    Title = "System.Web symbol usage",
                    Category = "API",
                    Severity = "Warning",
                    WhatItDetects = "Detects semantic symbol references.",
                    WhyItMatters = "Identifies API migration scope."
                }
            ],
            Projects =
            [
                new ProjectReport
                {
                    ProjectName = "Legacy.Web",
                    ProjectPath = "/repo/Legacy.Web/Legacy.Web.csproj",
                    TargetFramework = "net48",
                    DocumentsAnalyzed = 3,
                    Findings =
                    [
                        CreateFinding("SW0001", "Legacy.Web/HomeController.cs"),
                        CreateFinding("SW0001", "Legacy.Web/AccountController.cs"),
                        CreateFinding("SW0002", "Legacy.Web/Global.asax.cs")
                    ]
                },
                new ProjectReport
                {
                    ProjectName = "Legacy.Empty",
                    ProjectPath = "/repo/Legacy.Empty/Legacy.Empty.csproj",
                    TargetFramework = "net48",
                    DocumentsAnalyzed = 1,
                    Findings = []
                }
            ]
        };

        var outputDir = Path.Combine(Path.GetTempPath(), "refactorcli-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var writer = new MarkdownReportWriter(new FileSystem());
            var reportPath = await writer.WriteAsync(report, outputDir, CancellationToken.None);
            var markdown = await File.ReadAllTextAsync(reportPath);

            Assert.Contains("### Findings by Rule per Project", markdown, StringComparison.Ordinal);
            Assert.Contains("#### Legacy.Web", markdown, StringComparison.Ordinal);
            Assert.Contains("| SW0001 | 2 |", markdown, StringComparison.Ordinal);
            Assert.Contains("| SW0002 | 1 |", markdown, StringComparison.Ordinal);
            Assert.Contains("#### Legacy.Empty", markdown, StringComparison.Ordinal);
            Assert.Contains("| _None_ | 0 |", markdown, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    private static Finding CreateFinding(string id, string filePath)
    {
        return new Finding
        {
            Id = id,
            Category = "Test",
            Severity = "Info",
            Message = "Test finding",
            FilePath = filePath
        };
    }
}
