using RefactorCli.Commands.DeadCode.Configuration;
using RefactorCli.Infrastructure;

namespace RefactorCli.Tests;

public sealed class DeadCodeConfigLoaderTests
{
    [Fact]
    public async Task LoadAsync_ParsesExpectedSections()
    {
        var path = Path.Combine(Path.GetTempPath(), $"deadcode-{Guid.NewGuid():N}.yml");
        try
        {
            await File.WriteAllTextAsync(path, """
                frameworks:
                  aspNetMvc:
                    enabled: true
                    controllerSuffix: ApiController
                    nonActionAttributes:
                      - Microsoft.AspNetCore.Mvc.NonActionAttribute
                roots:
                  symbols:
                    - Demo.Program.Main()
                  attributes:
                    - Demo.KeepAttribute
                dynamicUsage:
                  markUnknownIfMatched: true
                  reflectionPatterns:
                    - Assembly.GetType
                suppressions:
                  - symbol: Demo.Legacy.Run()
                    reason: called by plugin
                """);

            var loader = new DeadCodeConfigLoader(new FileSystem());
            var config = await loader.LoadAsync(path, CancellationToken.None);

            Assert.True(config.Frameworks.AspNetMvc.Enabled);
            Assert.Equal("ApiController", config.Frameworks.AspNetMvc.ControllerSuffix);
            Assert.Contains("Demo.Program.Main()", config.Roots.Symbols);
            Assert.Contains("Demo.KeepAttribute", config.Roots.Attributes);
            Assert.Contains("Assembly.GetType", config.DynamicUsage.ReflectionPatterns);
            Assert.Contains(config.Suppressions, s => s.Symbol == "Demo.Legacy.Run()" && s.Reason == "called by plugin");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
