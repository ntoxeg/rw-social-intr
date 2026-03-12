# Architecture

Architectural decisions, patterns, and conventions discovered during the mission.

---

## Current State (Pre-Refactoring)
- 129 C# files in flat SocialInteractions/ directory
- Single namespace: `SocialInteractions`
- 10 API clients as static classes with duplicated HTTP/serialization boilerplate
- Settings monolith: `SocialInteractionsModSettings` at 1183 lines, 60+ fields
- Mix of static classes and GameComponents for managers
- Legacy compile.bat + compile.rsp alongside modern .csproj

## Target State
- 14 subdirectories (Core, Api, Dating, Children, Negotiation, Interactions, Combat, Speech, UI, DefOfs, Jobs, Components, Patches)
- Namespaces matching directories: SocialInteractions.{Dir}
- ILlmClient interface + LlmClientBase + LlmClientFactory
- Nested settings classes (ApiSettings, FeatureToggles, PromptSettings, DisplaySettings, GameplaySettings)
- All managers as GameComponents with static Current accessors
