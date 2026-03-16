# Pawn Memory System - Learnings

## Task 1: PawnMemory_GameComponent - COMPLETED

### Key Patterns Applied
1. **Memory Storage**: `Dictionary<int, string>` following PawnFlavorText_GameComponent pattern
   - Uses pawn.thingIDNumber as key (not Pawn reference)
   - Simple string values for memory content

2. **Buffer Serialization**: Manual dictionary-with-list serialization following VoiceAssignmentManager pattern
   - Cannot use simple `Scribe_Collections.Look` for `Dictionary<int, List<string>>`
   - Must manually handle Saving and PostLoadInit modes
   - Separate lists for keys and values during serialization

3. **Thread Safety**: Lock-based synchronization for buffer access
   - `private readonly object bufferLock = new object()`
   - All buffer dictionary operations wrapped in `lock (bufferLock) { ... }`
   - Memory dictionary is main-thread only (no locking needed)

4. **Service Registration**: Component registers itself in constructor
   - `Services.Memory = this;` in `PawnMemory_GameComponent(Game game)` constructor
   - Services.cs updated with `public static Memory.PawnMemory_GameComponent Memory { get; set; }`
   - Services.Reset() updated to clear Memory reference

### Buffer Capacity Implementation
- Constant: `private const int BufferCapacity = 50;`
- Enforcement: When buffer exceeds capacity, drop oldest entries
- Implementation: `buffer[pawnId].RemoveRange(0, buffer[pawnId].Count - BufferCapacity);`

### API Methods Implemented
- `GetMemory(int pawnId)` - Returns memory string or empty string
- `SetMemory(int pawnId, string memory)` - Sets or removes memory
- `AddBufferEntry(int pawnId, string entry)` - Thread-safe buffer append with capacity enforcement
- `GetAndClearBuffer(int pawnId)` - Thread-safe retrieval and clearing
- `GetAllPawnsWithBufferEntries()` - Thread-safe list of pawns with non-empty buffers
- `ClearMemory(int pawnId)` - Clears both memory and buffer for pawn

### Build Status
- ✅ Build succeeds with 0 errors, 0 warnings
- ✅ All API methods present and verified
- ✅ ExposeData() with proper Scribe_Collections calls
- ✅ Thread safety with locks on all buffer operations
- ✅ Services registration complete

### File Structure
- Location: `SocialInteractions/Memory/PawnMemory_GameComponent.cs`
- Size: 6.5KB
- Namespace: `SocialInteractions.Memory`
- Base class: `GameComponent`

### Dependencies Unblocked
- Task 3: PawnMemory_Patch (depends on this component)
- Task 4: Memory persistence tests
- Tasks 5-11: All memory-related features

## Task 2: Memory Settings Implementation

### Completed
- Added 4 new fields to FeatureToggles class:
  - `enableMemorySystem` (bool, default true)
  - `memoryCharacterLimit` (int, default 2500)
  - `memoryCompactionThreshold` (int, default 2000)
  - `memoryBufferEntryCap` (int, default 50)

- Added 2 new fields to PromptSettings class:
  - `memoryPromptTemplate` (string, uses DEFAULT_MEMORY_WRITING_TEMPLATE)
  - `memoryCompactionPromptTemplate` (string, uses DEFAULT_MEMORY_COMPACTION_TEMPLATE)

- Created 2 default template constants:
  - DEFAULT_MEMORY_WRITING_TEMPLATE: Instructs LLM to write memory entries in first-person, integrate events with existing memories, maintain personality, stay under char limit
  - DEFAULT_MEMORY_COMPACTION_TEMPLATE: Instructs LLM to condense memories while preserving personality-defining events and relationships

- Added all Scribe_Values.Look calls for serialization in both FeatureToggles.ExposeData() and PromptSettings.ExposeData()

### Pattern Notes
- Followed existing FeatureToggles pattern for boolean/numeric settings (lines 184-270)
- Followed existing PromptSettings pattern for template storage (lines 272-291)
- All new fields properly initialized with defaults
- All new fields properly serialized via Scribe_Values.Look

### Template Placeholders
Memory Writing Template uses:
- [pawn_name], [existing_memories], [todays_events], [pawn_traits], [pawn_mood], [char_limit]

Memory Compaction Template uses:
- [pawn_name], [full_memories], [char_limit]

### Build Status
- Pre-existing error in SocialInteractions.cs (line 817) unrelated to these changes
- All new settings fields compile correctly
- No new compilation errors introduced

## Task 5: Daily Memory Writer via GameComponentTick

### Core Implementation
- Added `GameComponentTick()` override in `PawnMemory_GameComponent` with a daily interval gate:
  - `DailyMemoryProcessingIntervalTicks = 60000`
  - Tick source: `Find.TickManager.TicksGame`
  - Reprocessing guard: `lastDailyMemoryProcessingTick`
  - Overlap guard: `isProcessingDailyMemories`

- Daily writer runs asynchronously via `Task.Run(async () => ...)` to avoid blocking the game thread.

- Daily processing is sequential (single `foreach` + awaited `GenerateText`) to avoid parallel LLM overload.

### LLM Pattern Applied
- Client creation follows project pattern: `using (ILlmClient client = LlmClientFactory.Create(SocialInteractions.Settings))`
- Per pawn, async call uses `await client.GenerateText(prompt)`
- Prompt built from `Settings.Memory.memoryPromptTemplate` with placeholders:
  - `[pawn_name]`, `[existing_memories]`, `[todays_events]`, `[pawn_traits]`, `[pawn_mood]`, `[char_limit]`

### Pawn Selection / Safety
- Uses `GetAllPawnsWithBufferEntries()` to discover candidate pawn IDs.
- Resolves pawn by ID via `PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead`.
- Skips and cleans buffer for invalid/dead/non-spawned pawns.

### Failure Semantics
- Buffer is retrieved atomically via `GetAndClearBuffer(pawnId)`.
- On LLM null/empty response or exception, entries are restored with `RestoreBufferEntries(...)` so retry can happen next day.
- On success, memory is updated via `SetMemory(pawnId, responseText.Trim())`.

### Logging
- Start log: `Processing memories for N pawns`
- Per-pawn success and failure logs added with `SLog.Message` / `SLog.Warning`.

### Verification Evidence
- `.sisyphus/evidence/task-5-daily-tick.txt`
- `.sisyphus/evidence/task-5-async-llm.txt`
- `.sisyphus/evidence/task-5-dead-pawn-handling.txt`
- Build: `.sisyphus/evidence/task-5-build.txt` (0 warnings, 0 errors)

## Task 6: Bio Tab Toggle
- Used `Widgets.ButtonText` with `GUI.color` to create a simple tab toggle instead of RimWorld's `TabDrawer` which requires a specific layout structure.
- Split `DoWindowContents` into `DrawBioTab` and `DrawMemoriesTab` to keep the code clean and maintainable.
- Ensured the tab toggle is hidden when `SocialInteractions.Settings.Memory.enableMemorySystem` is disabled, preserving the original UI behavior.

## Task 9: Hybrid Compaction (FIFO + LLM)

### Implementation notes
- Added `CompactMemory(int pawnId)` and `ShouldCompact(int pawnId)` in `PawnMemory_GameComponent`.
- Daily writer now calls compaction immediately after successful memory write (`SetMemory(...)` then `await CompactMemory(...)`).

### FIFO safety net pattern
- FIFO truncation is centralized in `ApplyFifoTruncation(string memory, int charLimit)`.
- Truncation removes from the beginning and tries to cut on sentence boundaries via `LastIndexOfAny(new[] { '.', '\n' }, cutPoint - 1)`.
- Safety net always executes within compaction flow to enforce hard character limit even if summarization fails.

### LLM compaction pattern
- Trigger gate: `ShouldCompact` uses `memoryCompactionThreshold` (fallback to 80% of hard limit if threshold <= 0).
- Prompt builder replaces `[pawn_name]`, `[full_memories]`, `[char_limit]` from `memoryCompactionPromptTemplate`.
- LLM call uses async existing pattern (`using ILlmClient ... await client.GenerateText(...)`).
- On errors, logs warning and falls back to FIFO truncation path.

### Frequency guard
- Added per-pawn once-per-day gate with `lastCompactionDayByPawn` dictionary.
- `CanAttemptCompactionToday` + `MarkCompactionAttemptForToday` prevent multiple compaction attempts for same pawn/day.
- Dictionary is persisted via `Scribe_Collections.Look`.

### Logging and evidence
- Compaction log format implemented: `Compacted memories for [pawn]: [before] → [after]`.
- Evidence files added:
  - `.sisyphus/evidence/task-9-fifo-truncation.txt`
  - `.sisyphus/evidence/task-9-llm-compaction.txt`

## Task 7: Drama InteractionWorker Buffer Hooks

### Implementation Pattern
- Used existing `BufferInteractionEvent(Pawn, string)` helper from Task 4 (line 1690 in SocialInteractions.cs)
- Helper already handles: null checks, memory system enabled check, colonist check, dead/destroyed check, Services.Memory access
- No need to create new helper — it already existed from Task 4's wave

### Hook Placement Strategy
- All hooks placed AFTER outcome is determined and thoughts are applied
- Hooks placed BEFORE or alongside `HandleNonStoppingInteraction` calls (where outcome is already known)
- For Backstabbing: hooked in `ExecuteDirectBackstabbing` after success/failure is known
- For EnhancedInsult: hooked after both severity and fight escalation are computed

### Dual Perspective Pattern
- Every interaction writes TWO buffer entries: one for initiator, one for recipient
- Each pawn's entry uses first-person perspective ("I insulted..." vs "...insulted me")
- This gives the LLM distinct context per pawn for daily memory writing

### Files Modified (6)
1. `InteractionWorker_Badmouthing.cs` — 3 outcomes × 2 entries = 6 buffer calls (gossip/believed/backfired)
2. `InteractionWorker_Backstabbing.cs` — 2 outcomes × 2 entries = 4 buffer calls (success/failure)
3. `InteractionWorker_Admiration.cs` — 2 outcomes × 2 entries = 4 buffer calls (well-received/fell-flat, with type label)
4. `InteractionWorker_MakeUp.cs` — 2 outcomes × 2 entries = 4 buffer calls (success/failure)
5. `InteractionWorker_EnhancedInsult.cs` — 1 path × 2 entries = 2 buffer calls (severity + fight note in both)
6. `InteractionWorker_LoversQuarrel.cs` — 3 outcomes × 2 entries = 6 buffer calls (reconciled/neutral/near-breakup)

### Gotcha: Duplicate Helper
- Initially tried to create a new `BufferInteractionEvent` in SocialInteractions.cs
- Build failed with CS0111 (duplicate member) — Task 4 had already created it
- The initial grep missed it because `mcp_grep` tool didn't search file content properly; used `bash grep -rn` to find all occurrences

## Task 8: Game Event + Combat Capture

### Key Findings
- `BufferInteractionEvent` was created as `private static` by Task 4 — changed to `public static` for cross-namespace access
- CombatPatches `PostApplyDamage` early-returns if pawn is downed (`pawn.Downed` check), so kill/down buffer writes must go BEFORE existing taunt logic
- `HistoryEventsManager_Patch` uses `HistoryEventArgsNames.Subject` to get the bonded animal (attempted; may be null depending on event args)
- `TaleRecorder_Patch` receives birth args as `object[]` — mother at index 0, baby at index 1

### Patterns
- All buffer writes use `SocialInteractions.BufferInteractionEvent(pawn, description)` — centralized guard for colonist-only + settings toggle
- Both participants get buffer entries for two-party events (marriage, cheating, dates) with their own perspective
- Combat is selective: only fires on `pawn.Dead` or `pawn.Downed` checks in PostApplyDamage, plus MakeDowned for the victim's perspective

### Files Modified (12)
1. `Core/SocialInteractions.cs` — made `BufferInteractionEvent` public
2. `Patches/MarriageCeremonyStart_Patch.cs` — "Married [partner]"
3. `Patches/TaleRecorder_Patch.cs` — "Gave birth to [baby]" / "Helped deliver [baby]"
4. `Patches/MentalState_Patch.cs` — "Had a mental break: [type]"
5. `Patches/InspirationHandler_TryStartInspiration_Patch.cs` — "Became inspired: [type]"
6. `Patches/QualityUtility_SendCraftNotification_Patch.cs` — "Crafted a [quality] [item]"
7. `Patches/Faction_Patch.cs` — "Became faction leader"
8. `Patches/HistoryEventsManager_Patch.cs` — "Bonded with [animal]"
9. `Patches/Precept_RoleMulti_Patch.cs` — "Assigned role: [role]"
10. `Patches/Precept_RoleSingle_Patch.cs` — "Assigned role: [role]"
11. `Combat/CombatPatches.cs` — selective kills/downs only
12. `Interactions/InteractionWorker_CaughtCheating.cs` — "Was caught cheating by [spouse]"
13. `Dating/DatingManager.cs` — "Went on a date with [partner]"
## Task 12: Architecture Documentation
- Updated `SocialInteractions/architecture.md` with comprehensive documentation of the Pawn Memory system.
- Documented `PawnMemory_GameComponent` storage (memories/buffer), daily async processing, and hybrid compaction (LLM + FIFO).
- Added a detailed Data Flow section for the Memory system.
- Verified integration points: `BufferInteractionEvent` for capture and `[pawn#_memories]` for prompt injection.
- Matched existing documentation style and hierarchy.
