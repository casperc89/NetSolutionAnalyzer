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
- `RefactorCli.Commands.DependencyGraph`: `dependency graph` feature for solution project graphing, postorder upgrade sequencing, and transitive upstream class dependency inventory.
- `RefactorCli.Tests`: analyzer and ordering tests.
- `RefactorCli.SampleLegacyWeb`: sample legacy-style project for deterministic analyzer coverage.

### SystemWebCatalog Feature Structure
- `Analysis/`
  - Roslyn solution loading (`MSBuildRoslynSolutionLoader`)
  - analyzer contract (`ICatalogAnalyzer`)
  - analyzer implementations (`SW0001`-`SW0704`)
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
dotnet run --project RefactorCli/RefactorCli.csproj -- dependency graph --help
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

### Run Dependency Graph
```bash
dotnet run --project RefactorCli/RefactorCli.csproj -- \
  dependency graph \
  --solution /path/to/your.sln \
  --output ./refactor-reports \
  --format json \
  --format md \
  --max-classes-per-project 100
```

### Options
- `--solution <path>`: path to solution file (required).
- `--output <path>`: report output directory.
- `--format json|md`: output formats.
- `--include-rule <id>` / `--include-rules <id1,id2>`: include only the specified rule IDs.
- `--max-classes-per-project <n>`: dependency graph markdown truncation limit for class listings.
- `--exclude-test-projects`: exclude projects with names ending in `.Tests` (supported by `systemweb catalog` and `dependency graph`).
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
- `SW0100` - `HttpContext.Current` ambient context access.
- `SW0101` - `HttpApplication` lifecycle handlers and request event methods.
- `SW0104` - `Server.MapPath` and `HttpServerUtility` usage.
- `SW0400` - Broad session usage patterns (`Session`, `HttpContext.Current.Session`, session collection types, extension usage).
- `SW0401` - Session key reads (`Session[key]`) with captured `sessionKey`.
- `SW0402` - Session key writes (`Session[key] = ...`) with captured `sessionKey`.
- `SW0500` - Legacy posted-file APIs (`HttpPostedFileBase`, `Request.Files`, `InputStream`).
- `SW0702` - Legacy cookie APIs (`Request.Cookies`, `Response.Cookies`, `HttpCookie`).
- `SW0703` - Cookie key reads (`Cookies[key]`) with captured `cookieKey`.
- `SW0704` - Cookie key writes (`Cookies[key] = ...`) with captured `cookieKey`.

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
3. Register the module in the `modules` array in `RefactorCli/Program.cs` so its services and commands are wired during startup.

## Testing
Run tests:
```bash
dotnet test RefactorCli.Tests/RefactorCli.Tests.csproj
```

Current test coverage includes:
- representative System.Web analyzer rule detection (including ambient context, lifecycle handlers, session, cookies, and map-path usage).
- deterministic dependency graph ordering and transitive upstream class dependency extraction.
- markdown report generation checks for both command modules (rule breakdowns, hotspots, session/cookie keys, and dependency tree rendering).

## Sample Project
`RefactorCli.SampleLegacyWeb` exists to provide deterministic, in-repo input patterns without relying on the real `System.Web` assembly.
