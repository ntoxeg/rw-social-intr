# Social Interactions Mod Architecture

## Overview

The SocialInteractions mod enhances RimWorld's social dynamics by integrating LLM-generated dialogue, adding a complex dating and cheating system, implementing combat taunts, raid negotiation mechanics, child misbehavior systems, and drama interactions (badmouthing, backstabbing, admiration, etc.). It uses Harmony patches to intercept and modify vanilla game behavior.

## Development Notes
When debugging RimWorld Harmony patches, if a patch isn't applying, follow these steps:
1.  Verify the target method signature in the `[HarmonyPatch]` attribute is perfect. Use a decompiler to get the exact signature from the game's assembly.
2.  The decompiled file very large, use a helper script (like `extract_class.py`) to extract just the class definition needed, or use read_chunk.py to read a portion of it
3.  If patching a method directly fails, try patching a higher-level method that calls it.
4.  For private methods, use `AccessTools.Method` to get the `MethodInfo` for the patch attribute.
5.  To access private fields within a patch, use `Traverse.Create(__instance).Field("fieldName").GetValue<FieldType>()`.
6.  Ensure the C# language version used in the mod code is compatible with the compiler version being used (the compiler only supports C# 5).
7.  Crucially, always add any new `.cs` source files to the `compile.rsp` response file so they are included in the compilation.
8.  Going forward, for all logging in the SocialInteractions mod, use the custom SLog class (SLog.Message, SLog.Warning, SLog.Error) instead of Verse.Log. This is to ensure consistency and allow for verbose logging control via mod settings.
9.  Freely generate and use helper python scrips if the basic CLI tools fail

### Line Replacement Utility

For precise code modifications, a Python tool for code editing that bypasses exact matching using line numbers and patterns has been provided smart_edit.py

Usage:
    smart_edit.py <file> insert_at_line <line_number> <content_file>
    smart_edit.py <file> replace_block <start_line> <end_line> <content_file>
    smart_edit.py <file> insert_after_pattern <pattern> <content_file>
    smart_edit.py <file> insert_before_pattern <pattern> <content_file>
    smart_edit.py <file> delete_lines <start_line> <end_line>
    smart_edit.py <file> wrap_block <start_line> <end_line> <prefix> <suffix>
    smart_edit.py <file> undo

This utility is especially helpful for making targeted modifications to files when the basic edit tool fails repeatedly


## Directory Structure

```
SocialInteractions/
├── About/                  # Mod metadata (About.xml)
├── 1.5/                    # RimWorld 1.5 version-specific content
│   ├── Assemblies/         # Compiled DLL output
│   └── Defs/               # XML definitions (interactions, jobs, thoughts, hediffs, etc.)
│       ├── JobDefs/         # Job definitions (backstabbing, children, chat, misc)
│       ├── MainTabDefs/     # Chat log main tab definition
│       ├── RulePacks/       # Drama and make-up rule packs
│       └── ThingDefs/       # Pauseable mote definition
├── Api/                    # LLM API client layer (interface, base, factory, 9 providers)
├── Children/               # Child misbehavior system (manager, tracker, job drivers, mental states)
├── Combat/                 # Combat taunt patches
├── Components/             # GameComponents and utility classes
├── Core/                   # Entry point, settings, logging, assembly info
├── Dating/                 # Dating system (manager, trackers, job drivers, joy givers, thoughts)
├── DefOfs/                 # DefOf static references (jobs, hediffs, interactions, traits, etc.)
├── Defs/                   # Non-versioned defs (main tabs, TTS mute)
│   └── MainTabDefs/        # TTS mute toggle tab definition
├── Interactions/           # Custom InteractionWorkers and PlayLogEntries
├── Jobs/                   # General-purpose job drivers and joy givers
├── Languages/              # Localization (English, ChineseSimplified)
│   ├── English/Keyed/
│   └── ChineseSimplified/Keyed/
├── Negotiation/            # Raid negotiation system (manager, dialog, lord jobs, patches)
├── Patches/                # Harmony patches (30+ patch files)
├── Speech/                 # Speech bubble display, TTS, voice assignment, pauseable motes
└── UI/                     # Chat log windows, pawn/voice selection dialogs, bio editor
```

## Core Components

### Core/ — Entry Point, Settings, and Logging

#### `SocialInteractions.cs`
- **Static class** managing mod-wide state and core functionality.
- **Harmony Patches**: Applies all Harmony patches on startup.
- **LLM Interaction Logic**:
  - `IsLlmInteractionEnabled`, `IsLlmJobEnabled`: Determine if an interaction/job should use the LLM based on extensive settings.
  - `GenerateDeepTalkPrompt`, `GenerateMonologuePrompt`: Constructs detailed prompts for the LLM using pawn (traits, mood, genes, skills, etc.) and world data (date, time, weather). Includes recent conversation history via the `[pawn1_journal]` and `[pawn2_journal]` placeholders.
  - `HandleInteraction`, `HandleNonStoppingInteraction`, `HandleJobGiverInteraction`, `HandleMonologue`: Entry points for triggering LLM interactions, managing asynchronous calls, parsing responses, and queuing speech bubbles.
  - `HandleCaughtCheatingInteraction`: A special handler that holds the cheating pawn in place, triggers a specific LLM interaction, and schedules a delayed fight between the pawns.
  - `HandleThreewayLovinInteraction`: Handles special 3p action scenarios with LLM dialogue.
  - Text utility methods (`WrapText`, `EstimateReadingTime`, `RemoveRichTextTags`, `FormatLlmText`).
- **Pawn Data Helpers**: Private methods (`GetRelationship`, `GetDislikes`, `GetAfflictions`, etc.) to extract relevant pawn information for prompts. Includes `GetLastSocialLogEntry` to extract recent conversation history between pawns. Also includes `GetPawnFlavorText` and `SetPawnFlavorText` for custom bio text management. The custom bio text is integrated into the prompt system through the `[pawn#_bio]` placeholder in the `ExtractPawnData` method.
- **Custom Pawn Bio System**: Static dictionary `PawnFlavorTexts` for storing bio text, with `GetPawnFlavorText` and `SetPawnFlavorText` methods for retrieval and storage using pawn IDs as keys.

#### `SocialInteractionsSettings.cs`
- **`SocialInteractionsModSettings`**: Holds all configurable options (API keys, flags for features/interactions, prompt template, UI/UX settings).
- **`SocialInteractionsMod`**: Implements the in-game settings UI.
- **Extensive Configuration**: Numerous settings for fine-tuning all aspects of the mod's behavior, from dating mechanics to LLM parameters.
- **Multi-API Support**: Configuration options for different LLM API types with their specific settings.

#### `SLog.cs`
- **Static class** providing a wrapper around `Verse.Log` with a verbosity toggle based on mod settings.
- **Conditional Logging**: Only outputs messages when verbose logging is enabled in the mod settings.

#### `AssemblyInfo.cs`
- Standard assembly metadata for the mod DLL.

### Api/ — LLM Client Layer

A clean abstraction layer for communicating with multiple LLM providers. Uses an interface → abstract base → concrete client hierarchy with a factory for instantiation.

#### `ILlmClient.cs`
- **Interface** defining the contract for all LLM clients.
- **Key Method**: `GenerateText()` — async method accepting prompt, max length, temperature, stop sequences, and sampling parameters (XTC, top-k, top-p, min-p, repetition penalty).

#### `LlmClientBase.cs`
- **Abstract base class** implementing `ILlmClient`.
- **Shared Infrastructure**: HTTP client management, JSON serialization/deserialization, error handling, response cleaning (removes thinking tags).
- **Template Method Pattern**: Subclasses implement `BuildRequestBody()` and `ExtractText()` for API-specific formats.
- **Utility Methods**: `BuildStopSequenceList()`, `CleanChatResponse()`, `IsValidHeaderValue()`.

#### `LlmClientFactory.cs`
- **Factory class** that creates the appropriate API client based on `LlmApiType` setting.
- **Supported Types**: KoboldCpp, Ollama, LMStudio, OpenAI, Gemini, Qwen, Deepseek, Grok, Claude, Player2.
- **Player2 Heartbeat**: Special `UpdatePlayer2Heartbeat()` method for health/usage tracking.

#### Concrete Clients

| Client | Auth Header | Endpoint | Thinking Support | Notes |
|--------|-------------|----------|-----------------|-------|
| `KoboldApiClient.cs` | — | `/api/v1/generate` | No | Local KoboldCpp server |
| `OllamaApiClient.cs` | — | `/api/generate` | No | Local Ollama server |
| `LMStudioApiClient.cs` | — | `/v1/chat/completions` | No | Local LM Studio server |
| `OpenAiApiClient.cs` | Bearer token | `/v1/chat/completions` | No | OpenAI-compatible API |
| `ClaudeApiClient.cs` | `x-api-key` + `anthropic-version` | `/v1/messages` | Yes (budget configurable) | Anthropic message-based format |
| `DeepseekApiClient.cs` | Bearer token | `/chat/completions` | Yes | OpenAI-compatible + thinking toggle |
| `GeminiApiClient.cs` | `x-goog-api-key` | `/v1beta/models/{model}:generateContent` | Yes (budget + level) | Parts-based content; max 5 stop sequences |
| `GrokApiClient.cs` | Bearer token | `/v1/chat/completions` | No | xAI; simplest implementation |
| `Player2ApiClient.cs` | Bearer + `player2-game-key` | `/v1/chat/completions` | No | Health heartbeat (60s); JSON sanitization; MinP support |
| `QwenApiClient.cs` | Bearer token | `/api/v1/services/aigc/text-generation/generate` | No | Alibaba Qwen |

### Speech/ — Speech Bubbles, TTS, and Voice Management

#### `SpeechBubbleManager.cs`
- **GameComponent** managing the display and queuing of speech bubbles.
- **Queuing System**: Ensures sequential display of multi-line LLM dialogue.
- **Spam/Busy Management**: Prevents new LLM interactions from firing while one is already in progress, falling back to default bubbles.
- **Threading**: Uses locks to safely manage shared queues (`speechBubbleQueue`, `pendingJobs`) across asynchronous LLM calls and the main game thread.
- **Display Methods**: `Enqueue` (for sequential), `EnqueueInstant` (for immediate, e.g., taunts), `ShowDefaultBubble` (for non-LLM summaries).
- **Conversation Management**: Tracks conversation IDs and active conversations to prevent overlapping dialogues.
- **Chat Log Integration**: Integrates with `ChatLogManager` to store all interactions for later review.
- **Efficiency System**: Implements scheduled unlock timing to optimize LLM request handling with `ScheduleUnlock` method.
- **Animal Support**: Includes fallback logic for non-humanlike targets (animals, mechs) to bypass UI windows and use default bubble-only mode.

#### `TTSManager.cs`
- **Sequential Playback**: Implements a `Sequence ID` system (`nextRequestId`, `nextPlaybackId`) and a `playbackBuffer` to guarantee audio plays in the correct order regardless of download speed.
- **Pause-Resilient Logic**: Uses `Time.unscaledDeltaTime` and `audioSource.ignoreListenerPause = true` to allow audio playback while the game is paused (e.g., during negotiation).
- **Mute Logic**: `Stop()` now clears all queues, buffers, and fast-forwards the sequence ID for instant, persistent silence.
- **Network Staggering**: Staggers API requests by 500ms in `NegotiationManager` to prevent server-side batching/LIFO processing.
- **External API Only**: Relies exclusively on OpenAI-compatible APIs (e.g., local Kokoro servers) for TTS generation.

#### `VoiceAssignmentManager.cs`
- **GameComponent** responsible for persistent voice allocation.
- Maintains `Dictionary<Pawn, string>` mapping pawns to specific voice names.
- Uses `Scribe_Collections` (with auxiliary lists) to save assignments in the save file.
- Automatically fetches voices from the API on game load and assigns them based on gender ("af_" for female, "am_" for male).

#### `PauseableMote.cs`
- Custom mote thing that supports pausing and custom visual effects for speech/interaction display.

### Negotiation/ — Raid Negotiation System

A complete negotiation pipeline: detection → dialogue → outcome application → peaceful phase → optional recruitment.

#### `NegotiationManager.cs`
- **Coordinator**: Orchestrates the multi-turn interactive negotiation process.
- **Narrative Context**: Generates detailed, paragraph-style pawn descriptions matching the standard dialogue template.
- **Skill Bias**: Explicitly injects the Social skill level into the LLM prompt to influence the pawn's eloquence and success rate.
- **Outcome Engine**:
    - Parsed categories: `POSITIVE`, `NEUTRAL`, `NEGATIVE`.
    - **Deferred Application**: Supports "Push Your Luck" mechanics. Successes are stored as `pendingOutcome` and only applied when the window is closed manually.
    - **Risk Override**: A subsequent `NEGATIVE` outcome immediately overrides any pending success, forces a failure penalty, and closes the window.
- **Batch Processing**: Enqueues TTS requests with artificial staggering (500ms) to ensure chronological processing by external servers.

#### `Dialog_PawnNegotiation.cs`
- **Interaction Window**: Styled after the vanilla Comms/Negotiation dialog.
- **Interactive UI**: Displays LLM-generated choices as clickable buttons and supports custom text input.
- **Live History**: Real-time display of dialogue history with rich-text support for color-coded status messages (Green=Success, Red=Failure, Yellow=Neutral).
- **Pause Resilience**: Configured as a `forcePause` window that respects real-time updates for TTS playback.

#### `LordJob_NegotiatedRaid.cs`
- **State Machine**: Orchestrates negotiated raid behavior with phases: travel → linger → plunder → exit.
- **Outcome Types**: Critical Success, Positive, Neutral, Failure — each determining raid behavior post-negotiation.
- **Peace Enforcement**: `Notify_RaiderHarmed()` detects colonist attacks on raiders and breaks the peace deal.
- **Smart Gathering**: `GetSmartLingerSpot()` finds suitable areas (tables, beds, party spots) for raiders to loiter.
- **Supporting Classes**: `LordToil_SafeTravel`, `LordToil_DoAssault`, `RaidOutcomeUtility`.

#### `LordToil_Plunder.cs`
- **Persistent Stealing Toil**: Extends `LordToil_StealCover` with an extended 60-cell search radius (vs. vanilla's 7).
- **Non-Aggressive**: Raiders search for and steal valuables without reverting to combat.

#### `NegotiationCooldown_GameComponent.cs`
- **GameComponent** persisting negotiation cooldowns for both pawns and factions.
- **Spam Prevention**: Enforces temporal delays between negotiations.
- **Methods**: `SetCooldown()`, `IsOnCooldown()`, `GetHoursRemaining()`.

#### `RaidNegotiation_Patches.cs`
- **Utility and Patches**: `RaidNegotiationUtility` identifies negotiable raids, finds raid leaders, and validates negotiation conditions.
- **Hostility Patch**: Makes pawns with `SI_Negotiating` hediff immune to raider hostility checks.
- **Job Patch**: Detects negotiation initiation from `JobDriver_HaveChatWith` and applies protection hediff.

#### `RaidLooting_Patches.cs`
- **Combat Suppression**: Suppresses "dangerous combat" checks for plundering raiders so they prioritize stealing.
- **Extended Loot Search**: Custom loot finder using `TraverseMode.PassDoors` and larger search radius.

#### `SI_JoinRequestLetter.cs`
- **Choice Letter**: Appears when a raider wants to join the colony after successful negotiation.
- **Accept/Reject**: Player can accept (pawn switches faction) or reject the request.

### Dating/ — Dating, Cheating, and Prisoner Pestering

#### `DatingManager.cs`
- **Static class** managing the high-level state of ongoing dates.
- **Date Tracking**: Maintains a list of active `Date` objects (initiator, partner, stage: `Joy`, `Lovin`, `Finished`).
- **Lifecycle Management**: `StartDate`, `EndDate`, `RejectDate`, `AdvanceDateStage`.
- **State Checks**: `IsOnDate`, `IsOnDateCooldown`, `GetPartnerOfDateWith`, `GetInitiatorOfDateWith`.
- **Core Date Logic**: `TransitionToLovin`, `CalculateDateCompatibility`, `CalculateSexualCompatibility`, `FindSuitableBedForLovin`.
- **3p Actions**: Support for threeway actions with special handling for spouse involvement.
- **Persistence**: `ExposeData` for saving/loading date state.
- **Maintenance**: `CleanupExpiredDateCooldowns`, `CheckForStuckDates`.

#### `DateTracker_MapComponent.cs`
- **MapComponent** acting as the primary engine for progressing dates.
- **Core Functionality**: Lifecycle monitoring, stage advancement logic, partner activity management, joy activity coordination.

#### `Dating_MapComponent.cs`
- **MapComponent** cleaning up orphaned `SI_Naked` hediffs and managing 3p action scenarios.
- **Grace Period**: Provides a grace period for pawns to transition into the correct job before removing the hediff.

#### Date Job Drivers

| Job Driver | Purpose |
|-----------|---------|
| `JobDriver_GoOnDate.cs` | Initiates dating sequence; rolls for acceptance based on opinion/mood |
| `JobDriver_DateLovin.cs` | "Lovin" stage with bouncing animation, hediff management, pregnancy handling |
| `JobDriver_FollowAndWatch.cs` | Partner follows initiator during joy stage with continuous path updates |
| `JobDriver_SocialRelaxDate.cs` | Relaxation activities with comfort/joy mechanics and wandering behavior |
| `JobDriver_CaughtCheating.cs` | Handles the caught-cheating confrontation sequence |
| `JobDriver_AbusiveThreesome.cs` | Initiator driver for 3p action scenarios |
| `JobDriver_AbusiveThreesomeParticipant.cs` | Participant driver for 3p action scenarios |
| `JobDriver_PesterPrisoner.cs` | Main driver for pestering prisoners/slaves; insult scheduling, suppression mechanics |
| `JobDriver_PesterPrisonerPartner.cs` | Partner variant that follows and participates in pestering |

#### Joy Givers

| Joy Giver | Purpose |
|-----------|---------|
| `JoyGiver_GoOnDate.cs` | Creates initial dating jobs |
| `JoyGiver_FollowAndWatch.cs` | Allows pawns to join multi-participant joy activities |
| `JoyGiver_PesterPrisoner.cs` | Selects prisoners/slaves as pestering targets based on traits/genes |

#### Cheating Thoughts

| Thought | Purpose |
|---------|---------|
| `Thought_CaughtCheating.cs` | "Caught [pawn] cheating" — for the witness |
| `Thought_GotCaughtCheating.cs` | "Got caught cheating by [pawn]" — for the cheater |
| `Thought_WasCheatedOn.cs` | "Was cheated on by [pawn]" — for the betrayed partner |

### Interactions/ — Custom InteractionWorkers and PlayLogEntries

#### Interaction Workers

| Worker | Purpose |
|--------|---------|
| `InteractionWorker_Badmouthing.cs` | Target selection via `GetLeastFavoritePawn`, opinion-based outcomes, gossip bonding, backstabbing triggers |
| `InteractionWorker_EnhancedInsult.cs` | Severity-based insults (Mild/Moderate/Severe/Violent), social fight escalation |
| `InteractionWorker_Admiration.cs` | Social hierarchy recognition, trait/skill matching, multiple admiration types |
| `InteractionWorker_Backstabbing.cs` | Strategic betrayal with social skill-based success, catastrophic opinion reversal mechanics |
| `InteractionWorker_MakeUp.cs` | Reconciliation mechanics with thought removal and opinion changes |
| `InteractionWorker_LoversQuarrel.cs` | Romantic quarrels with three outcomes (reconciliation, neutral, near-breakup); 10% breakup chance on severe outcomes |
| `InteractionWorker_CaughtCheating.cs` | Triggers specific LLM interactions for cheating confrontations |
| `InteractionWorker_DateLovin.cs` | Triggers LLM interactions for date lovin' events |
| `InteractionWorkers.cs` | Legacy/utility interaction worker definitions |

#### Play Log Entries

| Log Entry | Purpose |
|-----------|---------|
| `PlayLogEntry_Badmouthing.cs` | Includes target pawn info with perspective-based formatting |
| `PlayLogEntry_EnhancedInsult.cs` | Includes severity level and fight escalation info |
| `PlayLogEntry_Admiration.cs` | Includes admiration type (GeneralPraise, SharedInterestPraise, SkillBasedAdmiration, InspirationalPraise) |
| `PlayLogEntry_Backstabbing.cs` | Includes target and success/failure info for strategic betrayal |
| `PlayLogEntry_MakeUp.cs` | Includes success/failure tracking for reconciliation attempts |

### Children/ — Child Misbehavior System

#### `ChildrenMisbehaviorManager.cs`
- **Core Logic Manager**: Misbehavior calculations, level selection, and behavior execution.
- **Misbehavior Factor Calculation**: Based on parental opinion, child's mood, and character traits.
- **Four Levels of Misbehavior**:
  - Level 1 — Annoying Adults: Approaches adults during work with annoying questions.
  - Level 2 — Item Play / Tag / Spying: Takes valuable items, plays tag with other children, spies on intimate activity.
  - Level 3 — Property Damage: Tramples crops, breaks buildings with `CompBreakdownable`.
  - Level 4 — Dangerous Behavior: Weapon play (20% accidental discharge), fire lighting, radio leaking (can trigger raids).
- **Trait Integration**: Rebellious (+20%), Kind (-20%), Psychopath (+30%).

#### `ChildrenMisbehaviorTracker_MapComponent.cs`
- **MapComponent** that periodically checks for misbehavior opportunities (every 600 ticks).

#### Child Job Drivers

| Job Driver | Level | Description |
|-----------|-------|-------------|
| `JobDriver_ChildAnnoyAdult.cs` | 1 | Approaches adults at work with annoying questions |
| `JobDriver_ChildPlayWithItem.cs` | 2 | Takes and potentially damages items |
| `JobDriver_InviteToPlayTag.cs` | 2 | Invitation process for tag gameplay |
| `JobDriver_PlayTagRunner.cs` | 2 | "It" child who runs to random locations |
| `JobDriver_PlayTagChaser.cs` | 2 | Chasing child who follows the runner |
| `JobDriver_ChildSpyOnLovin.cs` | 2 | Sneaks to watch intimate activity; may disrupt |
| `JobDriver_ChildTrampleCrops.cs` | 3 | Destroys crops in growing zones |
| `JobDriver_ChildBreakBuilding.cs` | 3 | Attacks breakdownable buildings |
| `JobDriver_ChildPlayWithWeapon.cs` | 4 | Weapon play with 20% accidental discharge chance |
| `JobDriver_ChildLightFire.cs` | 4 | Lights fires on flammable objects |
| `JobDriver_ChildPlayWithRadio.cs` | 4 | Plays with comms console; skill-based raid trigger chance |
| `JobDriver_ChildGoCryToParent.cs` | — | Comfort-seeking after fleeing; social skill-based comfort |

#### `MentalState_ChildFleeInTerror.cs`
- Custom mental state for children who flee after taking damage.
- Dynamic flee chance decreasing with combined shooting/melee skills.

#### `ChildThoughtDefOf.cs`
- DefOf references for child-specific thoughts (ChildCrying, etc.).

### Jobs/ — General-Purpose Job Drivers

| File | Purpose |
|------|---------|
| `JobDriver_HaveChatWith.cs` | Primary social connection; opens `Dialog_PawnNegotiation` (interactive) or plays 30s interaction with speech bubbles (non-interactive); manual skill check fallback |
| `JobDriver_HaveDeepTalk.cs` | Custom driver for deep talk jobs initiated by pawns |
| `Job_HaveDeepTalk.cs` | Job definition helper for deep talk |
| `JobDriver_BeTalkedTo.cs` | Recipient-side driver for being talked to |
| `JobDriver_BackstabbingApproachTarget.cs` | Physical approach + backstabbing interaction execution |
| `JobDriver_BackstabbingGatherInfo.cs` | Information gathering phase before backstabbing attempt |
| `JoyGiver_HaveDeepTalk.cs` | Creates initial deep talk jobs |

### Components/ — GameComponents and Utilities

#### `PawnFlavorText_GameComponent.cs`
- **GameComponent** for saving/loading custom pawn bio text.
- Uses `Scribe_Collections.Look` for persistence across maps and game restarts.
- `SyncWithStaticDictionary` for data synchronization with `SocialInteractions` static dictionary.

#### `SocialInfluenceUtility.cs`
- **Static utility class** calculating social metrics.
- **Influence Score**: Opinion average × social skill (normalized 0-1).
- **Integration Score**: Positivity of candidate through initiator's social connections (normalized 0-1).

### UI/ — User Interface

#### `ChatLogManager.cs`
- **Static class** managing storage and retrieval of all chat messages.
- **ChatMessage Class**: Speaker, recipient, timestamp, type, formatting.
- **Message Types**: LLMChat, GameEvent, DateEvent, CombatEvent for filtering.

#### `ChatLogTabWindow.cs`
- **Main tab window** for displaying chat logs with conversation grouping, search functionality, and message caching.
- Dual-panel layout: conversation list on left, message details on right.

#### `ChatLogWindow.cs`
- **Deprecated** window class kept for compatibility. Closes immediately; chat logs now integrated into the history tab.

#### `Dialog_EditPawnFlavorText.cs`
- **Window** for editing custom pawn bio text with multi-line input and Save/Cancel/Clear buttons.

#### `MainButtonWorker_ToggleTTS.cs`
- Toggle button on main tab bar to mute/unmute TTS instantly.

#### `PawnSelectionDialog.cs`
- Dialog for selecting colonists to assign voices to, with scrollable pawn list and current voice display.

#### `VoiceSelectionDialog.cs`
- Dialog for choosing specific voice assignments for a selected pawn from available voices.

### DefOfs/ — Static Definition References

| File | Definitions |
|------|-------------|
| `SI_JobDefOf.cs` | All custom job definitions (dating, children, backstabbing, chat, etc.) |
| `SI_InteractionDefOf.cs` | Custom interaction definitions (badmouthing, dating, admiration, etc.) |
| `SI_HediffDefOf.cs` | OnDate, SI_Naked, SI_Negotiating, Abused hediffs |
| `SI_ThoughtDefOf.cs` | Custom thought definitions |
| `SI_MentalStateDefOf.cs` | ChildFleeInTerror mental state |
| `SI_ThingDefOf.cs` | PauseableMote thing definition |
| `CustomTraitDefOf.cs` | Masochist trait reference |
| `ChildThoughtDefOf.cs` | Child-specific thought references (in Children/) |

### Combat/ — Combat Taunts

#### `CombatPatches.cs`
- Patches various combat methods (`CheckMeleeAttackAt`, `TakeDamage`, etc.) to trigger combat taunts and complaints via `SpeechBubbleManager.EnqueueInstant`.
- Visual differentiation from regular dialogue.

## Harmony Patches

### Interaction & Thought Patches
| Patch | Target | Purpose |
|-------|--------|---------|
| `InteractionWorker_Interacted_Patch.cs` | `InteractionWorker.Interacted` | Routes relevant interactions to `SocialInteractions.HandleInteraction` |
| `DramaInteractionPatches.cs` | `Pawn_InteractionsTracker.TryInteractWith` | Triggers drama interactions (badmouthing, insults, admiration, make-up) |
| `InteractionWorker_Breakup_Patch.cs` | Breakup interaction | Adds LLM dialogue to breakup events |
| `InteractionWorker_ConvertIdeoAttempt_Patch.cs` | Ideology conversion | Triggers LLM interactions on conversion attempts |
| `ThoughtHandler_OpinionOffsetOfGroup_Patch.cs` | `ThoughtHandler.OpinionOffsetOfGroup` | Applies opinion modifiers from `Thought_CaughtCheating` |
| `MarriageCeremonyStart_Patch.cs` | Marriage ceremony transition | Triggers LLM dialogue when ceremony begins |

### Job & Joy Patches
| Patch | Target | Purpose |
|-------|--------|---------|
| `JobPatches.cs` | `JobDriver.TryMakePreToilReservations` | Allows same-item reservation for social interactions |
| `JobDriver_Joy_Patch.cs` | `JobDriver_Joy.MakeNewToils` | Allows date partners to join same joy activity |
| `JoyTickCheckEnd_Patch.cs` | Joy tick end check | Prevents joy termination for dating/pester activities |
| `JobDriver_GiveSpeech_Patch.cs` | `JobDriver_GiveSpeech` | Generates monologues for speeches, detects execution rituals |
| `JobDriver_ModifyCarriedThingDrawPos_Patch.cs` | Carried thing draw position | Bouncing/spinning animation for child play items |
| `Debug_JobTracker_Patch.cs` | Job change tracking | Logs unexpected job assignments during dates |

### Pawn Lifecycle & Event Patches
| Patch | Target | Purpose |
|-------|--------|---------|
| `Pawn_Tick_Patch.cs` | `Pawn.Tick` | Triggers scheduled fight after cheating interaction |
| `Map_FinalizeInit_Patch.cs` | `Map.FinalizeInit` | Initializes custom map components |
| `MindStateTick_Patch.cs` | `Pawn_MindState.MindStateTick` | Handles interrupting pawns for dating |
| `Pawn_DraftController_Drafted_Patch.cs` | `Pawn_DraftController.set_Drafted` | Interrupts date jobs when drafted |
| `Game_FlavorTextComponent_Patch.cs` | `Game.InitNewGame` / `Game.LoadGame` | Initializes `PawnFlavorText_GameComponent` and TTSManager |
| `Pawn_TakeDamage_Patch.cs` | `ThingWithComps.PreApplyDamage` | Triggers child flee-in-terror mental state |
| `Pawn_GetGizmos_Patch.cs` | Pawn gizmos | Adds "Negotiate" button to colonist action bar |
| `TaleRecorder_Patch.cs` | Birth events | Triggers LLM interactions between doctor and mother |

### Monologue Trigger Patches
| Patch | Target | Purpose |
|-------|--------|---------|
| `Faction_Patch.cs` | Faction leader selection | Monologue when new faction leader chosen |
| `HistoryEventsManager_Patch.cs` | "Bonded" history event | Monologue on animal bonding (10s cooldown) |
| `InspirationHandler_TryStartInspiration_Patch.cs` | `InspirationHandler.TryStartInspiration` | Monologue when pawn receives inspiration |
| `MentalState_Patch.cs` | Mental state entry | Monologue when entering mental states (excl. social fighting) |
| `Precept_RoleMulti_Patch.cs` | Multi-slot role assignment | Monologue on role assignment (e.g., priest) |
| `Precept_RoleSingle_Patch.cs` | Single-slot role assignment | Monologue on individual role promotions |
| `QualityUtility_SendCraftNotification_Patch.cs` | Craft notification | Monologue on masterwork/legendary crafting |
| `LordJob_Joinable_Party_CreateGraph_Patch.cs` | Party/concert start | Monologue for party organizers |
| `LordJob_PsychicRitual_CreateGraph_Patch.cs` | Psychic ritual start | Monologue for ritual invokers |

### Rendering Patches
| Patch | Target | Purpose |
|-------|--------|---------|
| `PawnRenderer_GetDrawParms_Patch.cs` | Pawn rendering | Visual offsets for date lovin' |
| `PawnRenderer_RenderPawnAt_Patch.cs` | Pawn rendering | Visual offsets for date lovin' |

### Raid Negotiation Patches
| Patch | Target | Purpose |
|-------|--------|---------|
| `RaidNegotiation_Patches.cs` | Hostility checks, job detection | Protects negotiators, detects negotiation start |
| `RaidLooting_Patches.cs` | Combat AI, stealing | Suppresses combat for plunderers, extends loot search |

### Character & UI Patches
| Patch | Target | Purpose |
|-------|--------|---------|
| `CharacterCardUtility_AddFlavorTextButton_Patch.cs` | Character card | Adds "Bio" button for accessing bio editor |

## XML Definitions (`1.5/Defs/`)

### Interaction Definitions
- `InteractionDefs.xml` — Base interaction definitions
- `InteractionDefs_Badmouthing.xml` — Badmouthing, enhanced insult, and backstabbing interactions
- `InteractionDefs_Children.xml` — Child play tag and misbehavior interactions
- `InteractionDefs_Dating.xml` — Dating-related interactions
- `InteractionDefs_DatingOutcome.xml` — Date outcome interactions
- `InteractionDefs_JobGivers.xml` — Job-giver interaction definitions
- `InteractionDefs_ManualChat.xml` — Manual chat interaction

### Job Definitions
- `JobDefs.xml` — Core job definitions
- `JobDefs/Jobs_HaveChatWith.xml` — Chat job definitions
- `JobDefs/Jobs_Misc.xml` — Miscellaneous jobs
- `JobDefs/JobDef_Backstabbing.xml` — Backstabbing job definitions
- `JobDefs/JobDefs_Children_BreakBuilding.xml` — Building breaking jobs
- `JobDefs/JobDefs_Children_Radio.xml` — Radio play jobs
- `JobDefs/JobDefs_Children_Tag.xml` — Tag play jobs
- `JobDefs_AbusiveThreesome.xml` — 3p action jobs
- `JobDefs_Children.xml` — General child jobs
- `JobDefs_Dating.xml` — Dating jobs
- `JobDefs_PesterPrisoner.xml` — Prisoner pestering jobs
- `JobDefs_Recipient.xml` — Recipient-side jobs

### Other Definitions
- `HediffDefs_Dating.xml`, `HediffDefs_Negotiation.xml`, `HediffDefs_PesterPrisoner.xml` — Custom hediffs
- `JoyGiverDefs.xml`, `JoyGiverDefs_Dating.xml`, `JoyGiverDefs_PesterPrisoner.xml` — Joy activities
- `JoyKindDefs.xml`, `JoyKindDefs_Dating.xml`, `JoyKindDefs_PesterPrisoner.xml` — Joy categories
- `ThoughtDefs_ChildComfort.xml`, `ThoughtDefs_Children.xml` — Child thoughts
- `ThoughtDefs_Dating.xml`, `ThoughtDefs_LoversQuarrel.xml`, `ThoughtDefs_PesterPrisoner.xml` — Social thoughts
- `MentalStateDefs_Children.xml` — Child mental states
- `LetterDefs.xml` — Custom letter types
- `RulePacks/RulePacks_Drama.xml`, `RulePacks/RulePacks_MakeUp.xml` — Log rule packs
- `MainTabDefs/MainTabDefs_ChatLog.xml` — Chat log tab
- `ThingDefs/PauseableMote.xml` — Custom mote definition

## Localization

- **Framework**: RimWorld's standard keyed translation system.
- **Languages**: English (`Languages/English/Keyed/Keyed.xml`), Chinese Simplified (`Languages/ChineseSimplified/Keyed/Keyed.xml`).
- **Coverage**: All mod settings, UI elements, dialog text, and descriptions.

## Data Flow Examples

### Starting a Date
1.  `JoyGiver_GoOnDate` gives a `JobDriver_GoOnDate` job to an initiator.
2.  `JobDriver_GoOnDate` moves the initiator to a potential partner and rolls for acceptance based on opinion and mood.
3.  If accepted:
    -   `DatingManager.StartDate` is called, creating the `Date` object and applying the `OnDate` hediff.
    -   `SocialInteractions.HandleNonStoppingInteraction` is called for the date acceptance dialogue.
    -   The initiator finds a joy activity (e.g., watching TV).
    -   The partner is given a `JobDriver_FollowAndWatch` job.
4.  `DateTracker_MapComponent` now monitors the date. It ensures the partner keeps following the initiator and attempts to have the partner join joy activities.
5.  When the initiator's joy need is satisfied or they move to a non-joy job, `DateTracker_MapComponent` calls `DatingManager.AdvanceDateStage`.
6.  `DatingManager` transitions the state to `DateStage.Lovin` and calls `TransitionToLovin`.
7.  `TransitionToLovin` finds a suitable location (bed or random spot) and starts `JobDriver_DateLovin` for both pawns.
8.  `JobDriver_DateLovin` runs, applying the `SI_Naked` hediff, showing the animation, and providing joy/thoughts. When it completes, it calls `DatingManager.AdvanceDateStage`.
9.  `DatingManager` transitions the state to `DateStage.Finished` and calls `EndDate`.
10. `DatingManager.EndDate` cleans up hediffs, ends any remaining jobs, and puts the pawns on a date cooldown.

### Monologue Trigger
1. A pawn experiences a specific event (e.g., becomes a leader, enters a mental state, crafts a masterwork).
2. The relevant Harmony patch calls `SocialInteractions.HandleMonologue` with the pawn and a subject describing the event.
3. `HandleMonologue` checks LLM availability, generates a prompt using `GenerateMonologuePrompt`, sends it to the LLM, and queues response lines as speech bubbles.

### Raid Negotiation
1. A raid spawns. `RaidNegotiationUtility.GetNegotiableRaids()` identifies eligible raids (humanlike faction, not yet in combat, assault-type lord job).
2. Player selects a colonist and clicks the "Negotiate" gizmo (added by `Pawn_GetGizmos_Patch`).
3. `JobDriver_HaveChatWith` detects the negotiation target and applies `SI_Negotiating` hediff for protection.
4. `Dialog_PawnNegotiation` opens. Player interacts via LLM-generated choices or typed input.
5. `NegotiationManager` processes each turn, determines outcomes (`POSITIVE`, `NEUTRAL`, `NEGATIVE`).
6. On window close, outcome is applied: `LordJob_NegotiatedRaid` replaces the assault lord job.
7. Raiders enter peaceful phases (linger → plunder via `LordToil_Plunder` → exit).
8. On critical success, `SI_JoinRequestLetter` may offer a raider as a colonist recruit.
9. `NegotiationCooldown_GameComponent` enforces cooldowns on the pawn and faction.
