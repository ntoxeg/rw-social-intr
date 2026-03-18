# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0] - 2026-03-17

### Added
- **Pawn Memory System** — colony pawns now accumulate persistent memories that evolve over time
  - Daily LLM-powered memory writing: events buffer throughout the day, then a single LLM call per pawn distills them into a cohesive memory entry
  - `[pawn1_memories]` / `[pawn2_memories]` placeholders available in all prompt templates, enabling pawns to reference their history in conversations
  - Hybrid compaction: FIFO truncation safety net + LLM-based summarization when memory exceeds threshold, preventing unbounded growth
  - Comprehensive event capture across 24+ hook points:
    - LLM dialogue and monologues (both participants)
    - Drama interactions: badmouthing, backstabbing, admiration, reconciliation, insults, lover's quarrels
    - Life events: marriage, birth, mental breaks, inspiration, masterwork crafting, leadership, animal bonding, role assignment
    - Combat: kills and downings (selective — not every hit)
    - Social: caught cheating, dates
  - Memory tab in Bio editor for viewing, editing, and clearing memories
  - Full settings UI: enable/disable toggle, character limit slider (500–5000), compaction threshold, buffer cap, customizable prompt templates with reset button
  - Colony-only filtering — non-colony pawns (raiders, visitors, animals) are excluded
  - Thread-safe buffer with 50 entry cap per pawn per day
  - Background async processing — non-blocking, one pawn at a time
  - 70 new unit tests covering buffer management, compaction, storage, and thread safety

### Changed
- Default max tokens increased from 1024 to 4096 for better reasoning model support
- Default max dialogue lines increased from 6 to 10
- Max tokens UI slider range expanded to 16384
- Memory settings moved to dedicated `MemorySettings` class (from `FeatureToggles`)

### Fixed
- Claude API thinking budget calculation — `budget_tokens` must be < `max_tokens` per API spec
- Deepseek API thinking budget calculation — same constraint enforcement
- Removed obsolete `compile.rsp` reference from architecture documentation

## [2.0.3] - 2026-03-16

### Changed
- Conversation interruptions are now significantly shorter — recipients (BeTalkedTo) are released after 10 seconds (600 ticks) instead of waiting for the entire conversation to finish
- Conversation fail-safe timeout reduced from 30s to 15s
- Default max dialogue lines reduced from 10 to 6 for new games

### Fixed
- `JobDriver_HaveDeepTalk` now enforces the `llmMaxDialogueLines` limit (was missing, unlike all other handlers)

## [2.0.2] - 2026-03-15

### Changed
- `LlmClientConfig` POCO captures LLM sampling parameters at client construction time; API clients no longer read directly from the global settings singleton, making them unit-testable without a game runtime
- `SLog` now delegates to an injected `IModLogger` (null = silent no-op); `VerseLogger` is the production implementation, allowing tests to substitute a `TestLogger` or `NullLogger`
- `SpeechBubbleManager` implements `ISpeechService`; `ChatLogManager` implements `IChatLog`; both register themselves via the new `Services` static locator on construction
- `ChatMessage` / `MessageType` moved from `SocialInteractions.UI` to `SocialInteractions` (Core) to remove lateral coupling from Speech/Negotiation into the UI namespace
- Dating, Combat, Negotiation, and Interactions subsystems now reference `Services.Speech` / `Services.ChatLog` abstractions instead of directly importing sibling namespaces

### Added
- 49 runtime unit tests covering API client construction, `LlmClientConfig` defaults, `CleanChatResponse`, `IsValidHeaderValue`, and `SLog` null-safety (`SocialInteractions.Tests`)

## [2.0.1] - 2026-03-12

### Fixed
- Updated all 47 XML type references across 24 def files to use new sub-namespaces introduced in 2.0.0 (e.g. SocialInteractions.Jobs, SocialInteractions.Children, SocialInteractions.Dating, SocialInteractions.Interactions, SocialInteractions.UI, SocialInteractions.Speech, SocialInteractions.Negotiation)

## [2.0.0] - 2026-03-12

### Changed
- Reorganized all 129 C# source files into 14 logical subdirectories (Core, Api, Dating, Children, Negotiation, Interactions, Combat, Speech, UI, DefOfs, Jobs, Components, Patches)
- Introduced directory-aligned namespaces (SocialInteractions.Api, SocialInteractions.Dating, etc.)
- Extracted shared ILlmClient interface and LlmClientBase from 10 API clients, reducing per-client boilerplate by ~60%
- Added LlmClientFactory for centralized API client creation
- Standardized error handling across all API clients (graceful null returns on failure)
- Decomposed SocialInteractionsModSettings into nested classes (ApiSettings, FeatureToggles, PromptSettings, DisplaySettings, GameplaySettings)
- Converted DatingManager, TTSManager, ChildrenMisbehaviorManager, and ChatLogManager from static classes to GameComponents
- Cleaned up static mutable state in SpeechBubbleManager and VoiceAssignmentManager

### Removed
- Legacy compile.bat and compile.rsp build system (standardized on dotnet build)

### Added
- Unit test project (SocialInteractions.Tests) with 24 characterization tests for the API layer

## [1.6.0] - 2026-03-11

### Changed
- Redesigned auto-generate bio to produce a structured, multi-section character sheet
  - Dossier section: code-generated factual summary (name, traits, skills, health, family, etc.) shown as a live read-only panel in the dialog
  - Persona section: LLM-generated personality paragraph and quirks/values bullet points
- Bio generation prompt now feeds additional pawn data (mood, afflictions, likes/dislikes, implants) for richer personality output
- Robust response parser with fallback for local models that don't follow formatting instructions
- Enlarged bio dialog window with distinct dossier and persona areas
- Added .csproj build support (dotnet build) using NuGet reference assemblies

### Fixed
- Extra closing brace in InteractionWorker_ConvertIdeoAttempt_Patch.cs

## [1.5.8] - 2026-03-10

### Added
- Auto-generate bio feature for pawns using LLM
- Auto-Generate button in Dialog_EditPawnFlavorText with async handling
- Scrollable text area for longer bios
- New translation keys for bio generation UI states

### Changed
- Disable auto-generate button when LLM is not configured, with tooltip
- Preserve Unicode in Player2ApiClient by removing aggressive ASCII sanitization

## [1.5.7] - 2026-02-27

### Added
- Player2 LLM/TTS integration as new API backend
- Conversation timeout safety to prevent stuck LLM conversations

### Fixed
- Null safety in dating mood calculation
- Enhanced negotiation dialog UI

## [1.5.5] - 2026-02-10

### Added
- Repetition penalty parameter to all LLM API clients (Claude, Deepseek, Gemini, Grok, Kobold, LMStudio, Ollama, OpenAI, Qwen)
- showDefaultBubbles and showLlmBubbles settings to control dialogue bubble visibility
- Bad date outcome system with mood-based calculation and negative thoughts
- Lover's quarrel interaction between partners
- Pester Prisoner dating activity with follow-and-insult mechanics
- Abusive Threesome activity triggered after Pester Prisoner under certain conditions
- New job drivers: JobDriver_PesterPrisoner, JobDriver_PesterPrisonerPartner, JobDriver_AbusiveThreesome, JobDriver_AbusiveThreesomeParticipant

### Changed
- Improved child break building behavior with adult detection and flee mechanics
- Improved children misbehavior management
- Better stuck date detection for new activity types
- Improved date management with partner acceptance/refusal handling

## [1.5.0] - 2026-01-27

### Added
- Monologue events for masterwork/legendary crafting and inspiration
- JobDriver_SocialRelaxDate for enhanced dating interactions
- New interaction types for the social system
- Social fight negotiation support

### Changed
- Updated VRE (Vanilla Rimworld Expanded) flirt compatibility
- Enhanced dating system with expanded API client support
- Enhanced negotiation system with new interaction types

## [1.4.1] - 2026-01-16

### Fixed
- Conversation management: finish actions ensure conversations are properly ended in JobDriver_CaughtCheating and JobDriver_HaveDeepTalk
- Negotiation system properly ends conversations and clears LLM busy state
- Pawn title handling updated to use story titles when available
- Added GetNextConversationId method for background dialogue logging
- Clear active conversations in SpeechBubbleManager to reset LLM state

## [1.4.0] - 2026-01-13

### Added
- Interactive negotiation system (Dialog_PawnNegotiation, NegotiationManager)
- Raid negotiation and looting functionality
- Negotiation cooldowns and join requests
- HediffDefs for negotiation states

### Changed
- Enhanced TTS functionality alongside negotiation system

## [1.3.7] - 2026-01-12

### Added
- Faction prompt field ([pawn#_faction]) for LLM context
- Incapable skills prompt field ([pawn#_noskills]) for LLM context
- Comprehensive prompt field documentation in settings

## [1.3.6] - 2026-01-12

### Changed
- Refined interaction behaviors
- Improved version comparison using proper Version.TryParse

### Fixed
- Null pawn handling in various interactions
- Voice assignment persistence across saves

## [1.3.0] - 2025-12-17

### Added
- Monologue feature for mental states with LLM support
- TTS playback queue to prevent overlapping audio
- Prompt field system ([pawn#_journal], etc.) for enhanced LLM context

### Fixed
- TTS voice parsing for filenames with extensions
- TTS voice selection UI issues
- Voice assignment algorithm improvements
- Strip pawn name from monologue TTS output

## [1.2.2] - 2025-12-05

### Added
- Text-to-Speech (TTS) feature with voice assignment
- TTS UI settings and configuration
- KindWords interaction integrated into existing system

### Changed
- Enhanced LLM API integration with response cleaning
- API-specific prompt formatting (removed hardcoded `<start>` tags)
- Improved version tracking with CURRENT_VERSION constant

### Fixed
- Ideology conversion attempts causing errors
- Backstabbing initiation chance balancing
- Chat log performance optimization
- Faction patch error

## [1.2.0] - 2025-11-16

### Added
- Comprehensive child misbehavior system with LLM support
  - Fire lighting and crop trampling behaviors
  - Child spying on couples
  - Play tag system between children
  - Building breaking and radio leaking
  - Item play animations
  - Weapon play with expanded logic and search radius
  - Mental state based flee in terror
  - Comfort mechanics for scared children
  - Adult detection and child flee mechanics
- LLM functionality for breakup interactions

### Changed
- Improved dating compatibility logic
- Enhanced spam protection system
- Updated architecture documentation for marriage ceremony integration

## [1.1.1] - 2025-11-10

### Added
- Version tracking system to settings for better compatibility management

## [1.1.0] - 2025-11-10

### Added
- MakeUp/Apologizing interaction to the drama system
- Trait-based reconciliation mechanics (kind pawns more likely to initiate)
- Integration with core social mechanics (thought removal, opinion changes)
- Present-tense prompt generation for better LLM responses
- Comprehensive settings and configuration options
- Translation support for English and Chinese
- RulePackDef files for reconciliation outcomes
- Meaningful log entries for reconciliation attempts

### Changed
- Enhanced InteractionDef for MakeUp to provide meaningful log entries
- Improved LLM integration that preserves core mechanics when disabled

## [1.0.2] - 2025-10-19

### Added
- Added Claude API support (Anthropic)
- Added Grok API support (xAI)
- Added Deepseek API support
- Added Qwen API support (Alibaba Cloud DashScope)
- Added Gemini API support (Google)
- Added model-specific settings for each new API
- Updated UI to include settings for all new APIs

### Changed
- Updated README with information about new API support
- Enhanced API client infrastructure to support multiple new services
- Improved documentation and code organization

## [1.0.1] - 2025-08-15

### Added
- Initial release with basic LLM integration
- Support for KoboldCpp, Ollama, LMStudio, and OpenAI APIs
- Social interaction features with AI-generated dialogue
- Dating system implementation
- Combat taunts and reactions
- Custom speech bubble system
- Chat log functionality

### Changed
- Initial mod structure and architecture

[2.1.0]: https://github.com/LuckyKo/rimworldmods/compare/v2.0.3...v2.1.0
[2.0.3]: https://github.com/LuckyKo/rimworldmods/compare/v2.0.2...v2.0.3
[2.0.2]: https://github.com/LuckyKo/rimworldmods/compare/v2.0.1...v2.0.2
[2.0.1]: https://github.com/LuckyKo/rimworldmods/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/LuckyKo/rimworldmods/compare/v1.5.8...v2.0.0
[1.5.8]: https://github.com/LuckyKo/rimworldmods/compare/v1.5.7...v1.5.8
[1.5.7]: https://github.com/LuckyKo/rimworldmods/compare/v1.5.5...v1.5.7
[1.5.5]: https://github.com/LuckyKo/rimworldmods/compare/v1.5.0...v1.5.5
[1.5.0]: https://github.com/LuckyKo/rimworldmods/compare/v1.4.1...v1.5.0
[1.4.1]: https://github.com/LuckyKo/rimworldmods/compare/v1.4.0...v1.4.1
[1.4.0]: https://github.com/LuckyKo/rimworldmods/compare/v1.3.7...v1.4.0
[1.3.7]: https://github.com/LuckyKo/rimworldmods/compare/v1.3.6...v1.3.7
[1.3.6]: https://github.com/LuckyKo/rimworldmods/compare/v1.3.0...v1.3.6
[1.3.0]: https://github.com/LuckyKo/rimworldmods/compare/v1.2.2...v1.3.0
[1.2.2]: https://github.com/LuckyKo/rimworldmods/compare/v1.2.0...v1.2.2
[1.2.0]: https://github.com/LuckyKo/rimworldmods/compare/v1.1.1...v1.2.0
[1.1.1]: https://github.com/LuckyKo/rimworldmods/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/LuckyKo/rimworldmods/compare/v1.0.2...v1.1.0
[1.0.2]: https://github.com/LuckyKo/rimworldmods/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/LuckyKo/rimworldmods/releases/tag/v1.0.1
