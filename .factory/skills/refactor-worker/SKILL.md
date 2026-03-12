---
name: refactor-worker
description: Handles structural refactoring — file moves, namespace changes, code transformations, interface extraction, class conversion.
---

# Refactor Worker

NOTE: Startup and cleanup are handled by `worker-base`. This skill defines the WORK PROCEDURE.

## When to Use This Skill

Features involving:
- Moving files between directories
- Changing namespaces and adding using directives
- Extracting interfaces and base classes from existing code
- Converting static classes to instance-based (GameComponent)
- Decomposing large classes into smaller ones
- Removing legacy files
- Any structural code transformation that preserves behavior

## Work Procedure

### Step 1: Read the Feature and Context

1. Read the feature description, preconditions, expectedBehavior, and verificationSteps from features.json
2. Read AGENTS.md for mission boundaries, file mapping, and critical codebase facts
3. Read `.factory/library/architecture.md` for any patterns discovered by previous workers
4. If the feature involves specific files, read them to understand current structure

### Step 2: Plan the Changes

Before making any changes, create a concrete plan:
- List every file that needs to be created, moved, or modified
- For namespace changes: identify all cross-references that need `using` directives
- For class conversions: identify all callers that need updating
- For interface extraction: identify the shared contract and per-implementation differences

### Step 3: Make Changes Systematically

**For file moves (directory restructuring):**
1. Create target directories if they don't exist
2. Move files using `git mv` (preserves history)
3. Update namespaces in moved files
4. Add `using` directives to files that reference moved types
5. Build after each batch of related moves to catch errors early

**For interface/base class extraction:**
1. Read ALL files that will be affected to understand the full pattern
2. Create the interface/base class first
3. Modify one implementation at a time, building between each
4. Update callers last

**For class conversion (static -> GameComponent):**
1. Read the static class fully — note all static fields, methods, and callers
2. Convert the class declaration (add `: GameComponent`, constructor, `Current` accessor)
3. Convert static fields to instance fields
4. Convert static methods to instance methods
5. Update ALL callers to use `.Current.` pattern (search thoroughly with grep)
6. Add `ExposeData()` override for any state that was previously persisted
7. Build and verify

### Step 4: Build Verification

After completing changes:
1. Run `dotnet build SocialInteractions\SocialInteractions.csproj -c Release`
2. If build fails, fix errors immediately — do NOT leave a broken build
3. If tests exist, run `dotnet test SocialInteractions.Tests\SocialInteractions.Tests.csproj`
4. Run any specific verification steps from the feature definition

### Step 5: Verify Completeness

- Grep for any remaining references to old patterns (old namespaces, old static calls, etc.)
- Verify no files were left behind (orphaned in wrong directory)
- Check that all expectedBehavior items from the feature are satisfied

### Step 6: Update Shared State

If you discovered patterns, conventions, or issues that future workers should know:
- Update `.factory/library/architecture.md` with architectural decisions made
- Note any edge cases or gotchas encountered

### Step 7: Commit

Commit all changes with a descriptive message. Use `git add -A` to capture moves and new files.

## Example Handoff

```json
{
  "salientSummary": "Moved 10 API client files from SocialInteractions/ root to SocialInteractions/Api/, updated namespace to SocialInteractions.Api in each file, added 'using SocialInteractions.Api;' to 23 files that reference API clients. Build succeeds with 0 errors.",
  "whatWasImplemented": "Relocated all 10 API client .cs files (OpenAi, Claude, Gemini, Ollama, Kobold, LMStudio, Qwen, Deepseek, Grok, Player2) to the Api/ subdirectory. Changed namespace declaration in each from 'SocialInteractions' to 'SocialInteractions.Api'. Added using directives in SocialInteractions.cs, NegotiationManager.cs, and 21 other files that reference API client types.",
  "whatWasLeftUndone": "",
  "verification": {
    "commandsRun": [
      {
        "command": "dotnet build SocialInteractions\\SocialInteractions.csproj -c Release",
        "exitCode": 0,
        "observation": "Build succeeded. 0 Warning(s) 0 Error(s)"
      },
      {
        "command": "rg \"namespace SocialInteractions$\" SocialInteractions\\Api\\",
        "exitCode": 1,
        "observation": "No matches — all Api/ files correctly use SocialInteractions.Api namespace"
      }
    ],
    "interactiveChecks": []
  },
  "tests": {
    "added": []
  },
  "discoveredIssues": [
    {
      "severity": "low",
      "description": "Player2ApiClient has instance methods unlike other static clients — will need special handling during interface extraction",
      "suggestedFix": "Note in architecture.md for the API layer feature"
    }
  ]
}
```

## When to Return to Orchestrator

- Build fails with errors that seem unrelated to current feature's changes
- A file listed in the feature description doesn't exist or has unexpected structure
- Circular dependency discovered that can't be resolved within current feature scope
- Changes would require modifying off-limits files (deploy.bat, XML Defs, etc.)
- Feature depends on work not yet completed (missing interface, missing base class)
