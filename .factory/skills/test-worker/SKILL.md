---
name: test-worker
description: Handles test project setup and writing characterization tests for the refactored codebase.
---

# Test Worker

NOTE: Startup and cleanup are handled by `worker-base`. This skill defines the WORK PROCEDURE.

## When to Use This Skill

Features involving:
- Setting up the test project infrastructure (csproj, solution file)
- Writing characterization tests for refactored code
- Adding InternalsVisibleTo attributes
- Testing factory patterns, interface contracts, error handling

## Work Procedure

### Step 1: Read the Feature and Context

1. Read the feature description, preconditions, expectedBehavior, and verificationSteps
2. Read AGENTS.md for mission boundaries and coding conventions
3. Read `.factory/library/architecture.md` for current architecture state
4. Read the source files being tested to understand their contracts

### Step 2: Set Up Test Infrastructure (if needed)

If the test project doesn't exist yet:
1. Create `SocialInteractions.Tests/SocialInteractions.Tests.csproj` with xUnit, targeting net48
2. Add `<ProjectReference>` to the main project
3. Add `[assembly: InternalsVisibleTo("SocialInteractions.Tests")]` to the main project
4. Create a solution file if one doesn't exist
5. Verify `dotnet build` of the test project succeeds
6. Verify `dotnet test` runs (even with 0 tests)

### Step 3: Write Tests (TDD Where Applicable)

**For characterization tests (testing existing behavior):**
1. Read the code being tested thoroughly
2. Identify testable surface (pure functions, data transformations, factory patterns)
3. Write tests that capture current behavior — NOT what you think behavior should be
4. Tests should fail if refactoring accidentally changes behavior

**For new code tests (interface contracts, factories):**
1. Write the test first (red)
2. Verify it fails for the right reason
3. Implement or verify implementation makes it pass (green)

**What IS testable (without RimWorld runtime):**
- LlmClientFactory returning correct types for each LlmApiType
- ILlmClient interface contract (Name property, method signatures)
- Error handling (null returns on failure)
- Settings field defaults and nested class structure
- Pure utility functions (string processing, data transformations)
- Data contract serialization/deserialization

**What is NOT testable (requires game runtime):**
- Anything using Pawn, Map, Thing, Game types at runtime
- Harmony patches
- GameComponent lifecycle (ExposeData, GameComponentTick)
- UI/Dialog classes (Unity Rect, Widgets)
- Scribe serialization (requires game's XML system)

### Step 4: Build and Run Tests

1. Run `dotnet build SocialInteractions.Tests\SocialInteractions.Tests.csproj`
2. Run `dotnet test SocialInteractions.Tests\SocialInteractions.Tests.csproj`
3. All tests must pass with 0 failures
4. Fix any failures before completing

### Step 5: Commit

Commit test files with a descriptive message.

## Example Handoff

```json
{
  "salientSummary": "Created test project with xUnit targeting net48, added ProjectReference to main project, added InternalsVisibleTo. Wrote 12 tests: 10 for LlmClientFactory (one per LlmApiType), 1 for factory null handling, 1 for ILlmClient Name property. All 12 pass.",
  "whatWasImplemented": "Created SocialInteractions.Tests/ with .csproj referencing xUnit 2.9.3 and main project. Added [assembly: InternalsVisibleTo] to main project. Wrote LlmClientFactoryTests.cs with 10 factory creation tests (one per API type verifying correct return type), edge case test for unknown type, and ILlmClient contract test verifying Name is non-empty for each client. Created solution file linking both projects.",
  "whatWasLeftUndone": "",
  "verification": {
    "commandsRun": [
      {
        "command": "dotnet test SocialInteractions.Tests\\SocialInteractions.Tests.csproj",
        "exitCode": 0,
        "observation": "Passed! - Failed: 0, Passed: 12, Skipped: 0, Total: 12"
      }
    ],
    "interactiveChecks": []
  },
  "tests": {
    "added": [
      {
        "file": "SocialInteractions.Tests/LlmClientFactoryTests.cs",
        "cases": [
          { "name": "Create_KoboldCpp_ReturnsKoboldClient", "verifies": "Factory returns KoboldApiClient for LlmApiType.KoboldCpp" },
          { "name": "Create_OpenAI_ReturnsOpenAiClient", "verifies": "Factory returns OpenAiApiClient for LlmApiType.OpenAI" },
          { "name": "AllClients_HaveNonEmptyName", "verifies": "Every ILlmClient implementation returns non-empty Name" }
        ]
      }
    ]
  },
  "discoveredIssues": []
}
```

## When to Return to Orchestrator

- Main project doesn't build (can't reference it)
- Types that should be testable require RimWorld runtime unexpectedly
- InternalsVisibleTo can't be added (file is off-limits or missing)
- Test framework dependency issues (NuGet restore failures)
