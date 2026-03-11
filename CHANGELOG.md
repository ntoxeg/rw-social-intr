# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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