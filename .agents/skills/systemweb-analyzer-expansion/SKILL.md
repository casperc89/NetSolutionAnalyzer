---
name: systemweb-analyzer-expansion
description: Add or extend System.Web catalog analyzers in RefactorCli.Commands.SystemWebCatalog, including analyzer registration, tests, sample project scenarios, and shim updates.
---

# System.Web Analyzer Expansion

Use this skill when a user asks to add or refine System.Web migration analyzers in this repo.

## Scope

Target project:
- `RefactorCli.Commands.SystemWebCatalog`

Always include:
- analyzer implementation
- DI registration
- analyzer tests
- sample project coverage
- shim updates if sample code needs additional System.Web shapes

## Inputs To Confirm

Capture these from the user request:
- rule IDs and titles (for example `SW0400`)
- exact detection shapes (symbols/member chains/config markers)
- severity/category expectations if provided
- whether existing rules should be refactored to avoid overlap

If any are missing, use existing repo conventions and continue.

## Workflow

1. Inspect current analyzer patterns:
- `RefactorCli.Commands.SystemWebCatalog/Analyzers/*.cs`
- `RefactorCli.Commands.SystemWebCatalog/Analysis/AnalysisServiceCollectionExtensions.cs`
- `RefactorCli.Tests/SystemWebAnalyzersTests.cs`

2. Implement one analyzer per rule:
- Add `CatalogRuleDescriptor` with stable ID/title/category/severity.
- Prefer semantic symbol checks over string matching.
- For member chains (for example `HttpContext.Current.Session`), resolve symbols on each relevant `MemberAccessExpressionSyntax`.
- For indexers (`Session[...]`), inspect `ElementAccessExpressionSyntax`.
- Keep messages migration-oriented and specific.

3. Register analyzers:
- Add `services.AddSingleton<ICatalogAnalyzer, ...>();` in `AnalysisServiceCollectionExtensions`.

4. Add tests:
- At least one positive test per rule in `SystemWebAnalyzersTests`.
- Add false-positive guard tests for lookalike non-`System.Web` symbols when relevant.
- Keep test sources minimal and self-contained in `CreateProject("""...""")` blocks.

5. Update sample project:
- Add/extend files in `RefactorCli.SampleLegacyWeb` to produce clear rule hits.
- Prefer dedicated files per concern (session, cookies, uploads, lifecycle, etc.).
- Keep examples simple and explicit.

6. Update shim as needed:
- Extend `RefactorCli.SampleSystemWebShim/SystemWebShim.cs` with minimal members/types needed to compile sample scenarios.
- Avoid over-modeling full framework behavior.

7. Validate:
- `dotnet build RefactorCli.sln`
- `dotnet test RefactorCli.Tests/RefactorCli.Tests.csproj --no-build`
- If full tests have known unrelated failures, run targeted tests with `--filter`.

## Rule Design Guardrails

- Avoid duplicate findings for the exact same location/symbol pair where possible; rely on `CatalogAccumulator` dedupe key behavior.
- Use fully-qualified symbol names in findings when ambiguity is likely.
- Keep analyzer overlap intentional:
  - broad catch-all rules can coexist with specific migration-blocker rules,
  - specific rules should provide better actionable messages.

## File Checklist

- `RefactorCli.Commands.SystemWebCatalog/Analyzers/<NewAnalyzer>.cs`
- `RefactorCli.Commands.SystemWebCatalog/Analysis/AnalysisServiceCollectionExtensions.cs`
- `RefactorCli.Tests/SystemWebAnalyzersTests.cs`
- `RefactorCli.SampleLegacyWeb/<Scenario>.cs`
- `RefactorCli.SampleSystemWebShim/SystemWebShim.cs` (if needed)

## Completion Output

When done, report:
- implemented rules and detection behavior
- changed files
- build/test commands run and pass/fail status
- any known unrelated test failures
