using RefactorCli.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RefactorCli.Commands.DeadCode.Configuration;

public sealed class DeadCodeConfigLoader
{
    private readonly IFileSystem _fileSystem;
    private readonly IDeserializer _deserializer;

    public DeadCodeConfigLoader(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public Task<DeadCodeConfig> LoadAsync(string? path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(DeadCodeConfig.Empty);
        }

        var fullPath = _fileSystem.GetFullPath(path);
        if (!_fileSystem.FileExists(fullPath))
        {
            throw new InvalidCommandOptionsException($"Dead code config not found: {fullPath}");
        }

        var text = _fileSystem.ReadAllText(fullPath);
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(DeadCodeConfig.Empty);
        }

        var dto = _deserializer.Deserialize<DeadCodeConfigDto>(text) ?? new DeadCodeConfigDto();
        return Task.FromResult(dto.ToModel());
    }

    private sealed class DeadCodeConfigDto
    {
        public FrameworkConfigDto? Frameworks { get; init; }

        public RootConfigDto? Roots { get; init; }

        public DynamicUsageConfigDto? DynamicUsage { get; init; }

        public List<SuppressionEntryDto>? Suppressions { get; init; }

        public DeadCodeConfig ToModel()
        {
            return new DeadCodeConfig
            {
                Frameworks = Frameworks?.ToModel() ?? new FrameworkConfig(),
                Roots = Roots?.ToModel() ?? new RootConfig(),
                DynamicUsage = DynamicUsage?.ToModel() ?? new DynamicUsageConfig(),
                Suppressions = (Suppressions ?? [])
                    .Select(s => s.ToModel())
                    .Where(s => !string.IsNullOrWhiteSpace(s.Symbol))
                    .ToList()
            };
        }
    }

    private sealed class FrameworkConfigDto
    {
        public AspNetMvcConfigDto? AspNetMvc { get; init; }

        public FrameworkConfig ToModel()
            => new() { AspNetMvc = AspNetMvc?.ToModel() ?? new AspNetMvcConfig() };
    }

    private sealed class AspNetMvcConfigDto
    {
        public bool? Enabled { get; init; }

        public string? ControllerSuffix { get; init; }

        public List<string>? ActionAttributes { get; init; }

        public List<string>? NonActionAttributes { get; init; }

        public AspNetMvcConfig ToModel()
        {
            var defaults = new AspNetMvcConfig();
            return new AspNetMvcConfig
            {
                Enabled = Enabled ?? true,
                ControllerSuffix = string.IsNullOrWhiteSpace(ControllerSuffix) ? "Controller" : ControllerSuffix,
                ActionAttributes = NormalizeOrDefault(ActionAttributes, defaults.ActionAttributes),
                NonActionAttributes = NormalizeOrDefault(NonActionAttributes, defaults.NonActionAttributes)
            };
        }

        private static IReadOnlyList<string> NormalizeOrDefault(List<string>? values, IReadOnlyList<string> defaults)
        {
            if (values is null)
            {
                return defaults;
            }

            return values
                .Select(v => v?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }

    private sealed class RootConfigDto
    {
        public List<string>? Symbols { get; init; }

        public List<string>? Attributes { get; init; }

        public RootConfig ToModel()
        {
            return new RootConfig
            {
                Symbols = Normalize(Symbols),
                Attributes = Normalize(Attributes)
            };
        }

        private static IReadOnlyList<string> Normalize(List<string>? values)
        {
            if (values is null || values.Count == 0)
            {
                return [];
            }

            return values
                .Select(v => v?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }

    private sealed class DynamicUsageConfigDto
    {
        public bool? MarkUnknownIfMatched { get; init; }

        public List<string>? ReflectionPatterns { get; init; }

        public DynamicUsageConfig ToModel()
        {
            return new DynamicUsageConfig
            {
                MarkUnknownIfMatched = MarkUnknownIfMatched ?? true,
                ReflectionPatterns = Normalize(ReflectionPatterns)
            };
        }

        private static IReadOnlyList<string> Normalize(List<string>? values)
        {
            if (values is null || values.Count == 0)
            {
                return [];
            }

            return values
                .Select(v => v?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }

    private sealed class SuppressionEntryDto
    {
        public string? Symbol { get; init; }

        public string? Reason { get; init; }

        public SuppressionEntry ToModel()
            => new() { Symbol = Symbol?.Trim(), Reason = Reason?.Trim() };
    }
}
