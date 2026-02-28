# RefactorCli Documentation

## Goal
`RefactorCli` is a modular .NET CLI for migration analysis.  
Its current goal is to generate a repeatable inventory of `System.Web` usage across a target solution, including API usage and file locations, to support incremental ASP.NET Core migration planning.

## Current Scope
- Analyze only (no source rewriting).
- Use Roslyn semantic analysis where possible.
- Include non-code signals (`web.config`, `.cshtml`) in the same catalog output.
- Produce stable reports that can be diffed between runs.

## Architecture

### Solution Layout
- `RefactorCli`: executable entrypoint, host bootstrapping, DI wiring.
- `RefactorCli.Abstractions`: shared generic contracts and exit codes.
- `RefactorCli.Infrastructure`: filesystem and console abstractions.
- `RefactorCli.Commands.SystemWebCatalog`: `systemweb catalog` command module + handler, analysis pipeline, and report writers.
- `RefactorCli.Tests`: analyzer and ordering tests.
- `RefactorCli.SampleLegacyWeb`: sample legacy-style project using a local `System.Web` shim.

## Sample Project
`RefactorCli.SampleLegacyWeb` is included to exercise catalog rules without referencing the real `System.Web` assembly.

### Why It Exists
- Provides deterministic analyzer coverage in this repo.
- Demonstrates legacy patterns commonly seen before ASP.NET Core migration.
- Lets you validate report output quickly on a known input.

### What It Contains
- Shim namespace and types: `System.Web.HttpApplication`, `System.Web.HttpContext`, `System.Web.IHttpHandler`, session state, and `HttpContext.Current`.
- Code usage:
  - `using System.Web`
  - base type usage (`HttpApplication`)
  - handler usage (`IHttpHandler`)
  - `HttpContext.Current` access
  - session write/read (`Session["CurrentUserId"]`)
- Non-code usage:
  - `web.config` with `system.web`, auth/membership/handlers/modules
  - `.cshtml` view with `System.Web`/`HttpContext`/`Request.`/`Response.` tokens

### Analyze Only the Sample Solution
```bash
dotnet run --project RefactorCli/RefactorCli.csproj -- \
  systemweb catalog \
  --solution /Users/caspercramer/Documents/dev/RefactorCli/RefactorCli.sln \
  --output ./refactor-reports \
  --format json \
  --format md
```

### Layering Rules
- Commands should use abstractions and services, not Roslyn APIs directly.
- System.Web-specific Roslyn analysis lives with the `SystemWebCatalog` command.
- Reporting is format-specific and implemented through `IReportWriter`.

### Command Model
- Module contract: `ICommandModule`.
- Handler contract: `ICommandHandler<TOptions>`.
- First module: `systemweb catalog`.

### Analysis Pipeline
1. Load `.sln` through `IRoslynSolutionLoader` (`MSBuildWorkspace` + diagnostics).
2. Run `ICatalogAnalyzer` implementations per project.
3. Collect + dedupe findings via `CatalogAccumulator`.
4. Build stable `CatalogReport` ordering.
5. Write output via configured `IReportWriter` implementations.

## Usage

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
- `--solution <path>`: solution to analyze (required today).
- `--repo <path>`: reserved for future repo-first flows.
- `--output <path>`: report output directory.
- `--format json|md|sarif`: report formats (`json` and `md` implemented).
- `--verbosity quiet|normal|diag`: command verbosity level.

### Exit Codes
- `0`: success.
- `2`: invalid args / missing solution.
- `3`: solution load failure (for example zero projects loaded).
- `4`: unexpected error.

## Report Model
Top-level `CatalogReport`:
- `GeneratedAtUtc`
- `SolutionPath`
- `Projects[]`

Per-project `ProjectReport`:
- `ProjectName`
- `ProjectPath`
- `TargetFramework`
- `DocumentsAnalyzed`
- `Findings[]`

Per-finding `Finding`:
- `Id`, `Category`, `Severity`, `Message`
- `Symbol`
- `FilePath`, `Line`, `Column`
- `Snippet`
- `Properties`

## Implemented Rules
- `SW0001`: `using System.Web...`
- `SW0002`: semantic symbol usage in `System.Web.*`
- `SW0003`: base type / inheritance references in `System.Web.*`
- `SW0004`: `*.config` patterns (`system.web`, auth/membership/handlers/modules, etc.)
- `SW0005`: heuristic `.cshtml` patterns (`System.Web`, `HttpContext`, `Request.`, etc.)

## How To Extend

### Add a New Command Module
1. Create a new project (for example `RefactorCli.Commands.<Feature>`).
2. Implement `ICommandModule` and register commands/options.
3. Implement typed `ICommandHandler<TOptions>`.
4. Register services in `Program.cs` with `services.AddModule<YourModule>(rootCommand);`.

### Add a New Analyzer Rule
1. Create a class implementing `ICatalogAnalyzer` in `RefactorCli.Commands.SystemWebCatalog/Analysis`.
2. Give it a stable rule ID (`SWxxxx`).
3. Add findings through `CatalogAccumulator` (dedupe-safe).
4. Register analyzer in `AnalysisServiceCollectionExtensions`.
5. Add unit tests in `RefactorCli.Tests`.

### Add a New Report Format
1. Implement `IReportWriter` in `RefactorCli.Commands.SystemWebCatalog/Reporting`.
2. Set `Format` to the CLI token (example: `sarif`).
3. Register writer in `SystemWebCatalogCommandModule`.
4. Include usage docs and test output stability.

## Testing
- Run all tests:
```bash
dotnet test RefactorCli.Tests/RefactorCli.Tests.csproj
```
- Current tests validate:
  - SW0001 detection.
  - SW0002 semantic binding (including fully qualified usage without `using`).
  - stable report ordering behavior.
