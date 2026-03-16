# Pawn Memory System

## TL;DR

> **Quick Summary**: Add a persistent memory system to colony pawns. Events and conversations accumulate daily in a buffer, then an LLM call distills them into a memory entry per pawn. Memories are injected into all prompts via `[pawn#_memories]` placeholder, enabling pawns to evolve personality over time. Hybrid compaction keeps memory bounded.
> 
> **Deliverables**:
> - PawnMemory GameComponent with storage + buffer + save/load persistence
> - Daily async LLM-based memory writer with customizable prompt template
> - Event capture hooks across all interaction types (LLM content, drama, game events, combat)
> - Hybrid compaction (FIFO + LLM summarization) to bound memory size
> - `[pawn#_memories]` placeholder integrated into all prompts
> - Memory tab in existing Bio editor for player viewing/editing
> - Settings: toggle, character limit, prompt template
> - Unit tests for compaction, buffer management, size limits
> 
> **Estimated Effort**: Large
> **Parallel Execution**: YES - 4 waves + final verification
> **Critical Path**: Task 1 → Task 5 → Task 9 → Task 11 → Final Verification

---

## Context

### Original Request
Build a memory module for colony pawns. Each pawn gets personal memory storage written by the LLM via a dedicated prompt, based on events and conversations. Memories feed back into prompts for all social interactions (dialogues, monologues). Must include compaction logic to prevent unbounded growth.

### Interview Summary
**Key Discussions**:
- **Memory writing**: Batched/periodic — raw events accumulate, then once per in-game day, one LLM call per pawn digests the buffer into a memory entry
- **Memory sources**: Everything the pawn experiences — LLM dialogue, monologues, drama, game events, combat, injuries, relationship changes
- **Size budget**: ~500-800 tokens per pawn (~2000-3000 characters)
- **Compaction**: Hybrid — recent entries kept verbatim, older entries periodically consolidated via LLM summarization
- **Player visibility**: Full editor as a new tab in existing Bio editor dialog
- **Processing**: Background async, non-blocking, same pattern as existing LLM calls
- **Prompt template**: User-customizable in mod settings, like dialogue/monologue templates
- **Tests**: After implementation

**Research Findings**:
- `PawnFlavorText_GameComponent` is the closest existing pattern: `Dictionary<int, string>` → `Scribe_Collections` → `[pawn#_bio]` placeholder. Memory system mirrors this.
- `ExtractPawnData()` is the central prompt injection hub (~25 keys per pawn). Adding `[pawn#_memories]` is straightforward.
- `HandleNonStoppingInteraction` and `HandleMonologue` are the two main LLM response endpoints — natural buffer write points.
- Current `[pawn#_journal]` only stores ONE vanilla PlayLog entry — memory system replaces this with rich, persistent context.
- `SpeechBubbleManager` already uses `GameComponentTick()` — established precedent for tick-based processing.
- README mentions 2k context window is "usually enough" — memory size must be carefully bounded.
- Architecture doc says C# 5 compatible syntax required. Project uses `SocialInteractions/SocialInteractions.csproj` for builds (no `compile.rsp` — MSBuild auto-includes `.cs` files).

### Metis Review
**Identified Gaps** (addressed):
- **Context budget risk**: 500-800 tokens is significant in a 2k window. Added hard character limit guardrail with conservative default.
- **Buffer flooding**: Uncapped buffer could grow huge on eventful days. Added per-pawn buffer entry cap (max 50/day).
- **Thread safety**: Buffer writes from game thread, daily writer runs async. Need locks on buffer access.
- **Save during async**: If save happens mid-LLM-call, buffer is persisted (safe), in-progress call is lost (retries next day).
- **Dead pawn mid-buffer**: Skip dead/destroyed pawns during daily processing, clear their buffers.
- **Mod added to existing save**: Graceful empty state — no memories, starts accumulating from that point.
- **Multiple maps**: GameComponent is game-level (not map-level), works across all maps.
- **Bio editor is single-panel**: Need to redesign as tabbed or add toggle buttons (Bio | Memories).

---

## Work Objectives

### Core Objective
Give colony pawns persistent, evolving memory that influences all future LLM-generated social interactions, creating the illusion of character development and continuity.

### Concrete Deliverables
- `SocialInteractions/Memory/PawnMemory_GameComponent.cs` — Core storage and buffer
- `SocialInteractions/Memory/MemorySettings.cs` — Settings data class (or integrated into existing settings)
- `SocialInteractions/Memory/DailyMemoryWriter.cs` — Daily async LLM-based memory processor
- `SocialInteractions/Memory/MemoryCompactor.cs` — Hybrid FIFO + LLM compaction logic
- Modified `SocialInteractions/Core/SocialInteractions.cs` — ExtractPawnData with `[pawn#_memories]`
- Modified `SocialInteractions/Core/SocialInteractionsSettings.cs` — Memory toggle, char limit, prompt template
- Modified `SocialInteractions/UI/Dialog_EditPawnFlavorText.cs` — Memory tab
- Event capture hooks across: HandleNonStoppingInteraction, HandleMonologue, drama InteractionWorkers, game event patches, combat patches
- Unit tests in `SocialInteractions.Tests/`
- Verified new files auto-included in `SocialInteractions/SocialInteractions.csproj` build
- Updated `SocialInteractions/architecture.md` with Memory system section

### Definition of Done
- [ ] Colony pawns accumulate events in buffer during gameplay
- [ ] Once per in-game day, LLM processes each pawn's buffer into a memory entry
- [ ] `[pawn#_memories]` placeholder works in prompt templates
- [ ] Memories persist across save/load
- [ ] Hybrid compaction prevents unbounded growth
- [ ] Player can view/edit memories via Bio editor
- [ ] Memory system can be toggled on/off in settings
- [ ] All new files compile via `dotnet build SocialInteractions/SocialInteractions.csproj`
- [ ] Unit tests pass for compaction and buffer logic

### Must Have
- Colony pawns ONLY (filter by `pawn.IsColonist`)
- Hard character limit on memory size (default ~2500 chars)
- Per-pawn buffer entry cap (max 50 entries/day to prevent flooding)
- Thread-safe buffer access (locks for async operations)
- Background async LLM processing (non-blocking)
- Graceful handling: dead pawns, empty buffers, mod added to existing save
- User-customizable memory prompt template
- Feature toggle in settings (default: enabled)

### Must NOT Have (Guardrails)
- **No token counting** — use character limits, not token limits (no tokenizer available in C#)
- **No memory for non-colony pawns** — raiders, visitors, prisoners, animals are excluded
- **No real-time memory writing** — all memory creation is batched (daily), never per-interaction
- **No blocking LLM calls** — all daily processing must be async
- **No breaking existing prompts** — `[pawn#_memories]` is additive; existing placeholders unchanged
- **No excessive abstraction** — follow the simple patterns already in the codebase (static dictionaries, direct GameComponent access)
- **No save corruption risk** — buffer is always in a saveable state; async writes don't touch Scribe data
- **No AI slop** — no generic utility classes, no over-engineered interfaces, no unnecessary design patterns

---

## Verification Strategy

> **ZERO HUMAN INTERVENTION** — ALL verification is agent-executed. No exceptions.

### Test Decision
- **Infrastructure exists**: YES (`SocialInteractions.Tests/` directory present)
- **Automated tests**: Tests after implementation
- **Framework**: Whatever the existing test project uses (discover during implementation)

### QA Policy
Every task includes agent-executed QA scenarios.
Evidence saved to `.sisyphus/evidence/task-{N}-{scenario-slug}.{ext}`.

- **Core logic**: Use Bash (dotnet test / build commands) — compile, run tests, verify output
- **Integration**: Use Bash — build mod DLL, verify no compile errors
- **UI**: Agent reads code structure to verify UI element placement and tab logic

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Foundation — parallel, all quick):
├── Task 1: PawnMemory GameComponent — storage + buffer + persistence [quick]
├── Task 2: Memory settings — toggle, char limit, prompt template [quick]
└── Task 3: Memory prompt integration (reading) — [pawn#_memories] in ExtractPawnData [quick]

Wave 2 (Core Pipeline — after Wave 1):
├── Task 4: LLM content capture — buffer writes in HandleNonStoppingInteraction + HandleMonologue [quick]
├── Task 5: Daily memory writer — GameComponentTick + async LLM processing [deep]
└── Task 6: Bio editor memory tab — extend Dialog_EditPawnFlavorText [visual-engineering]

Wave 3 (Event Capture + Compaction — after Wave 2, highly parallel):
├── Task 7: Drama interaction capture — badmouthing, backstabbing, admiration, make-up hooks [unspecified-high]
├── Task 8: Game event + combat capture — marriage, death, raid, birth, injury hooks [unspecified-high]
├── Task 9: Hybrid compaction — FIFO truncation + LLM summarization [deep]
└── Task 10: Settings UI — add memory section to mod settings page [quick]

Wave 4 (Tests + Documentation — after Wave 3):
├── Task 11: Unit tests — compaction, buffer, size limits [unspecified-high]
└── Task 12: Architecture doc update [writing]

Wave FINAL (Verification — after ALL tasks, 4 parallel):
├── Task F1: Plan compliance audit (oracle)
├── Task F2: Code quality review (unspecified-high)
├── Task F3: Build + integration QA (unspecified-high)
└── Task F4: Scope fidelity check (deep)

Critical Path: Task 1 → Task 5 → Task 9 → Task 11 → Final Verification
Parallel Speedup: ~60% faster than sequential
Max Concurrent: 4 (Waves 3 & Final)
```

### Dependency Matrix

| Task | Depends On | Blocks | Wave |
|------|-----------|--------|------|
| 1 | — | 3, 4, 5, 6, 7, 8, 9, 10, 11 | 1 |
| 2 | — | 5, 6, 10 | 1 |
| 3 | 1 | 5 | 1 |
| 4 | 1 | 5, 7, 8 | 2 |
| 5 | 1, 2, 3, 4 | 9, 11 | 2 |
| 6 | 1, 2 | 11 | 2 |
| 7 | 1, 4 | 11 | 3 |
| 8 | 1, 4 | 11 | 3 |
| 9 | 1, 5 | 11 | 3 |
| 10 | 2 | — | 3 |
| 11 | 5, 6, 7, 8, 9 | 12 | 4 |
| 12 | 11 | — | 4 |

### Agent Dispatch Summary

- **Wave 1**: 3 tasks — T1 → `quick`, T2 → `quick`, T3 → `quick`
- **Wave 2**: 3 tasks — T4 → `quick`, T5 → `deep`, T6 → `visual-engineering`
- **Wave 3**: 4 tasks — T7 → `unspecified-high`, T8 → `unspecified-high`, T9 → `deep`, T10 → `quick`
- **Wave 4**: 2 tasks — T11 → `unspecified-high`, T12 → `writing`
- **FINAL**: 4 tasks — F1 → `oracle`, F2 → `unspecified-high`, F3 → `unspecified-high`, F4 → `deep`

---

## TODOs

- [x] 1. PawnMemory GameComponent — Storage, Buffer, and Persistence

  **What to do**:
  - Create `SocialInteractions/Memory/PawnMemory_GameComponent.cs` as a new `GameComponent`
  - Store memories in `Dictionary<int, string>` keyed by pawn `thingIDNumber` (same pattern as `PawnFlavorText_GameComponent`)
  - Store daily event buffer in `Dictionary<int, List<string>>` — each entry is a short text description of an event (e.g., `"Had a deep conversation with Maria about their shared love of cooking"`)
  - Implement `ExposeData()` using `Scribe_Collections.Look` for both dictionaries. For the buffer (which has `List<string>` values), you'll need the auxiliary list serialization pattern from `VoiceAssignmentManager` or flatten to a serializable format
  - Public methods: `GetMemory(int pawnId)`, `SetMemory(int pawnId, string memory)`, `AddBufferEntry(int pawnId, string entry)`, `GetAndClearBuffer(int pawnId)`, `GetAllPawnsWithBufferEntries()`, `ClearMemory(int pawnId)`
  - Add thread-safe locking on buffer access (buffer writes happen from game thread in interaction handlers; daily writer reads from async context)
  - Buffer entry cap: max 50 entries per pawn per day — silently drop oldest if exceeded
  - Constructor registers component for global access (either via `Services` static class or direct `Current.Game.GetComponent<PawnMemory_GameComponent>()`)
  - Verify new file is auto-included in `SocialInteractions/SocialInteractions.csproj` build (MSBuild globbing should handle this; if not, add explicit `<Compile>` entry)

  **Must NOT do**:
  - No memory for non-colony pawns
  - No complex inheritance hierarchy — single class, simple dictionary storage
  - No token counting — character limits only

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Single file, well-defined pattern to follow (PawnFlavorText_GameComponent), straightforward data structure
  - **Skills**: []
  - **Skills Evaluated but Omitted**:
    - `playwright`: No browser interaction needed
    - `git-master`: Not a git operation

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 2, 3)
  - **Blocks**: Tasks 3, 4, 5, 6, 7, 8, 9, 10, 11
  - **Blocked By**: None (can start immediately)

  **References**:

  **Pattern References** (existing code to follow):
  - `SocialInteractions/Components/PawnFlavorText_GameComponent.cs` — Primary pattern: `Dictionary<int, string>` storage with `Scribe_Collections.Look`, `GetFlavorText`/`SetFlavorText` methods, `SyncWithStaticDictionary`. Mirror this structure for memory storage.
  - `SocialInteractions/Speech/VoiceAssignmentManager.cs:176-237` — Auxiliary list serialization pattern for complex dictionary types in `ExposeData()`. Needed because `Dictionary<int, List<string>>` requires manual list handling for Scribe.
  - `SocialInteractions/Speech/SpeechBubbleManager.cs` (constructor) — Example of GameComponent registering itself. Look at how it sets `Services.Speech = this`.

  **API/Type References**:
  - `SocialInteractions/Core/Services.cs` — Static service locator pattern. If adding `Services.Memory`, follow this exact pattern with `{ get; set; }` property.
  - `SocialInteractions/Core/SocialInteractions.cs:36` — `public static Dictionary<int, string> PawnFlavorTexts` — Shows the static dictionary pattern. Memory component may need similar static backup.

  **Why Each Reference Matters**:
  - `SocialInteractions/Components/PawnFlavorText_GameComponent` is the EXACT pattern to copy — same key type (int pawn ID), same persistence mechanism. The memory component is essentially a second flavor text component with an additional buffer dictionary.
  - `VoiceAssignmentManager` shows how to handle the more complex buffer dictionary serialization where simple `Scribe_Collections.Look` won't work for nested collections.

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: GameComponent compiles and has correct API surface
    Tool: Bash
    Preconditions: All source files present in SocialInteractions/Memory/
    Steps:
      1. Build the project via `dotnet build SocialInteractions/SocialInteractions.csproj`
      2. Grep new file for required public methods: GetMemory, SetMemory, AddBufferEntry, GetAndClearBuffer, GetAllPawnsWithBufferEntries, ClearMemory
      3. Grep for ExposeData override with Scribe_Collections.Look calls
      4. Grep for lock statements on buffer access
    Expected Result: Build succeeds, all 6 public methods present, ExposeData has Scribe calls, lock statements present
    Failure Indicators: Build failure, missing methods, no Scribe persistence, no thread safety
    Evidence: .sisyphus/evidence/task-1-gamecomponent-api.txt

  Scenario: Buffer cap enforcement
    Tool: Bash
    Preconditions: Source file exists
    Steps:
      1. Grep for buffer cap constant (50 or configurable)
      2. Verify AddBufferEntry checks count before adding
      3. Verify oldest entries are dropped when cap exceeded
    Expected Result: Cap constant exists, AddBufferEntry enforces it
    Evidence: .sisyphus/evidence/task-1-buffer-cap.txt
  ```

  **Commit**: YES
  - Message: `feat(memory): add PawnMemory GameComponent with storage and persistence`
  - Files: `SocialInteractions/Memory/PawnMemory_GameComponent.cs`
  - Pre-commit: `dotnet build SocialInteractions/SocialInteractions.csproj` succeeds

- [x] 2. Memory Settings — Toggle, Character Limit, and Prompt Template

  **What to do**:
  - Add memory-related settings to `SocialInteractions/Core/SocialInteractionsSettings.cs`
  - Either create a `MemorySettings` inner class (following the pattern of `ApiSettings`, `FeatureToggles`, `PromptSettings`) or add fields to existing sections:
    - `enableMemorySystem` (bool, default `true`) — master toggle
    - `memoryCharacterLimit` (int, default `2500`) — hard cap on memory text length per pawn
    - `memoryCompactionThreshold` (int, default `2000`) — when memory exceeds this, trigger compaction
    - `memoryBufferEntryCap` (int, default `50`) — max buffer entries per pawn per day
    - `memoryPromptTemplate` (string) — customizable template for daily memory writing
    - `memoryCompactionPromptTemplate` (string) — customizable template for compaction summarization
  - Create default prompt templates:
    - **Memory writing template**: Takes `[pawn_name]`, `[existing_memories]`, `[todays_events]`, `[pawn_traits]`, `[pawn_mood]` and produces updated memory text. The prompt should instruct the LLM to maintain first-person perspective, integrate new events, preserve important memories, and stay under the character limit.
    - **Compaction template**: Takes `[pawn_name]`, `[full_memories]`, `[char_limit]` and produces a condensed summary.
  - Add `ExposeData` serialization for all new fields (follow existing `Scribe_Values.Look` pattern)

  **Must NOT do**:
  - No settings UI in this task (that's Task 10)
  - No complex template validation — just store the string

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Extending existing settings file with new fields, following established patterns
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 3)
  - **Blocks**: Tasks 5, 6, 10
  - **Blocked By**: None (can start immediately)

  **References**:

  **Pattern References**:
  - `SocialInteractions/Core/SocialInteractionsSettings.cs:272-291` — `PromptSettings` class pattern: how prompt templates are stored with default constants. Copy this pattern for memory templates.
  - `SocialInteractions/Core/SocialInteractionsSettings.cs:184-270` — `FeatureToggles` pattern: how boolean toggles and numeric settings are organized with `Scribe_Values.Look`.
  - `SocialInteractions/Core/SocialInteractionsSettings.cs:74-92` — Default template constants showing all available placeholders. Use as reference for designing memory template placeholders.

  **External References**:
  - The default memory writing prompt should be similar in spirit to the dialogue prompt but focused on memory synthesis rather than conversation generation.

  **Why Each Reference Matters**:
  - `PromptSettings` shows exactly how to store a customizable template with a default constant. Memory template follows this identical approach.
  - `FeatureToggles` shows the toggle + numeric settings pattern with proper Scribe serialization.

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Settings fields exist with correct defaults
    Tool: Bash
    Preconditions: Modified SocialInteractionsSettings.cs compiles
    Steps:
      1. Grep for enableMemorySystem with default true
      2. Grep for memoryCharacterLimit with default 2500
      3. Grep for memoryPromptTemplate with non-empty default
      4. Grep for memoryCompactionPromptTemplate with non-empty default
      5. Grep for Scribe_Values.Look calls for each new field
    Expected Result: All fields present with correct defaults and Scribe serialization
    Evidence: .sisyphus/evidence/task-2-settings-fields.txt

  Scenario: Default memory prompt template is well-formed
    Tool: Bash
    Preconditions: Source file exists
    Steps:
      1. Extract the default memory prompt template constant
      2. Verify it contains placeholders: [pawn_name], [existing_memories], [todays_events]
      3. Verify it instructs LLM about character limit and first-person perspective
    Expected Result: Template contains required placeholders and instructions
    Evidence: .sisyphus/evidence/task-2-prompt-template.txt
  ```

  **Commit**: YES
  - Message: `feat(memory): add memory settings, toggle, char limit, prompt template`
  - Files: `SocialInteractions/Core/SocialInteractionsSettings.cs`

- [x] 3. Memory Prompt Integration (Reading) — `[pawn#_memories]` in ExtractPawnData

  **What to do**:
  - In `SocialInteractions/Core/SocialInteractions.cs`, modify `ExtractPawnData()` method (around line 808 where `[pawn#_bio]` and `[pawn#_journal]` are set)
  - Add new dictionary entry: `data[prefix + "_memories"] = GetPawnMemory(pawn);`
  - Create static helper method `GetPawnMemory(Pawn pawn)`:
    - If memory system disabled in settings, return empty string
    - If pawn is not colonist, return empty string
    - Get `PawnMemory_GameComponent` via `Current.Game.GetComponent<PawnMemory_GameComponent>()`
    - Return stored memory text, or empty string if none exists
  - Update the default dialogue and monologue prompt templates to include `[pawn#_memories]` placeholder (add after `[pawn#_journal]` or `[pawn#_bio]`)
  - The placeholder should be documented in the template comments alongside existing placeholders

  **Must NOT do**:
  - No writing of memories in this task — reading only
  - No modification to prompt parsing logic (placeholder replacement already handles arbitrary keys)

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Adding a few lines to an existing method, creating a short helper method
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 2) — but depends on Task 1 for the GameComponent type reference
  - **Blocks**: Task 5
  - **Blocked By**: Task 1

  **References**:

  **Pattern References**:
  - `SocialInteractions/Core/SocialInteractions.cs:808-820` — Where `[pawn#_bio]` and `[pawn#_journal]` are set in `ExtractPawnData`. Add `[pawn#_memories]` here following identical pattern.
  - `SocialInteractions/Core/SocialInteractions.cs:65-131` — `GetPawnFlavorText(Pawn pawn)` method. Mirror this for `GetPawnMemory(Pawn pawn)` — same null checks, same component access pattern.
  - `SocialInteractions/Core/SocialInteractions.cs:509-520` — Start of `ExtractPawnData` showing parameter pattern and dictionary construction.

  **API/Type References**:
  - `SocialInteractions/Memory/PawnMemory_GameComponent.cs` (from Task 1) — `GetMemory(int pawnId)` method to call

  **Why Each Reference Matters**:
  - Lines 808-820 show the exact insertion point and pattern for adding a new placeholder. The memory placeholder should appear right next to journal and bio.
  - `GetPawnFlavorText` shows the exact defensive coding pattern: null component check, pawn ID extraction, fallback to empty string.

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: ExtractPawnData includes memory placeholder
    Tool: Bash
    Preconditions: Core/SocialInteractions.cs modified
    Steps:
      1. Grep for "_memories" in ExtractPawnData method
      2. Verify GetPawnMemory helper method exists
      3. Verify GetPawnMemory checks settings toggle and pawn.IsColonist
      4. Verify default prompt templates include [pawn1_memories] / [pawn2_memories]
    Expected Result: Placeholder added, helper method has proper guards, templates updated
    Evidence: .sisyphus/evidence/task-3-placeholder-integration.txt

  Scenario: Non-colonist pawns return empty memory
    Tool: Bash
    Preconditions: Source files exist
    Steps:
      1. Read GetPawnMemory method
      2. Verify it checks pawn.IsColonist (or equivalent) before accessing component
      3. Verify it returns "" for non-colonists and when memory system is disabled
    Expected Result: Guard clauses present for non-colonist and disabled state
    Evidence: .sisyphus/evidence/task-3-colony-only-guard.txt
  ```

  **Commit**: YES
  - Message: `feat(memory): integrate [pawn#_memories] placeholder into ExtractPawnData`
  - Files: `SocialInteractions/Core/SocialInteractions.cs`

- [x] 4. LLM Content Capture — Buffer Writes in HandleNonStoppingInteraction + HandleMonologue

  **What to do**:
  - In `SocialInteractions/Core/SocialInteractions.cs`, after the LLM response is received and parsed in `HandleNonStoppingInteraction` (around line 1756 where messages are enqueued to speech bubbles):
    - Get `PawnMemory_GameComponent` via `Current.Game.GetComponent<>()`
    - If memory system enabled and pawn is colonist, call `AddBufferEntry(pawn.thingIDNumber, entryText)`
    - Buffer entry format: `"[InteractionType] with [OtherPawnName]: [brief summary or first line of dialogue]"` — keep entries short (~100-200 chars each)
    - Add buffer entries for BOTH initiator and recipient if both are colonists
  - Same for `HandleMonologue` (around line 1559):
    - Buffer entry format: `"[Monologue about subject]: [first line of monologue text]"`
    - Only for the speaking pawn
  - Same for `HandleJobGiverInteraction` and `HandleCaughtCheatingInteraction` — any method that receives LLM response text
  - Utility helper: `BufferInteractionEvent(Pawn pawn, string eventDescription)` — centralizes the "check enabled + check colonist + add to buffer" logic

  **Must NOT do**:
  - No LLM calls in this task — just buffering raw event descriptions
  - No memory writing — that's Task 5
  - Don't buffer the entire LLM response verbatim — just a brief description of what happened

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Adding a few hook lines to existing methods, plus one utility helper
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 5, 6)
  - **Blocks**: Tasks 5, 7, 8
  - **Blocked By**: Task 1

  **References**:

  **Pattern References**:
  - `SocialInteractions/Core/SocialInteractions.cs:1630-1845` — `HandleNonStoppingInteraction` method. Find where LLM response is parsed (~line 1712-1759) and messages are split. This is the insertion point for buffer writes.
  - `SocialInteractions/Core/SocialInteractions.cs:1451-1628` — `HandleMonologue` method. Find where response text is available after LLM call. Insert buffer write here.
  - `SocialInteractions/Core/SocialInteractions.cs:1847-1927` — `HandleJobGiverInteraction`. Same pattern.
  - `SocialInteractions/Core/SocialInteractions.cs:1325-1354` — `HandleInteraction` entry point. Understand the routing to determine which methods need hooks.

  **Why Each Reference Matters**:
  - `HandleNonStoppingInteraction` is the PRIMARY interaction handler — most LLM dialogue flows through here. The buffer write must go AFTER the response is received but BEFORE or alongside speech bubble enqueuing.
  - `HandleMonologue` handles all single-pawn monologues. Same insertion pattern.

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Buffer writes present in all LLM response handlers
    Tool: Bash
    Preconditions: Core/SocialInteractions.cs modified
    Steps:
      1. Grep for AddBufferEntry or BufferInteractionEvent calls in HandleNonStoppingInteraction
      2. Grep for same in HandleMonologue
      3. Grep for same in HandleJobGiverInteraction
      4. Verify utility helper method exists with colonist check and settings toggle check
    Expected Result: Buffer writes present in all 3+ handler methods, utility helper has guards
    Evidence: .sisyphus/evidence/task-4-llm-capture-hooks.txt

  Scenario: Both participants get buffer entries for dialogues
    Tool: Bash
    Preconditions: Source files modified
    Steps:
      1. In HandleNonStoppingInteraction, verify buffer write happens for BOTH initiator and recipient
      2. Verify colonist check applies to each pawn independently
    Expected Result: Both pawns get buffer entries if both are colonists; only one if only one is colonist
    Evidence: .sisyphus/evidence/task-4-both-participants.txt
  ```

  **Commit**: YES
  - Message: `feat(memory): capture LLM dialogue and monologue events to memory buffer`
  - Files: `SocialInteractions/Core/SocialInteractions.cs`

- [x] 5. Daily Memory Writer — GameComponentTick + Async LLM Processing

  **What to do**:
  - Add `GameComponentTick()` override to `PawnMemory_GameComponent` (or create a separate `SocialInteractions/Memory/DailyMemoryWriter.cs` component if separation of concerns is preferred — follow the pattern most consistent with the codebase)
  - Every tick, check if a full in-game day has elapsed since last processing (`Find.TickManager.TicksGame` — 60000 ticks = 1 day)
  - When daily tick fires:
    - Get all colony pawns with non-empty buffers via `GetAllPawnsWithBufferEntries()`
    - Skip dead/destroyed pawns (clean up their buffers)
    - Process pawns ONE AT A TIME sequentially (not all at once) to avoid LLM overload
    - For each pawn: build the memory writing prompt using the customizable template from settings
    - Replace template placeholders: `[pawn_name]` → pawn's name, `[existing_memories]` → current memory text, `[todays_events]` → joined buffer entries, `[pawn_traits]` → traits, `[pawn_mood]` → current mood label, `[char_limit]` → settings limit
    - Call `LlmClientFactory.Create(settings)` → `client.GenerateText(prompt)` asynchronously
    - On response: update the pawn's memory via `SetMemory(pawnId, responseText)`
    - Clear the pawn's buffer after successful processing
    - On LLM failure: leave buffer intact (will retry next day)
  - Use the existing async pattern from the codebase (likely `Task.Run` or similar — check how `HandleNonStoppingInteraction` does async)
  - Add a processing state flag to prevent overlapping daily runs (if yesterday's processing hasn't finished when today's tick fires, skip)
  - Log processing via `SLog` — "Processing memories for N pawns" at start, success/failure per pawn

  **Must NOT do**:
  - No blocking the game thread — all LLM calls must be async
  - No processing all pawns simultaneously — sequential to avoid LLM overload
  - No compaction in this task — that's Task 9
  - No modifying buffer during async processing without locks

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: Most complex task — async processing, tick management, LLM client integration, error handling, thread safety. Requires understanding the full async pattern used by the mod.
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO — depends on Tasks 1, 2, 3, 4 all completing
  - **Parallel Group**: Wave 2 (but sequential after Wave 1)
  - **Blocks**: Tasks 9, 11
  - **Blocked By**: Tasks 1, 2, 3, 4

  **References**:

  **Pattern References**:
  - `SocialInteractions/Speech/SpeechBubbleManager.cs` (GameComponentTick override) — Shows how to override `GameComponentTick()` in this codebase. Look at tick counting and interval checking patterns.
  - `SocialInteractions/Core/SocialInteractions.cs:1630-1845` — `HandleNonStoppingInteraction` async pattern. Study how it calls `LlmClientFactory.Create()` + `client.GenerateText()` and handles the response. Copy this exact async pattern.
  - `SocialInteractions/Api/LlmClientFactory.cs:7-15` — How to create LLM client from settings.
  - `SocialInteractions/Api/ILlmClient.cs:11-21` — `GenerateText()` signature — `Task<string>` return type, parameters available.

  **API/Type References**:
  - `SocialInteractions/Memory/PawnMemory_GameComponent.cs` (Task 1) — `GetMemory`, `SetMemory`, `GetAndClearBuffer`, `GetAllPawnsWithBufferEntries`
  - `SocialInteractions/Core/SocialInteractionsSettings.cs` (Task 2) — `memoryPromptTemplate`, `memoryCharacterLimit`, `enableMemorySystem`

  **Why Each Reference Matters**:
  - `SpeechBubbleManager.GameComponentTick` is the ONLY existing `GameComponentTick` implementation — it shows the tick-based processing pattern including timing and state management.
  - `HandleNonStoppingInteraction` async pattern is CRITICAL — this is how async LLM calls are done in this codebase. Do NOT invent a new async pattern; copy this one exactly.

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Daily tick fires at correct interval
    Tool: Bash
    Preconditions: GameComponent code present
    Steps:
      1. Grep for GameComponentTick override
      2. Verify tick interval check (60000 ticks or equivalent daily check)
      3. Verify "last processed tick" tracking to prevent re-processing
      4. Verify overlap prevention flag
    Expected Result: Tick override present with daily interval, re-processing guard, overlap guard
    Evidence: .sisyphus/evidence/task-5-daily-tick.txt

  Scenario: Async LLM call follows existing pattern
    Tool: Bash
    Preconditions: Source files present
    Steps:
      1. Verify LlmClientFactory.Create usage
      2. Verify client.GenerateText call is async (Task-based)
      3. Verify LLM failure is handled (buffer not cleared on failure)
      4. Verify SetMemory is called with LLM response on success
      5. Verify buffer is cleared only after successful processing
    Expected Result: Async pattern matches existing code, proper success/failure handling
    Evidence: .sisyphus/evidence/task-5-async-llm.txt

  Scenario: Dead/destroyed pawns are skipped and cleaned up
    Tool: Bash
    Preconditions: Source files present
    Steps:
      1. Grep for dead/destroyed pawn check in daily processing loop
      2. Verify buffer cleanup for dead pawns
    Expected Result: Dead pawns skipped, their buffers cleared
    Evidence: .sisyphus/evidence/task-5-dead-pawn-handling.txt
  ```

  **Commit**: YES
  - Message: `feat(memory): implement daily async memory writer via GameComponentTick`
  - Files: `SocialInteractions/Memory/PawnMemory_GameComponent.cs` (or `SocialInteractions/Memory/DailyMemoryWriter.cs`)

- [x] 6. Bio Editor Memory Tab — Extend Dialog_EditPawnFlavorText

  **What to do**:
  - Modify `SocialInteractions/UI/Dialog_EditPawnFlavorText.cs` to add a second panel/tab for viewing and editing pawn memories
  - Add a tab-like toggle at the top of the dialog: **Bio** | **Memories** (use radio buttons or clickable text headers, since the existing dialog is a simple single-panel window — not a real tabbed interface)
  - When "Bio" tab is selected: show existing bio editor (current behavior, unchanged)
  - When "Memories" tab is selected:
    - Show a read-only text area with the pawn's current memory text (from `PawnMemory_GameComponent.GetMemory(pawnId)`)
    - Show an "Edit" button that enables editing (turns text area into editable text field)
    - Show a "Clear Memories" button that wipes the pawn's memory (with confirmation)
    - Show a "Save" button when in edit mode
  - Default tab should be "Bio" (preserves existing UX for users who don't use memories)
  - If memory system is disabled in settings, hide the Memories tab entirely
  - The memory text area should be scrollable (memories can be up to ~2500 chars)

  **Must NOT do**:
  - No restructuring of the entire dialog — add the tab toggle minimally
  - No RimWorld tab widgets if they don't exist — use simple clickable text/buttons
  - No memory writing logic — this is just viewing/editing the stored text

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
    - Reason: UI work — modifying a dialog window, adding tab navigation, scroll behavior
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 4, 5)
  - **Blocks**: Task 11
  - **Blocked By**: Tasks 1, 2

  **References**:

  **Pattern References**:
  - `SocialInteractions/UI/Dialog_EditPawnFlavorText.cs` — The dialog to extend. Read the entire file to understand: window sizing, `DoWindowContents`, text area rendering, Save/Cancel/Clear buttons. The memory tab should mirror the bio panel structure.
  - `SocialInteractions/Negotiation/Dialog_PawnNegotiation.cs` — Example of a more complex dialog with scrollable content and multiple sections. Study its layout patterns.
  - `SocialInteractions/UI/PawnSelectionDialog.cs` — Example of a scrollable list dialog. Pattern for scroll handling.

  **API/Type References**:
  - `SocialInteractions/Memory/PawnMemory_GameComponent.cs` (Task 1) — `GetMemory(pawnId)`, `SetMemory(pawnId, text)`, `ClearMemory(pawnId)`
  - `SocialInteractions/Core/SocialInteractionsSettings.cs` (Task 2) — `enableMemorySystem` toggle to conditionally show/hide tab

  **Why Each Reference Matters**:
  - `Dialog_EditPawnFlavorText` IS the file being modified. Understanding its full structure is essential to add the tab without breaking existing bio editing.
  - `Dialog_PawnNegotiation` shows how to build more complex dialogs with scrolling and multiple panels in this codebase.

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Dialog has Bio/Memories toggle
    Tool: Bash
    Preconditions: UI/Dialog_EditPawnFlavorText.cs modified
    Steps:
      1. Grep for "Bio" and "Memories" labels/buttons in the dialog
      2. Verify tab state tracking variable exists
      3. Verify DoWindowContents switches content based on active tab
      4. Verify bio editing still works when Bio tab is selected (existing behavior unchanged)
    Expected Result: Two-tab toggle present, content switches, bio editing preserved
    Evidence: .sisyphus/evidence/task-6-bio-tab-toggle.txt

  Scenario: Memory tab shows memory text and has edit/clear buttons
    Tool: Bash
    Preconditions: Source file modified
    Steps:
      1. Grep for GetMemory call in the dialog
      2. Verify scrollable text area for memory display
      3. Verify Edit, Save, Clear buttons exist in memory tab
      4. Verify Clear has confirmation (not one-click delete)
    Expected Result: Memory tab reads from component, has scroll, has edit/save/clear with confirmation
    Evidence: .sisyphus/evidence/task-6-memory-tab-ui.txt

  Scenario: Memory tab hidden when system disabled
    Tool: Bash
    Preconditions: Source file modified
    Steps:
      1. Verify settings check for enableMemorySystem
      2. When disabled, only Bio tab should be visible (no toggle needed)
    Expected Result: Tab toggle only shown when memory system enabled
    Evidence: .sisyphus/evidence/task-6-disabled-hidden.txt
  ```

  **Commit**: YES
  - Message: `feat(memory): add memory tab to Bio editor dialog`
  - Files: `SocialInteractions/UI/Dialog_EditPawnFlavorText.cs`

- [x] 7. Drama Interaction Capture — Badmouthing, Backstabbing, Admiration, Make-Up Hooks

  **What to do**:
  - Hook into each drama InteractionWorker to buffer event descriptions when colony pawns are involved
  - Use the same `BufferInteractionEvent` utility from Task 4
  - **SocialInteractions/Interactions/InteractionWorker_Badmouthing.cs**: After interaction completes, buffer `"Badmouthed [target] to [recipient]. [outcome: bonded/backfired]"`
  - **SocialInteractions/Interactions/InteractionWorker_Backstabbing.cs**: Buffer `"Attempted to backstab [target] by turning [ally] against them. [outcome: success/failure]"`
  - **SocialInteractions/Interactions/InteractionWorker_Admiration.cs**: Buffer `"Expressed admiration for [target]. [type: GeneralPraise/SkillBased/etc.]"`
  - **SocialInteractions/Interactions/InteractionWorker_MakeUp.cs**: Buffer `"Attempted to reconcile with [target]. [outcome: success/failure]"`
  - **SocialInteractions/Interactions/InteractionWorker_EnhancedInsult.cs**: Buffer `"Insulted [target] ([severity]). [escalated to fight: yes/no]"`
  - **SocialInteractions/Interactions/InteractionWorker_LoversQuarrel.cs**: Buffer `"Had a lover's quarrel with [target]. [outcome: reconciled/neutral/near-breakup]"`
  - All entries should include both participants' perspectives where applicable (if both are colonists, both get a buffer entry with their perspective)

  **Must NOT do**:
  - No LLM calls — just buffer text entries
  - No modifying the interaction logic itself — only adding buffer hooks after outcomes are determined

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: Multiple files to modify (6 InteractionWorkers), each needs careful placement of buffer write after outcome determination
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 8, 9, 10)
  - **Blocks**: Task 11
  - **Blocked By**: Tasks 1, 4

  **References**:

  **Pattern References**:
  - `SocialInteractions/Interactions/InteractionWorker_Badmouthing.cs` — Find `Interacted` or `RandomSelectionWeight` methods. Buffer write should go in the method that executes after the interaction outcome is known. Look for where opinion changes are applied.
  - `SocialInteractions/Interactions/InteractionWorker_Backstabbing.cs` — Same pattern. Find success/failure branching point.
  - `SocialInteractions/Interactions/InteractionWorker_Admiration.cs` — Find where admiration type is determined.
  - `SocialInteractions/Interactions/InteractionWorker_MakeUp.cs` — Find success/failure outcome.
  - `SocialInteractions/Interactions/InteractionWorker_EnhancedInsult.cs` — Find severity determination and fight escalation check.
  - `SocialInteractions/Interactions/InteractionWorker_LoversQuarrel.cs` — Find outcome branching (reconciliation/neutral/near-breakup).

  **API/Type References**:
  - `SocialInteractions/Core/SocialInteractions.cs` — `BufferInteractionEvent` utility method from Task 4

  **Why Each Reference Matters**:
  - Each InteractionWorker has its own outcome logic. The buffer write must go AFTER the outcome is determined so the event description accurately reflects what happened.

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Buffer hooks present in all drama InteractionWorkers
    Tool: Bash
    Preconditions: All InteractionWorker files modified
    Steps:
      1. Grep for BufferInteractionEvent (or AddBufferEntry) in each InteractionWorker file
      2. Verify each hook captures outcome information (success/failure/type)
      3. Verify colonist check is applied per pawn
    Expected Result: All 6 InteractionWorkers have buffer hooks with outcome info
    Evidence: .sisyphus/evidence/task-7-drama-hooks.txt

  Scenario: Both participants get entries when both are colonists
    Tool: Bash
    Steps:
      1. In at least one InteractionWorker (e.g., Badmouthing), verify buffer entry for initiator AND recipient
      2. Verify each gets their own perspective
    Expected Result: Dual buffer writes with distinct perspective text
    Evidence: .sisyphus/evidence/task-7-dual-perspective.txt
  ```

  **Commit**: YES
  - Message: `feat(memory): capture drama interaction events to memory buffer`
  - Files: `SocialInteractions/Interactions/InteractionWorker_Badmouthing.cs`, `InteractionWorker_Backstabbing.cs`, `InteractionWorker_Admiration.cs`, `InteractionWorker_MakeUp.cs`, `InteractionWorker_EnhancedInsult.cs`, `InteractionWorker_LoversQuarrel.cs`

- [x] 8. Game Event + Combat Capture — Life Events, Raids, Injuries

  **What to do**:
  - Hook into existing Harmony patches and event handlers to buffer significant game events for colony pawns
  - Use the same `BufferInteractionEvent` utility from Task 4
  - **Marriage** (`SocialInteractions/Patches/MarriageCeremonyStart_Patch.cs`): Buffer `"Married [partner]"`
  - **Birth** (`SocialInteractions/Patches/TaleRecorder_Patch.cs`): Buffer `"[Gave birth to / Helped deliver] [baby name]"`
  - **Mental breaks** (`SocialInteractions/Patches/MentalState_Patch.cs`): Buffer `"Had a mental break: [mental state type]"`
  - **Inspiration** (`SocialInteractions/Patches/InspirationHandler_TryStartInspiration_Patch.cs`): Buffer `"Became inspired: [inspiration type]"`
  - **Masterwork crafting** (`SocialInteractions/Patches/QualityUtility_SendCraftNotification_Patch.cs`): Buffer `"Crafted a [quality] [item]"`
  - **Leadership** (`SocialInteractions/Patches/Faction_Patch.cs`): Buffer `"Became faction leader"`
  - **Animal bonding** (`SocialInteractions/Patches/HistoryEventsManager_Patch.cs`): Buffer `"Bonded with [animal name]"`
  - **Role assignment** (`SocialInteractions/Patches/Precept_RoleMulti_Patch.cs`, `Precept_RoleSingle_Patch.cs`): Buffer `"Assigned role: [role name]"`
  - **Combat** (`SocialInteractions/Combat/CombatPatches.cs`): Buffer `"Fought in combat. [Killed/Injured/Was downed by] [target/attacker]"` — be selective, only major combat events (kills, downing), not every hit
  - **Caught cheating** (`SocialInteractions/Interactions/InteractionWorker_CaughtCheating.cs`): Buffer `"Was caught cheating by [spouse]"` / `"Caught [partner] cheating"`
  - **Date events**: Buffer `"Went on a date with [partner]"` — hook into `SocialInteractions/Dating/DatingManager.StartDate` or `EndDate`

  **Must NOT do**:
  - No buffering trivial events (every single mood tick, minor opinion changes)
  - No creating NEW Harmony patches — only add buffer writes to EXISTING patches
  - No LLM calls

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: Many files to touch (10+ patches), each needs a small addition. Breadth over depth.
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 7, 9, 10)
  - **Blocks**: Task 11
  - **Blocked By**: Tasks 1, 4

  **References**:

  **Pattern References**:
  - `SocialInteractions/Patches/MarriageCeremonyStart_Patch.cs` — Marriage patch. Find where ceremony is detected, add buffer write.
  - `SocialInteractions/Patches/TaleRecorder_Patch.cs` — Birth event patch. Find birth detection.
  - `SocialInteractions/Patches/MentalState_Patch.cs` — Mental break detection. Already triggers monologues — add buffer write alongside.
  - `SocialInteractions/Patches/InspirationHandler_TryStartInspiration_Patch.cs` — Inspiration detection.
  - `SocialInteractions/Patches/QualityUtility_SendCraftNotification_Patch.cs` — Masterwork detection.
  - `SocialInteractions/Patches/Faction_Patch.cs` — Leader selection.
  - `SocialInteractions/Combat/CombatPatches.cs` — Combat events. Be selective — only kills and downing events.
  - `SocialInteractions/Dating/DatingManager.cs` — `StartDate` / `EndDate` methods for date event capture.

  **Why Each Reference Matters**:
  - All these patches ALREADY detect the events — we're just adding a buffer write alongside their existing behavior. No new event detection needed.

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Buffer hooks in major event patches
    Tool: Bash
    Preconditions: Patch files modified
    Steps:
      1. Grep for BufferInteractionEvent/AddBufferEntry across all modified patch files
      2. Verify at least 8 distinct event types are captured (marriage, birth, mental break, inspiration, crafting, leadership, bonding, combat)
      3. Verify colonist-only checks
    Expected Result: 8+ event types captured with colonist guards
    Evidence: .sisyphus/evidence/task-8-event-hooks.txt

  Scenario: Combat events are selective (not every hit)
    Tool: Bash
    Steps:
      1. In CombatPatches.cs, verify buffer writes only for kill/down events, not every damage tick
    Expected Result: Selective combat buffering
    Evidence: .sisyphus/evidence/task-8-combat-selective.txt
  ```

  **Commit**: YES
  - Message: `feat(memory): capture game events and combat to memory buffer`
  - Files: Multiple patch files (see list above)

- [x] 9. Hybrid Compaction — FIFO Truncation + LLM Summarization

  **What to do**:
  - Implement compaction logic that runs AFTER the daily memory writer updates a pawn's memory (in Task 5's daily processing flow, or as a separate method called from there)
  - **Phase 1 — FIFO safety net** (always runs): After memory is updated, check character count against `memoryCharacterLimit` from settings. If exceeded, truncate from the beginning (oldest text) until under limit. This is the simple, fast, always-reliable fallback.
  - **Phase 2 — LLM summarization** (runs when threshold reached): When memory length exceeds `memoryCompactionThreshold` (e.g., 80% of limit), trigger an LLM compaction call:
    - Build compaction prompt using `memoryCompactionPromptTemplate` from settings
    - Replace placeholders: `[pawn_name]`, `[full_memories]`, `[char_limit]`
    - The prompt instructs the LLM to condense the memories while preserving the most important personality-defining events and removing redundancy
    - Call LLM async, replace memory with compacted version on success
    - On LLM failure: fall back to FIFO truncation (always safe)
  - Compaction should run at most once per day per pawn (tracked by the daily processing flag)
  - The FIFO truncation should be smart: try to truncate at sentence boundaries (find last period/newline before the cut point) rather than mid-word
  - Log compaction events via SLog: "Compacted memories for [pawn]: [before chars] → [after chars]"

  **Must NOT do**:
  - No compaction during normal gameplay — only during daily processing
  - No blocking — LLM compaction is async
  - No compaction if memory is below threshold (unnecessary work)

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: Involves async LLM calls, careful text manipulation, threshold logic, error handling with graceful fallback. Needs to integrate cleanly with daily writer from Task 5.
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 7, 8, 10)
  - **Blocks**: Task 11
  - **Blocked By**: Tasks 1, 5

  **References**:

  **Pattern References**:
  - `SocialInteractions/Memory/PawnMemory_GameComponent.cs` (Task 1) — `GetMemory`, `SetMemory` for reading/writing compacted memory
  - `SocialInteractions/Core/SocialInteractions.cs:1630-1845` — Async LLM call pattern. Compaction LLM call follows this same pattern.
  - `SocialInteractions/Api/LlmClientFactory.cs` — Creating LLM client for compaction call.

  **API/Type References**:
  - `SocialInteractions/Core/SocialInteractionsSettings.cs` (Task 2) — `memoryCharacterLimit`, `memoryCompactionThreshold`, `memoryCompactionPromptTemplate`

  **Why Each Reference Matters**:
  - The async LLM call pattern from `HandleNonStoppingInteraction` MUST be followed. Compaction is essentially another async LLM call with a different prompt.
  - Settings thresholds determine when compaction triggers — these must be respected exactly.

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: FIFO truncation works at sentence boundaries
    Tool: Bash
    Preconditions: Compaction code present
    Steps:
      1. Grep for truncation logic (character limit check, boundary detection)
      2. Verify it finds sentence boundaries (period, newline) rather than cutting mid-word
      3. Verify it runs AFTER daily memory update
    Expected Result: Smart truncation present with sentence boundary detection
    Evidence: .sisyphus/evidence/task-9-fifo-truncation.txt

  Scenario: LLM compaction triggers at threshold
    Tool: Bash
    Steps:
      1. Verify threshold comparison against memoryCompactionThreshold
      2. Verify LLM call uses memoryCompactionPromptTemplate
      3. Verify fallback to FIFO on LLM failure
      4. Verify compaction runs at most once per day per pawn
    Expected Result: Threshold check present, template used, fallback exists, frequency limited
    Evidence: .sisyphus/evidence/task-9-llm-compaction.txt
  ```

  **Commit**: YES
  - Message: `feat(memory): implement hybrid FIFO + LLM compaction`
  - Files: `SocialInteractions/Memory/MemoryCompactor.cs` (or method in PawnMemory_GameComponent)

- [x] 10. Settings UI — Add Memory Section to Mod Settings Page

  **What to do**:
  - In `SocialInteractions/Core/SocialInteractionsSettings.cs`, in the `DoSettingsWindowContents` method (the mod settings page), add a new section for Memory settings
  - Follow the existing layout pattern (labeled sections with toggles and sliders)
  - UI elements:
    - **Checkbox**: "Enable Pawn Memory System" (maps to `enableMemorySystem`)
    - **Slider**: "Memory Character Limit" (range: 500-5000, step 100, maps to `memoryCharacterLimit`)
    - **Slider**: "Compaction Threshold" (range: 500-5000, step 100, maps to `memoryCompactionThreshold`). Add validation: must be less than character limit
    - **Slider**: "Buffer Entry Cap" (range: 10-100, step 5, maps to `memoryBufferEntryCap`)
    - **Text area**: "Memory Prompt Template" — large editable text field for the memory writing template
    - **Text area**: "Compaction Prompt Template" — large editable text field for the compaction template
    - **Button**: "Reset Memory Templates" — resets both templates to defaults (like existing "Reset Templates" button for dialogue)
  - Place the Memory section logically near existing prompt/LLM settings
  - Add localization keys in `SocialInteractions/Languages/English/Keyed/Keyed.xml` for all labels

  **Must NOT do**:
  - No complex template validation
  - No runtime memory processing — this is purely UI

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Following existing settings UI patterns exactly — checkboxes, sliders, text areas
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 7, 8, 9)
  - **Blocks**: None
  - **Blocked By**: Task 2

  **References**:

  **Pattern References**:
  - `SocialInteractions/Core/SocialInteractionsSettings.cs` — `DoSettingsWindowContents` method. Study the existing layout: how sections are labeled, how checkboxes and sliders are rendered, how text areas are done.
  - `SocialInteractions/Languages/English/Keyed/Keyed.xml` — Existing localization keys. Follow naming convention for new keys.

  **Why Each Reference Matters**:
  - `DoSettingsWindowContents` IS the method to modify. Must match existing visual style exactly.
  - Keyed.xml naming convention must be followed for consistency.

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Memory settings section exists in mod settings
    Tool: Bash
    Steps:
      1. Grep DoSettingsWindowContents for memory-related UI elements
      2. Verify checkbox for enableMemorySystem
      3. Verify sliders for characterLimit and compactionThreshold
      4. Verify text areas for both prompt templates
      5. Verify "Reset" button for templates
    Expected Result: All UI elements present in settings page
    Evidence: .sisyphus/evidence/task-10-settings-ui.txt

  Scenario: Localization keys added
    Tool: Bash
    Steps:
      1. Grep SocialInteractions/Languages/English/Keyed/Keyed.xml for memory-related keys
      2. Verify all UI labels have corresponding localization entries
    Expected Result: All new labels have localization keys
    Evidence: .sisyphus/evidence/task-10-localization.txt
  ```

  **Commit**: YES
  - Message: `feat(memory): add memory settings to mod settings UI`
  - Files: `SocialInteractions/Core/SocialInteractionsSettings.cs`, `SocialInteractions/Languages/English/Keyed/Keyed.xml`

- [x] 11. Unit Tests — Compaction, Buffer, Size Limits

  **What to do**:
  - Add unit tests in `SocialInteractions.Tests/` for the memory system's core logic
  - Discover the existing test framework by examining the test project (likely NUnit, xUnit, or MSTest)
  - Test cases to write:
    - **Buffer management**: AddBufferEntry adds entries, GetAndClearBuffer returns and clears, buffer cap enforcement (51st entry drops oldest)
    - **FIFO truncation**: Memory exceeding char limit gets truncated from start; truncation respects sentence boundaries
    - **Memory storage**: GetMemory returns empty for unknown pawn, SetMemory stores and retrieves correctly, ClearMemory wipes data
    - **Colony-only guard**: BufferInteractionEvent skips non-colonist pawns (may need mock/stub of Pawn.IsColonist)
    - **Settings integration**: Disabled memory system returns empty from GetPawnMemory
    - **Thread safety**: Concurrent buffer add + read doesn't corrupt data (if testable without game runtime)
  - Note: Some tests may be hard to write without the RimWorld game runtime (e.g., Pawn objects, GameComponent lifecycle). Focus on logic that can be tested in isolation — extract pure functions where possible.

  **Must NOT do**:
  - No testing LLM calls (external dependency)
  - No testing UI rendering
  - No integration tests requiring game runtime (unless test infrastructure already supports it)

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: Needs to discover test framework, understand what's testable without game runtime, write meaningful tests
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO — needs all implementation tasks complete
  - **Parallel Group**: Wave 4
  - **Blocks**: Task 12
  - **Blocked By**: Tasks 5, 6, 7, 8, 9

  **References**:

  **Pattern References**:
  - `SocialInteractions.Tests/` — Existing test project. Examine to discover: test framework, naming conventions, mock patterns, what dependencies are available (or stubbed).
  - `SocialInteractions/Memory/PawnMemory_GameComponent.cs` (Task 1) — The main class to test.

  **Why Each Reference Matters**:
  - The test project shows exactly how tests are structured in this codebase — follow the same framework, naming, and patterns.

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Tests compile and pass
    Tool: Bash
    Steps:
      1. Run dotnet test (or equivalent) for SocialInteractions.Tests
      2. Verify all new memory tests pass
      3. Verify no existing tests are broken
    Expected Result: All tests pass, 0 failures
    Evidence: .sisyphus/evidence/task-11-test-results.txt

  Scenario: Adequate test coverage for core logic
    Tool: Bash
    Steps:
      1. Count number of test methods targeting memory system
      2. Verify at least: 2 buffer tests, 2 compaction tests, 2 storage tests, 1 guard test
    Expected Result: At least 7 test methods covering core memory logic
    Evidence: .sisyphus/evidence/task-11-test-coverage.txt
  ```

  **Commit**: YES
  - Message: `test(memory): add unit tests for compaction, buffer, size limits`
  - Files: `SocialInteractions.Tests/MemoryTests.cs` (or similar)

- [x] 12. Architecture Documentation Update

  **What to do**:
  - Update `architecture.md` with a new "Memory/" section following the existing documentation style
  - Document:
    - **Memory/ directory** in the Directory Structure section
    - **PawnMemory_GameComponent** — storage model, buffer model, persistence mechanism
    - **Daily Memory Writer** — tick-based processing, async LLM call, prompt template usage
    - **Hybrid Compaction** — FIFO + LLM summarization logic, thresholds
    - **Event Capture** — list all hook points (interactions, patches, events) with brief descriptions
    - **Data Flow** — Add a "Memory System" data flow example (like the existing "Starting a Date" example):
      1. Event occurs → buffer entry added
      2. Daily tick fires → buffer read + existing memory loaded
      3. Memory writing prompt built from template
      4. LLM call → response becomes new memory
      5. Compaction check → truncate/summarize if needed
      6. Next interaction → `[pawn#_memories]` injected into prompt
    - **New Placeholders** — document `[pawn#_memories]` in the placeholder list
    - **Settings** — document new memory settings fields
  - Also add entry to the Harmony Patches table if any new patches were created (unlikely — we're extending existing ones)

  **Must NOT do**:
  - No code changes — documentation only
  - No restructuring existing docs sections

  **Recommended Agent Profile**:
  - **Category**: `writing`
    - Reason: Pure documentation task following existing format
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 4 (with Task 11)
  - **Blocks**: None
  - **Blocked By**: Task 11 (needs to know final implementation details)

  **References**:

  **Pattern References**:
  - `SocialInteractions/architecture.md` — The entire file. Follow its existing section format, heading hierarchy, table structure, and data flow example style.

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Architecture doc updated with Memory section
    Tool: Bash
    Steps:
      1. Grep SocialInteractions/architecture.md for "Memory" section header
      2. Verify PawnMemory_GameComponent documented
      3. Verify data flow example present
      4. Verify [pawn#_memories] placeholder documented
      5. Verify Directory Structure includes Memory/
    Expected Result: Complete Memory section following existing doc format
    Evidence: .sisyphus/evidence/task-12-architecture-doc.txt
  ```

  **Commit**: YES
  - Message: `docs(memory): update architecture.md with Memory system section`
  - Files: `SocialInteractions/architecture.md`

---

## Final Verification Wave

> 4 review agents run in PARALLEL. ALL must APPROVE. Rejection → fix → re-run.

- [ ] F1. **Plan Compliance Audit** — `oracle`
  Read the plan end-to-end. For each "Must Have": verify implementation exists (read file, check code). For each "Must NOT Have": search codebase for forbidden patterns — reject with file:line if found. Check evidence files exist in `.sisyphus/evidence/`. Compare deliverables against plan.
  Output: `Must Have [N/N] | Must NOT Have [N/N] | Tasks [N/N] | VERDICT: APPROVE/REJECT`

- [ ] F2. **Code Quality Review** — `unspecified-high`
  Build the mod via `dotnet build SocialInteractions/SocialInteractions.csproj`. Review all new/changed files for: `as any`/`@ts-ignore` equivalents, empty catches, `Console.WriteLine` in prod (should use `SLog`), commented-out code, unused imports. Check AI slop: excessive comments, over-abstraction, generic names. Verify C# 5 compatibility (no `async`/`await` if constrained, no null-conditional if constrained — verify actual constraint first).
  Output: `Build [PASS/FAIL] | Files [N clean/N issues] | VERDICT`

- [ ] F3. **Build + Integration QA** — `unspecified-high` 
  Build the full DLL via `dotnet build SocialInteractions/SocialInteractions.csproj`. Verify all new `.cs` files are compiled (check build output). Check that `PawnMemory_GameComponent` is properly registered. Verify `ExtractPawnData` includes `[pawn#_memories]`. Verify settings are saveable. Check `ExposeData()` round-trip logic.
  Output: `Build [PASS/FAIL] | Components [N/N registered] | Persistence [N/N verified] | VERDICT`

- [ ] F4. **Scope Fidelity Check** — `deep`
  For each task: read "What to do", read actual code. Verify 1:1 — everything in spec was built, nothing beyond spec was built. Check "Must NOT do" compliance. Detect cross-task contamination. Flag unaccounted changes.
  Output: `Tasks [N/N compliant] | Contamination [CLEAN/N issues] | VERDICT`

---

## Commit Strategy

| After Task(s) | Commit Message | Key Files |
|---------------|----------------|-----------|
| 1 | `feat(memory): add PawnMemory GameComponent with storage and persistence` | SocialInteractions/Memory/PawnMemory_GameComponent.cs |
| 2 | `feat(memory): add memory settings, toggle, char limit, prompt template` | SocialInteractions/Core/SocialInteractionsSettings.cs |
| 3 | `feat(memory): integrate [pawn#_memories] placeholder into ExtractPawnData` | SocialInteractions/Core/SocialInteractions.cs |
| 4 | `feat(memory): capture LLM dialogue and monologue events to memory buffer` | SocialInteractions/Core/SocialInteractions.cs |
| 5 | `feat(memory): implement daily async memory writer via GameComponentTick` | SocialInteractions/Memory/DailyMemoryWriter.cs (or in GameComponent) |
| 6 | `feat(memory): add memory tab to Bio editor dialog` | SocialInteractions/UI/Dialog_EditPawnFlavorText.cs |
| 7 | `feat(memory): capture drama interaction events to memory buffer` | SocialInteractions/Interactions/InteractionWorker_*.cs |
| 8 | `feat(memory): capture game events and combat to memory buffer` | SocialInteractions/Patches/*.cs |
| 9 | `feat(memory): implement hybrid FIFO + LLM compaction` | SocialInteractions/Memory/MemoryCompactor.cs |
| 10 | `feat(memory): add memory settings to mod settings UI` | SocialInteractions/Core/SocialInteractionsSettings.cs |
| 11 | `test(memory): add unit tests for compaction, buffer, size limits` | SocialInteractions.Tests/ |
| 12 | `docs(memory): update architecture.md with Memory system section` | SocialInteractions/architecture.md |

---

## Success Criteria

### Verification Commands
```bash
# Build succeeds
dotnet build SocialInteractions/SocialInteractions.csproj
# All unit tests pass
dotnet test SocialInteractions.Tests/SocialInteractions.Tests.csproj
```

### Final Checklist
- [ ] Colony pawns accumulate events → daily LLM digest → memory persists across save/load
- [ ] `[pawn#_memories]` works in prompt templates (dialogue + monologue)
- [ ] Compaction prevents memory from exceeding character limit
- [ ] Player can view/edit memories via Bio editor tab
- [ ] Feature toggle works (disable → no buffer writes, no daily processing, placeholder returns empty)
- [ ] No save corruption — clean save/load round-trip
- [ ] No blocking — daily processing is fully async
- [ ] All "Must NOT Have" items verified absent
- [ ] SocialInteractions/architecture.md updated with Memory system section
