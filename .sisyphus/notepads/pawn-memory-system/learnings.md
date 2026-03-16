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
