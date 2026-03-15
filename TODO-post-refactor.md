# Post-Refactor Issues & Improvements

Issues discovered during the refactoring mission that were out of scope. Grouped by priority.

---

## Bug Fixes

### 1. Missing save persistence for `openAiModelName`
`openAiModelName` field in `ApiSettings` has no `Scribe_Values.Look` call in `ExposeData()`, so it is never saved/loaded. This predates the refactoring.
**Fix:** Add `Scribe_Values.Look(ref openAiModelName, "openAiModelName", "");` in `ApiSettings.ExposeData()`.
DONE

### 2. Thread-safety in `SpeechBubbleManager.IsConversationActive`
`activeConversations.Contains()` is read without acquiring `queueLock`, while mutations to the collection are locked. Race condition from original code.
**Fix:** Add lock acquisition around the read, or switch to a `ConcurrentDictionary`/`ConcurrentBag`.
DONE — Also fixed `HasActiveConversations()` which had the same issue.

### 3. Double serialization of dating data
`DateTracker_MapComponent.ExposeData()` still calls `DatingManager.Current.ExposeData()`, but now that `DatingManager` is a `GameComponent`, the engine calls `ExposeData()` automatically. This causes `"dates"` and `"dateCooldowns"` keys to be serialized twice. Currently provides accidental backward compatibility for pre-conversion saves.
**Fix:** Remove the `DatingManager.Current.ExposeData()` call from `DateTracker_MapComponent`. Consider a one-time migration path for users with saves from before the GameComponent conversion.
DONE

---

## Code Quality

### 4. Remove unused `misbehaviorCheckInterval` field
`ChildrenMisbehaviorManager.misbehaviorCheckInterval` is assigned but never read. Produces CS0414 compiler warning on every build.
**Fix:** Remove the field, or wire it into the misbehavior check cadence logic if it was intended to be used.
DONE

### 5. Optimize `SpeechBubbleManager.GameComponentTick()` double-lookup
`GameComponentTick()` calls static helper methods which re-fetch `Current` from the game — a redundant component lookup each tick.
**Fix:** Route tick logic through instance methods directly instead of via static wrappers.
DONE — Extracted `EndConversationInternal()` instance method; `GameComponentTick` calls it directly instead of the static `EndConversation()`.

### 6. `chatLog` field style in `ChatLogManager`
Declared `private readonly` on a mutable `List<T>`. The `readonly` prevents reassignment but not mutation.
**Fix:** Minor — consider using `IReadOnlyList<T>` for public API or documenting intent.
DONE — Changed `GetChatLog()` return type to `IReadOnlyList<ChatMessage>` and updated callers.

---

## Documentation

### 7. Stale `SocialInteractions/architecture.md`
Still references deleted `compile.rsp` workflow and legacy `csc.exe` compiler.
**Fix:** Update or remove stale references to reflect the current `dotnet build` setup.
DONE

---

## Architecture Improvements

### 8. Reduce lateral namespace coupling
Subsystem namespaces have lateral dependencies (Dating->Speech, Combat->Speech, Interactions->Children/Dating, Negotiation->Speech/UI, Speech->UI). None are circular, but they don't follow the "depend inward only" ideal.
**Fix:** Extract shared interfaces to `SocialInteractions` (Core) namespace so subsystems depend on abstractions rather than directly referencing sibling namespaces.
DONE — Implemented in 5 waves:
- Created `ISpeechService`, `IChatLog`, `IModLogger` interfaces in `Core/`
- Created `Services` static locator in `Core/`
- Moved `ChatMessage`/`MessageType` DTOs from `UI/` to `Core/` namespace
- `SpeechBubbleManager` implements `ISpeechService`, `ChatLogManager` implements `IChatLog`
- Migrated 7 consumer files (Dating, Combat, Negotiation, Interactions, Speech) to use `Services.*` instead of direct cross-namespace references
- Remaining: `NegotiationManager.cs` retains one fully-qualified `TTSManager` reference (TTS not in scope for ISpeechService)

---

## Testing

### 9. Runtime API client tests blocked by Unity reference assemblies
Characterization tests use source-level assertions because runtime instantiation of mod types triggers `UnityEngine.CoreModule` loading failures. This limits test coverage to ~5-15% of the codebase.
**Fix:** To enable runtime tests, provide executable Unity/RimWorld assemblies (not ref-only) in the test path, or introduce a test-time abstraction layer around logging/settings dependencies.
DONE — Implemented abstraction layer:
- `SLog` now delegates to `IModLogger` (null = silent no-ops in tests)
- API clients accept `LlmClientConfig` POCO instead of reading `SocialInteractions.Settings` directly
- Created `NullLogger`, `TestLogger`, `ApiTestBase` test helpers
- Added 49 runtime unit tests: API client construction, `CleanChatResponse`, `IsValidHeaderValue`, `LlmClientConfig` defaults, `SLog` abstraction
