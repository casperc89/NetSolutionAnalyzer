# RefactorCli Documentation

## Goal
`RefactorCli` is a modular .NET CLI for migration analysis.

Current focus: generate a repeatable inventory of `System.Web` usage across a solution to support incremental ASP.NET Core migration planning.

## Current Scope
- Analyze only (no source rewriting).
- Use Roslyn semantic analysis for C# findings.
- Include non-code signals (`web.config` and `.cshtml`) in the same report.
- Produce stable output that can be diffed between runs.

## Architecture

### Solution Layout
- `RefactorCli`: executable entrypoint, host bootstrapping, DI wiring.
- `RefactorCli.Abstractions`: shared generic contracts and runtime primitives (`ICommandModule`, `ICommandHandler<T>`, console/filesystem abstractions, exit codes).
- `RefactorCli.Infrastructure`: generic implementations (`IFileSystem`, `IAppConsole`).
- `RefactorCli.Commands.SystemWebCatalog`: complete `systemweb catalog` feature (command module, handler, analysis pipeline, contracts, report writers).
- `RefactorCli.Tests`: analyzer and ordering tests.
- `RefactorCli.SampleLegacyWeb`: sample legacy-style project for deterministic analyzer coverage.

### SystemWebCatalog Feature Structure
- `Analysis/`
  - Roslyn solution loading (`MSBuildRoslynSolutionLoader`)
  - analyzer contract (`ICatalogAnalyzer`)
  - analyzer implementations (`SW0001`-`SW0005`)
  - finding aggregation (`CatalogAccumulator`)
  - report assembly (`CatalogEngine`)
  - orchestration service (`SystemWebCatalogService`)
- `Contracts/`
  - `SystemWebCatalogOptions`
  - `CatalogReport`, `ProjectReport`, `Finding`
  - `CatalogRuleDescriptor` (rule metadata used by analyzers and reporting)
  - `IReportWriter`
- `Reporting/`
  - `JsonReportWriter`
  - `MarkdownReportWriter`

### Execution Flow
1. CLI parses `systemweb catalog` options.
2. `SystemWebCatalogCommandHandler` validates and invokes `SystemWebCatalogService`.
3. `SystemWebCatalogService` resolves/validates solution path and loads the solution via `MSBuildRoslynSolutionLoader`.
4. `CatalogEngine` runs all registered `ICatalogAnalyzer` instances per project, or only selected rule IDs when `--include-rule/--include-rules` is provided.
5. Findings are deduped and ordered deterministically.
6. `CatalogReport` is produced with project findings and rule descriptors.
7. Selected report writers (`json`, `md`) write files to output directory.

## Command Usage

### Help
```bash
dotnet run --project RefactorCli/RefactorCli.csproj -- --help
dotnet run --project RefactorCli/RefactorCli.csproj -- systemweb catalog --help
```

### Run Catalog
```bash
dotnet run --project RefactorCli/RefactorCli.csproj -- \
  systemweb catalog \
  --solution /path/to/your.sln \
  --output ./refactor-reports \
  --format json \
  --format md
```

### Options
- `--solution <path>`: path to solution file (required).
- `--repo <path>`: reserved for future repo-first flows.
- `--output <path>`: report output directory.
- `--format json|md|sarif`: output formats (`json` and `md` currently implemented).
- `--include-rule <id>` / `--include-rules <id1,id2>`: include only the specified rule IDs.
- `--verbosity quiet|normal|diag`: command verbosity.

### Exit Codes
- `0`: success.
- `2`: invalid arguments.
- `3`: solution load failure.
- `4`: unexpected error.

## Report Model

### `CatalogReport`
- `GeneratedAtUtc`
- `SolutionPath`
- `Rules[]` (`CatalogRuleDescriptor`)
- `Projects[]` (`ProjectReport`)

### `CatalogRuleDescriptor`
- `Id`
- `Title`
- `Category`
- `Severity`
- `WhatItDetects`
- `WhyItMatters`

### `ProjectReport`
- `ProjectName`
- `ProjectPath`
- `TargetFramework`
- `DocumentsAnalyzed`
- `Findings[]`

### `Finding`
- `Id`, `Category`, `Severity`, `Message`
- `Symbol`
- `FilePath`, `Line`, `Column`
- `Snippet`
- `Properties`

## Implemented Rules
- `SW0001` - System.Web namespace imports.
- `SW0002` - Semantic System.Web symbol references in C#.
- `SW0003` - Inheritance/interface use of System.Web framework types.
- `SW0004` - Classic System.Web configuration markers in `*.config`.
- `SW0005` - Heuristic System.Web usage patterns in Razor views.

Rule metadata (what it detects + why it matters) is defined directly in analyzer descriptors and surfaced in markdown reports.

## Extending SystemWebCatalog

### Add a New Analyzer Rule
1. Create a new analyzer class in `RefactorCli.Commands.SystemWebCatalog/Analysis` implementing `ICatalogAnalyzer`.
2. Define a `CatalogRuleDescriptor` with stable `Id`, category/severity, detection intent, and migration rationale.
3. Emit findings through `CatalogAccumulator`.
4. Register the analyzer in `AnalysisServiceCollectionExtensions`.
5. Add/update tests in `RefactorCli.Tests`.

### Add a New Report Format
1. Implement `IReportWriter` in `RefactorCli.Commands.SystemWebCatalog/Reporting`.
2. Set `Format` to the CLI token.
3. Register writer in `SystemWebCatalogCommandModule`.
4. Validate output stability with tests and a sample run.

### Add Another Command Module
1. Create `RefactorCli.Commands.<Feature>` project.
2. Implement `ICommandModule` and one or more `ICommandHandler<TOptions>` handlers.
3. Register via `services.AddModule<YourModule>(rootCommand)` in `RefactorCli/Program.cs`.

## Testing
Run tests:
```bash
dotnet test RefactorCli.Tests/RefactorCli.Tests.csproj
```

Current test coverage includes:
- SW0001 detection.
- SW0002 semantic symbol detection (including fully-qualified usage without `using`).
- deterministic finding ordering in `CatalogEngine`.

## Sample Project
`RefactorCli.SampleLegacyWeb` exists to provide deterministic, in-repo input patterns without relying on the real `System.Web` assembly.
