namespace RefactorCli.Commands.DeadCode.Configuration;

public sealed class DeadCodeConfig
{
    public static DeadCodeConfig Empty { get; } = new();

    public FrameworkConfig Frameworks { get; init; } = new();

    public RootConfig Roots { get; init; } = new();

    public DynamicUsageConfig DynamicUsage { get; init; } = new();

    public IReadOnlyList<SuppressionEntry> Suppressions { get; init; } = [];
}

public sealed class FrameworkConfig
{
    public AspNetMvcConfig AspNetMvc { get; init; } = new();
}

public sealed class AspNetMvcConfig
{
    public bool Enabled { get; init; } = true;

    public string ControllerSuffix { get; init; } = "Controller";

    public IReadOnlyList<string> ActionAttributes { get; init; } =
    [
        "Microsoft.AspNetCore.Mvc.HttpGetAttribute",
        "Microsoft.AspNetCore.Mvc.HttpPostAttribute",
        "Microsoft.AspNetCore.Mvc.HttpPutAttribute",
        "Microsoft.AspNetCore.Mvc.HttpPatchAttribute",
        "Microsoft.AspNetCore.Mvc.HttpDeleteAttribute",
        "Microsoft.AspNetCore.Mvc.RouteAttribute"
    ];

    public IReadOnlyList<string> NonActionAttributes { get; init; } =
    [
        "Microsoft.AspNetCore.Mvc.NonActionAttribute"
    ];
}

public sealed class RootConfig
{
    public IReadOnlyList<string> Symbols { get; init; } = [];

    public IReadOnlyList<string> Attributes { get; init; } = [];
}

public sealed class DynamicUsageConfig
{
    public bool MarkUnknownIfMatched { get; init; } = true;

    public IReadOnlyList<string> ReflectionPatterns { get; init; } = [];
}

public sealed class SuppressionEntry
{
    public string? Symbol { get; init; }

    public string? Reason { get; init; }
}
