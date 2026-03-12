# Architecture

Architectural decisions, patterns, and conventions discovered during the mission.

---

## Current State (After Milestone: structural-cleanup)
- 129 C# files organized into 14 subdirectories under SocialInteractions/
- Directory-aligned namespaces: `SocialInteractions.{DirName}` (Core/ retains bare `SocialInteractions`)
- 10 API clients as static classes with duplicated HTTP/serialization boilerplate
- Settings monolith: `SocialInteractionsModSettings` at 1183 lines, 60+ fields
- Mix of static classes and GameComponents for managers
- Legacy compile.bat and compile.rsp have been deleted; standardized on `dotnet build`

## Gotchas Discovered During Structural Cleanup

### Verse.UI Namespace Shadowing
After introducing the `SocialInteractions.UI` namespace, any code referencing `UI.screenHeight` (from `Verse.UI`) becomes ambiguous. Must use fully-qualified `Verse.UI.screenHeight` (or `Verse.UI.screenWidth`, etc.) in files that have `using SocialInteractions.UI;` or are inside the `SocialInteractions.UI` namespace.

### String-Based Harmony Self-Patching
When the mod patches its own types across namespaces (e.g., `RaidNegotiation_Patches` in `SocialInteractions.Negotiation` patching `JobDriver_HaveChatWith` in `SocialInteractions.Jobs`), use string-based `AccessTools.Method("SocialInteractions.Jobs.JobDriver_HaveChatWith:MethodName")` rather than `typeof()` to avoid direct sibling namespace coupling.

## Target State
- 14 subdirectories (Core, Api, Dating, Children, Negotiation, Interactions, Combat, Speech, UI, DefOfs, Jobs, Components, Patches)
- Namespaces matching directories: SocialInteractions.{Dir}
- ILlmClient interface + LlmClientBase + LlmClientFactory
- Nested settings classes (ApiSettings, FeatureToggles, PromptSettings, DisplaySettings, GameplaySettings)
- All managers as GameComponents with static Current accessors
