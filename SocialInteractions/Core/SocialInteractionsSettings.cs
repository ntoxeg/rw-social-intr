using Verse;
using UnityEngine;
using System.Collections.Generic; // New using directive
using System;
using RimWorld;

namespace SocialInteractions
{
    public enum LlmApiType
    {
        KoboldCpp,
        Ollama,
        LMStudio,
        OpenAI,
        Gemini,
        Qwen,
        Deepseek,
        Grok,
        Claude,
        Player2
    }

    public enum TtsApiType
    {
        OpenAI,
        Player2
    }



    public class SocialInteractionsModSettings : ModSettings
    {
        // Version tracking
        private const string CURRENT_VERSION = "1.5.8";
        public string modVersion = CURRENT_VERSION; // Current version of the mod

        // Default templates
        /*
         * Available Prompt Fields:
         * 
         * [pawn1], [pawn2]: Names of the pawns
         * [pawn#_sex]: Sex of the pawn (Male/Female)
         * [pawn#_age]: Biological age of the pawn
         * [pawn#_title]: Royal title or role (colonist, prisoner, slave, guest)
         * [pawn#_faction]: Faction name of the pawn
         * [pawn#_ideology]: Ideology name of the pawn
         * [pawn#_traits]: Comma-separated list of traits
         * [pawn#_genes]: Comma-separated list of active genes/xenotype
         * [pawn#_proficiencies]: Top 3 skills
         * [pawn#_noskills]: Skills the pawn is incapable of performing
         * [pawn#_mood]: Current mood description
         * [pawn#_likes]: Most positive thought
         * [pawn#_dislikes]: Most negative thought
         * [pawn#_afflictions]: Medical conditions/hediffs
         * [pawn#_family]: Family relationships
         * [pawn#_bio]: Backstory/bio
         * [pawn#_action]: Current job/activity
         * [pawn#_journal]: Recent log entry (when last spoke)
         * [pawn#_opinion]: Opinion of the conversation target
         * 
         * [relation]: Relationship between pawns (Spouse, Lover, etc.)
         * [tile]: Biome/terrain type of the map
         * [colony]: Description of the colony (population, etc.)
         * [event]: Recent significant event
         * [time]: Current in-game time (e.g., "08:00")
         * [date]: Current in-game date (e.g., "1st of Aprimay, 5500")
         * [weather]: Current weather label
         * [subject]: Interaction subject/topic
         * [topic]: Interaction topic (for monologue)
         */
        public const string DEFAULT_DIALOGUE_TEMPLATE = @"The following is an interaction between two RimWorld characters, [pawn1] and [pawn2]. Keep each dialogue line short with around 3-4 dialogue lines in total. It's a brutal world out there so feel free to use swearing, explicit or rough language freely where appropriate.

[pawn1] is a [pawn1_sex], age [pawn1_age], a [pawn1_title] of the [pawn1_faction] faction, following the [pawn1_ideology] ideology, has the following traits: [pawn1_traits]; Xenotype: [pawn1_genes]; [pawn1] is proficient in: [pawn1_proficiencies]; [pawn1] is incapable of: [pawn1_noskills]; [pawn1]'s mood is [pawn1_mood], positives: [pawn1_likes] / negatives: [pawn1_dislikes]; Medical status: [pawn1_afflictions]. [pawn1]'s family: [pawn1_family]. [pawn1_bio] 
[pawn1] is currently [pawn1_action]

[pawn2] is a [pawn2_sex], age [pawn2_age], a [pawn2_title] of the [pawn2_faction] faction, following the [pawn2_ideology] ideology, has the following traits: [pawn2_traits]; Xenotype: [pawn2_genes]; [pawn2] is proficient in: [pawn2_proficiencies]; [pawn2] is incapable of: [pawn2_noskills]; [pawn2]'s mood is [pawn2_mood], positives: [pawn2_likes] / negatives: [pawn2_dislikes]; Medical status: [pawn2_afflictions]. [pawn2]'s family: [pawn2_family]. [pawn2_bio]
[pawn2] is currently [pawn2_action]

[pawn2] is [pawn1]'s [relation].
[pawn1]'s opinion of [pawn2]: [pawn1_opinion]
[pawn2]'s opinion of [pawn1]: [pawn2_opinion]
Last time they spoke: [pawn1_journal]

The colony is in a [tile] area, has [colony], and [event]. 
It's currently [time], on [date] and the weather is [weather].

Current event: [subject]

";
        
        public const string DEFAULT_MONOLOGUE_TEMPLATE = @"The following is a [topic] by a RimWorld character, [pawn1]. It's a brutal world out there so feel free to use swearing, explicit or rough language freely where appropriate.

[pawn1] is a [pawn1_sex], age [pawn1_age], a [pawn1_title] of the [pawn1_faction] faction, following the [pawn1_ideology] ideology, has the following traits: [pawn1_traits]; Xenotype: [pawn1_genes]; [pawn1] is proficient in: [pawn1_proficiencies]; [pawn1] is incapable of: [pawn1_noskills]; [pawn1]'s mood is [pawn1_mood], positives: [pawn1_likes] / negatives: [pawn1_dislikes]; Medical status: [pawn1_afflictions]. [pawn1_bio]
[pawn1] is currently [pawn1_action]

The colony is in a [tile] area, has [colony], and [event].
It's currently [time], on [date] and the weather is [weather].

Current event: [pawn1] [subject]

";

        public string llmApiKey = "1234";
        public string llmPromptTemplate = DEFAULT_DIALOGUE_TEMPLATE;
        public string llmMonologuePromptTemplate = DEFAULT_MONOLOGUE_TEMPLATE;
        public bool llmInteractionsEnabled = false;
        public int wordsPerLineLimit = 10; // Default to 10 words per line
        public float wordsPerSecond = 3.0f; // Default to 5 words per second
        public int llmMaxDialogueLines = 10; // Default to 10 lines
        public float llmTemperature = 0.7f; // Default temperature
        public int llmMaxTokens = 1024; // Default max tokens
        public int llmTopK = 40; // Default Top K (0 = disabled)
        public float llmTopP = 1.0f; // Default Top P (1.0 = disabled)
        public float llmMinP = 0.05f; // Default Min P (0.0 = disabled)
        public float llmRepetitionPenalty = 1.0f; // Default Repetition Penalty (1.0 = disabled)
        public string ollamaModelName = "llama3.2"; // Default Ollama model name
        public string lmStudioModelName = "gemma-2-2b-it"; // Default LM Studio model name
        public string openAiModelName = "gpt-3.5-turbo"; // Default OpenAI model name
        public string geminiModelName = "gemini-flash-latest"; // Default Gemini model name
        public string qwenModelName = "qwen-max"; // Default Qwen model name
        public string deepseekModelName = "deepseek-chat"; // Default Deepseek model name
        public string grokModelName = "grok-3-mini"; // Default Grok model name
        public bool disableLlmThinking = true; // Whether to disable LLM thinking/reasoning
        public string claudeModelName = "claude-3-sonnet-20240229"; // Default Claude model name
        public string player2ModelName = "p2-intelligence-v1"; // Default Player2 model name
        public string player2GameClientId = "019c9077-e849-7348-9d91-593f91438fb3"; // Player2 Game Client ID for usage tracking
        public bool preventSpam = false;
        public bool forceChatCompletion = true; // New setting to force chat completion for local APIs

        // API settings
        public LlmApiType llmApiType = LlmApiType.KoboldCpp; // Default to KoboldCpp
        public string llmApiUrl = "http://localhost:5001";
        
        // Feature enablement settings
        public bool pawnsStopOnInteraction = true;
        public bool enableCombatTaunts = true;
        public bool enableDatingFeature = true;
        public bool enableXtcSampling = false;
        public bool enableDrama = false; // New setting for drama interactions like badmouthing
        
        public bool verboseLogging = false;
        public bool showDefaultBubbles = true; // Toggle for default interaction bubbles
        public bool showLlmBubbles = true; // Toggle for LLM dialogue bubbles
        public bool useBackgroundTextRendering = false; // False = drop shadow (current), True = background style
        
        // Interaction type settings
        public bool enableChitchat = true;
        public bool enableManualChat = true; // New setting for manual chat
        public bool enableInteractiveNegotiation = true; // Toggle for window vs simple bubbles
        public bool enableDeepTalk = true;
        public bool enableInsult = true;
        public bool enableRomanceAttempt = true;
        public bool enableMarriageProposal = true;
        public bool enableReassure = true;
        public bool enableDisturbingChat = true;
        public bool enableTendPatient = true;
        public bool enableRescue = true;
        public bool enableVisitSickPawn = true;
        public bool enableLovin = true;
        public bool enableDating = true;
        public bool enableMarriageCeremony = true; // Whether to enable LLM interactions during marriage ceremonies
        public bool enableBreakups = true; // Whether breakup interactions are enabled
        public bool useLlmForBreakups = true; // Whether to use LLM for breakup interactions
        public bool enableIdeologyConversionInteractions = true; // Whether ideology conversion interactions are enabled
        public bool enableKindWordsInteractions = true; // Whether kind words interactions are enabled
        public bool enableRaidNegotiation = true; // Whether negotiation with enemy raids is enabled
        
        // New interaction toggles
        public bool enableFlirt = true;
        public bool enableSlight = true;
        public bool enableIncestuousFlirt = true;
        public bool enableRapport = true;
        public bool enableRecruitAttempt = true;
        public bool enableReduceResistance = true;
        public bool enableReduceWill = true;
        public bool enableEnslaveAttempt = true;

        // Monologue event toggles
        public bool enableMasterworkMonologue = true; // Monologue when crafting masterwork/legendary
        public bool enableInspirationMonologue = true; // Monologue when receiving inspiration

        public float negotiationCooldownHours = 24.0f; // New setting for negotiation cooldown
        
        // String settings
        public string llmStoppingStrings = @"<end>
</end>
</start>
<start>
—END—
<END>
**end**
(end)";
        
        // Magic number settings (not exposed in UI)
        public float meleeTauntProbability = 0.35f;
        public float shootTauntProbability = 0.15f;
        public float gettingHitComplaintProbability = 0.3f;
        public float downedCallForHelpProbability = 0.85f;
        public int dateCooldownTicks = 5000;
        public int maxDistanceForDate = 50;
        public float joyThresholdForDate = 0.5f;
        public int jobCheckIntervalTicks = 60;
        public int initialToleranceTicks = 60;
        public int goOnDateCooldownTicks = 600;
        public int cheatingConfrontationTicks = 300;
        
        // Dating lovin' settings
        public float baseLovinChance = 0.95f;
        public int dateLovinTicks = 2500;
        public int dateLovinTimeoutTicks = 600; // 10 seconds
        public float maxDistanceToLovinSpot = 50f; // Maximum distance to accept a bed for lovin'
        
        // Dating partner selection weights/penalties
        public float spouseDateWeight = 100f;
        public float fianceDateWeight = 90f;
        public float loverDateWeight = 80f;
        public float opinionAdjustmentFactor = 50f; // For relationship partners, opinion adjustment range: -2 to +2
        public float nonRelatedPartnerWeightFactor = 0.7f; // General weight factor for non-related partners
        public float cheatingPenalty = 30f;
        public float opinionDifferenceThreshold = 20f; // Opinion difference needed to eliminate cheating penalty
        
        // Badmouthing interaction settings (for debugging/tweaking)
        public float baseBadmouthingChance = 0.05f; // Base chance for pawns without encouraging traits
        public float traitEncouragedBadmouthingChance = 0.25f; // Chance for pawns with encouraging traits
        public float badOpinionAdditionalChance = 0.15f; // Additional chance when pawn has low opinion of someone else
        
        // Enhanced Chitchat Insult settings (for debugging/tweaking)
        public float baseEnhancedChitchatInsultChance = 0.05f; // Base chance (5%)
        public float enhancedChitchatInsultMoodMultiplierBad = 1.5f; // Multiplier when mood is low (< 40%)
        public float enhancedChitchatInsultMoodMultiplierGood = 0.7f; // Multiplier when mood is high (> 80%)
        public float enhancedChitchatInsultOpinionMultiplierVeryNegative = 2.0f; // Multiplier when opinion is very negative (< -20)
        public float enhancedChitchatInsultOpinionMultiplierVeryPositive = 0.6f; // Multiplier when opinion is very positive (> 30)
        public float enhancedChitchatInsultTraitMultiplier = 1.8f; // Multiplier for pawns with encouraging traits
        public float enhancedChitchatInsultOpinionDifferenceMultiplier = 0.5f; // Multiplier scale for opinion differences
        
        // Badmouthing opinion adjustment settings
        public int badmouthingOpinionReductionForTarget = -5; // How much to reduce recipient's opinion of the target
        public int badmouthingOpinionReductionForInitiator = -8; // How much to reduce recipient's opinion of the initiator when it's inappropriate
        public int badmouthingLowOpinionThreshold = 0; // Threshold for considering an opinion "low"
        
        // Admiration interaction settings (for debugging/tweaking)
        public float baseAdmirationChance = 0.03f; // Base chance for admiration interactions
        public float admirationAttractionMultiplier = 2.0f; // Multiplier when initiator shares traits/skills with recipient
        public float admirationPositiveOpinionMultiplier = 1.5f; // Multiplier when opinion is positive
        
        // Admiration opinion impact settings
        public float admirationOpinionIncreaseOnSuccess = 3f; // Opinion increase when admiration successfully boosts standing
        public float admirationOpinionDecreaseOnFail = -1f; // Opinion change when admiration fails poorly
        public float admirationNegativeImpactChance = 0.1f; // Chance of slight negative impact when admiration fails

        // Backstabbing interaction settings
        public bool enableBackstabbing = true; // Whether backstabbing interactions are enabled
        public float baseBackstabbingChance = 0.05f; // Base chance for backstabbing attempts

        // Children misbehavior settings
        public bool enableChildrenMisbehavior = true; // Whether children misbehavior is enabled
        public float baseChildrenMisbehaviorChance = 0.1f; // Base chance for children misbehavior
        public float childrenMisbehaviorParentOpinionImpact = 0.5f; // How much parental opinion affects misbehavior chance (higher = more impact)

        // TTS Settings
        public bool enableTTS = false;
        public TtsApiType ttsApiType = TtsApiType.OpenAI;
        public float ttsVolume = 100f;
        public float ttsSpeed = 1.0f;
        public bool ttsMuted = false;
        public bool ttsInternalPlayback = true; // Skip flag for Player2

        public string ttsApiUrl = "http://localhost:8880/v1/audio/speech";
        public string ttsApiKey = "";
        public string ttsModel = "tts-1";

        // Audio format setting
        public UnityEngine.AudioType ttsAudioFormat = UnityEngine.AudioType.WAV;

        // MakeUp/Apologizing interaction settings
        public float baseMakeUpChance = 0.08f; // Base chance for make-up/apologizing attempts
        public float makeUpPositiveOpinionMultiplier = 1.5f; // Multiplier when opinion is positive
        public float makeUpNegativeOpinionMultiplier = 0.7f; // Multiplier when opinion is negative
        
        // Pester Prisoner/Slave settings
        public bool enablePesterPrisonerFeature = true; // Whether pester prisoner feature is enabled
        public int pesterPrisonerDuration = 7200; // Duration in ticks (2 minutes)
        public int pesterInsultIntervalMin = 900; // Minimum interval between insults (15 seconds)
        public int pesterInsultIntervalMax = 1800; // Maximum interval between insults (30 seconds)
        public float pesterJoyGainRate = 0.0001f; // Joy gain per tick
        public float pesterSuppressionAmount = 0.1f; // Default suppression increase per insult

        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pawnsStopOnInteraction, "pawnsStopOnInteraction", true);
            Scribe_Values.Look(ref showDefaultBubbles, "showDefaultBubbles", true);
            Scribe_Values.Look(ref showLlmBubbles, "showLlmBubbles", true);
            Scribe_Values.Look(ref disableLlmThinking, "disableLlmThinking", true);
            Scribe_Values.Look(ref enableCombatTaunts, "enableCombatTaunts", true);
            Scribe_Values.Look(ref llmInteractionsEnabled, "llmInteractionsEnabled", false);
            Scribe_Values.Look(ref llmApiType, "llmApiType", LlmApiType.KoboldCpp);
            Scribe_Values.Look(ref llmApiUrl, "llmApiUrl", "");
            Scribe_Values.Look(ref llmApiKey, "llmApiKey", "");
            Scribe_Values.Look(ref llmPromptTemplate, "llmPromptTemplate", "");
            Scribe_Values.Look(ref llmMonologuePromptTemplate, "llmMonologuePromptTemplate", "");
            Scribe_Values.Look(ref wordsPerLineLimit, "wordsPerLineLimit", 10);
            Scribe_Values.Look(ref wordsPerSecond, "wordsPerSecond", 3.0f);
            Scribe_Values.Look(ref llmMaxDialogueLines, "llmMaxDialogueLines", 10);
            
            Scribe_Values.Look(ref llmTemperature, "llmTemperature", 0.7f);
            Scribe_Values.Look(ref llmMaxTokens, "llmMaxTokens", 300);
            Scribe_Values.Look(ref llmTopK, "llmTopK", 40);
            Scribe_Values.Look(ref llmTopP, "llmTopP", 1.0f);
            Scribe_Values.Look(ref llmMinP, "llmMinP", 0.05f);
            Scribe_Values.Look(ref llmRepetitionPenalty, "llmRepetitionPenalty", 1.0f);
            Scribe_Values.Look(ref ollamaModelName, "ollamaModelName", "llama3.2");
            Scribe_Values.Look(ref lmStudioModelName, "lmStudioModelName", "gemma-2-2b-it");
            Scribe_Values.Look(ref geminiModelName, "geminiModelName", "gemini-2.5-flash");
            Scribe_Values.Look(ref qwenModelName, "qwenModelName", "qwen-max");
            Scribe_Values.Look(ref deepseekModelName, "deepseekModelName", "deepseek-chat");
            Scribe_Values.Look(ref grokModelName, "grokModelName", "grok-3-mini");
            Scribe_Values.Look(ref claudeModelName, "claudeModelName", "claude-3-sonnet-20240229");
            Scribe_Values.Look(ref player2ModelName, "player2ModelName", "p2-intelligence-v1");
            Scribe_Values.Look(ref player2GameClientId, "player2GameClientId", "019c9077-e849-7348-9d91-593f91438fb3");

            Scribe_Values.Look(ref enableChitchat, "enableChitchat", true);
            Scribe_Values.Look(ref enableManualChat, "enableManualChat", true); // New setting for manual chat
            Scribe_Values.Look(ref enableInteractiveNegotiation, "enableInteractiveNegotiation", true);
            Scribe_Values.Look(ref enableDeepTalk, "enableDeepTalk", true);
            Scribe_Values.Look(ref enableInsult, "enableInsult", true);
            Scribe_Values.Look(ref enableRomanceAttempt, "enableRomanceAttempt", true);
            Scribe_Values.Look(ref enableMarriageProposal, "enableMarriageProposal", true);
            Scribe_Values.Look(ref enableReassure, "enableReassure", true);
            Scribe_Values.Look(ref enableDisturbingChat, "enableDisturbingChat", true);
            Scribe_Values.Look(ref enableTendPatient, "enableTendPatient", true);
            Scribe_Values.Look(ref enableRescue, "enableRescue", true);
            Scribe_Values.Look(ref enableVisitSickPawn, "enableVisitSickPawn", true);
            Scribe_Values.Look(ref enableLovin, "enableLovin", true);
            Scribe_Values.Look(ref enableDating, "enableDating", true);
            Scribe_Values.Look(ref enableMarriageCeremony, "enableMarriageCeremony", true);
            Scribe_Values.Look(ref enableDatingFeature, "enableDatingFeature", true);
            Scribe_Values.Look(ref enableBreakups, "enableBreakups", true);
            Scribe_Values.Look(ref useLlmForBreakups, "useLlmForBreakups", true);
            Scribe_Values.Look(ref enableIdeologyConversionInteractions, "enableIdeologyConversionInteractions", true);
            Scribe_Values.Look(ref enableKindWordsInteractions, "enableKindWordsInteractions", true);
            Scribe_Values.Look(ref enableRaidNegotiation, "enableRaidNegotiation", true);
            
            // New interaction toggles
            Scribe_Values.Look(ref enableFlirt, "enableFlirt", true);
            Scribe_Values.Look(ref enableSlight, "enableSlight", true);
            Scribe_Values.Look(ref enableIncestuousFlirt, "enableIncestuousFlirt", true);
            Scribe_Values.Look(ref enableRapport, "enableRapport", true);
            Scribe_Values.Look(ref enableRecruitAttempt, "enableRecruitAttempt", true);
            Scribe_Values.Look(ref enableReduceResistance, "enableReduceResistance", true);
            Scribe_Values.Look(ref enableReduceWill, "enableReduceWill", true);
            Scribe_Values.Look(ref enableEnslaveAttempt, "enableEnslaveAttempt", true);

            // Monologue event toggles
            Scribe_Values.Look(ref enableMasterworkMonologue, "enableMasterworkMonologue", true);
            Scribe_Values.Look(ref enableInspirationMonologue, "enableInspirationMonologue", true);

            Scribe_Values.Look(ref negotiationCooldownHours, "negotiationCooldownHours", 24.0f);
            Scribe_Values.Look(ref enableDrama, "enableDrama", false);
            Scribe_Values.Look(ref llmStoppingStrings, "llmStoppingStrings", "");
            Scribe_Values.Look(ref preventSpam, "preventSpam", false);
            Scribe_Values.Look(ref enableXtcSampling, "enableXtcSampling", false);
            Scribe_Values.Look(ref joyThresholdForDate, "joyThresholdForDate", 0.8f);
            Scribe_Values.Look(ref verboseLogging, "verboseLogging", false);
            Scribe_Values.Look(ref forceChatCompletion, "forceChatCompletion", true);
            
            Scribe_Values.Look(ref useBackgroundTextRendering, "useBackgroundTextRendering", false);
            
            // Magic number settings (not exposed in UI)
            Scribe_Values.Look(ref meleeTauntProbability, "meleeTauntProbability", 0.35f);
            Scribe_Values.Look(ref shootTauntProbability, "shootTauntProbability", 0.15f);
            Scribe_Values.Look(ref gettingHitComplaintProbability, "gettingHitComplaintProbability", 0.3f);
            Scribe_Values.Look(ref downedCallForHelpProbability, "downedCallForHelpProbability", 0.85f);
            Scribe_Values.Look(ref baseLovinChance, "baseLovinChance", 0.75f);
            Scribe_Values.Look(ref dateCooldownTicks, "dateCooldownTicks", 3000);
            Scribe_Values.Look(ref dateLovinTicks, "dateLovinTicks", 2000);
            Scribe_Values.Look(ref cheatingConfrontationTicks, "cheatingConfrontationTicks", 300);
            Scribe_Values.Look(ref maxDistanceForDate, "maxDistanceForDate", 50);
            Scribe_Values.Look(ref jobCheckIntervalTicks, "jobCheckIntervalTicks", 60);
            Scribe_Values.Look(ref initialToleranceTicks, "initialToleranceTicks", 60);
            Scribe_Values.Look(ref goOnDateCooldownTicks, "goOnDateCooldownTicks", 600);
            
            // Dating lovin' settings
            Scribe_Values.Look(ref dateLovinTimeoutTicks, "dateLovinTimeoutTicks", 300);
            Scribe_Values.Look(ref maxDistanceToLovinSpot, "maxDistanceToLovinSpot", 50f);
            
            // Dating partner selection weights/penalties
            Scribe_Values.Look(ref spouseDateWeight, "spouseDateWeight", 100f);
            Scribe_Values.Look(ref fianceDateWeight, "fianceDateWeight", 90f);
            Scribe_Values.Look(ref loverDateWeight, "loverDateWeight", 80f);
            Scribe_Values.Look(ref opinionAdjustmentFactor, "opinionAdjustmentFactor", 50f);
            Scribe_Values.Look(ref nonRelatedPartnerWeightFactor, "nonRelatedPartnerWeightFactor", 1.0f);
            Scribe_Values.Look(ref cheatingPenalty, "cheatingPenalty", 30f);
            Scribe_Values.Look(ref opinionDifferenceThreshold, "opinionDifferenceThreshold", 20f);
            
            // Badmouthing interaction settings (for debugging/tweaking)
            Scribe_Values.Look(ref baseBadmouthingChance, "baseBadmouthingChance", 0.05f);
            Scribe_Values.Look(ref traitEncouragedBadmouthingChance, "traitEncouragedBadmouthingChance", 0.25f);
            Scribe_Values.Look(ref badOpinionAdditionalChance, "badOpinionAdditionalChance", 0.15f);
            Scribe_Values.Look(ref badmouthingOpinionReductionForTarget, "badmouthingOpinionReductionForTarget", -5);
            Scribe_Values.Look(ref badmouthingOpinionReductionForInitiator, "badmouthingOpinionReductionForInitiator", -8);
            Scribe_Values.Look(ref badmouthingLowOpinionThreshold, "badmouthingLowOpinionThreshold", 0);
            
            // Enhanced Chitchat Insult settings (for debugging/tweaking)
            Scribe_Values.Look(ref baseEnhancedChitchatInsultChance, "baseEnhancedChitchatInsultChance", 0.05f);
            Scribe_Values.Look(ref enhancedChitchatInsultMoodMultiplierBad, "enhancedChitchatInsultMoodMultiplierBad", 1.5f);
            Scribe_Values.Look(ref enhancedChitchatInsultMoodMultiplierGood, "enhancedChitchatInsultMoodMultiplierGood", 0.7f);
            Scribe_Values.Look(ref enhancedChitchatInsultOpinionMultiplierVeryNegative, "enhancedChitchatInsultOpinionMultiplierVeryNegative", 2.0f);
            Scribe_Values.Look(ref enhancedChitchatInsultOpinionMultiplierVeryPositive, "enhancedChitchatInsultOpinionMultiplierVeryPositive", 0.6f);
            Scribe_Values.Look(ref enhancedChitchatInsultTraitMultiplier, "enhancedChitchatInsultTraitMultiplier", 1.8f);
            Scribe_Values.Look(ref enhancedChitchatInsultOpinionDifferenceMultiplier, "enhancedChitchatInsultOpinionDifferenceMultiplier", 0.5f);
            
            // Admiration interaction settings (for debugging/tweaking)
            Scribe_Values.Look(ref baseAdmirationChance, "baseAdmirationChance", 0.05f);
            Scribe_Values.Look(ref admirationAttractionMultiplier, "admirationAttractionMultiplier", 2.0f);
            Scribe_Values.Look(ref admirationPositiveOpinionMultiplier, "admirationPositiveOpinionMultiplier", 1.5f);
            
            // Admiration opinion impact settings
            Scribe_Values.Look(ref admirationOpinionIncreaseOnSuccess, "admirationOpinionIncreaseOnSuccess", 3f);
            Scribe_Values.Look(ref admirationOpinionDecreaseOnFail, "admirationOpinionDecreaseOnFail", -1f);
            Scribe_Values.Look(ref admirationNegativeImpactChance, "admirationNegativeImpactChance", 0.1f);
            
            // Backstabbing interaction settings
            Scribe_Values.Look(ref enableBackstabbing, "enableBackstabbing", true);
            Scribe_Values.Look(ref baseBackstabbingChance, "baseBackstabbingChance", 0.05f);

            // Children misbehavior settings
            Scribe_Values.Look(ref enableChildrenMisbehavior, "enableChildrenMisbehavior", true);
            Scribe_Values.Look(ref baseChildrenMisbehaviorChance, "baseChildrenMisbehaviorChance", 0.9f);
            Scribe_Values.Look(ref childrenMisbehaviorParentOpinionImpact, "childrenMisbehaviorParentOpinionImpact", 0.5f);

            // MakeUp/Apologizing interaction settings
            Scribe_Values.Look(ref baseMakeUpChance, "baseMakeUpChance", 0.08f);
            Scribe_Values.Look(ref makeUpPositiveOpinionMultiplier, "makeUpPositiveOpinionMultiplier", 1.5f);
            Scribe_Values.Look(ref makeUpNegativeOpinionMultiplier, "makeUpNegativeOpinionMultiplier", 0.7f);

            // Pester Prisoner/Slave settings
            Scribe_Values.Look(ref enablePesterPrisonerFeature, "enablePesterPrisonerFeature", true);
            Scribe_Values.Look(ref pesterPrisonerDuration, "pesterPrisonerDuration", 7200);
            Scribe_Values.Look(ref pesterInsultIntervalMin, "pesterInsultIntervalMin", 1800);
            Scribe_Values.Look(ref pesterInsultIntervalMax, "pesterInsultIntervalMax", 3600);
            Scribe_Values.Look(ref pesterJoyGainRate, "pesterJoyGainRate", 0.0001f);
            Scribe_Values.Look(ref pesterSuppressionAmount, "pesterSuppressionAmount", 0.1f);


            // TTS Settings
            Scribe_Values.Look(ref enableTTS, "enableTTS", false);
            Scribe_Values.Look(ref ttsApiType, "ttsApiType", TtsApiType.OpenAI);
            Scribe_Values.Look(ref ttsVolume, "ttsVolume", 100f);
            Scribe_Values.Look(ref ttsSpeed, "ttsSpeed", 1.0f);
            Scribe_Values.Look(ref ttsMuted, "ttsMuted", false);
            Scribe_Values.Look(ref ttsInternalPlayback, "ttsInternalPlayback", true);
            Scribe_Values.Look(ref ttsApiUrl, "ttsApiUrl", "http://localhost:8880/v1/audio/speech");
            Scribe_Values.Look(ref ttsApiKey, "ttsApiKey", "");
            Scribe_Values.Look(ref ttsModel, "ttsModel", "tts-1");
            Scribe_Values.Look(ref ttsAudioFormat, "ttsAudioFormat", AudioType.WAV);

            // Version tracking
            Scribe_Values.Look(ref modVersion, "modVersion", CURRENT_VERSION);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                // Check if the loaded version is older than the current version
                // This ensures we reset templates to support the new API-specific endings
                Version currentVer;
                Version loadedVer;
                
                bool currentParsed = Version.TryParse(CURRENT_VERSION, out currentVer);
                bool loadedParsed = Version.TryParse(modVersion, out loadedVer);

                if (currentParsed && loadedParsed && loadedVer < currentVer)
                {
                    SLog.Message(string.Format("[SocialInteractions] Detected mod update to {0}. Resetting prompt templates to default to support new API features.", CURRENT_VERSION));
                    llmPromptTemplate = DEFAULT_DIALOGUE_TEMPLATE;
                    llmMonologuePromptTemplate = DEFAULT_MONOLOGUE_TEMPLATE;
                    
                    // Update the version to current so we don't reset again
                    modVersion = CURRENT_VERSION;
                }
                else if (!currentParsed || !loadedParsed)
                {
                    // Fallback to string comparison if parsing fails
                    if (string.Compare(modVersion, CURRENT_VERSION) < 0)
                    {
                        SLog.Message(string.Format("[SocialInteractions] Detected mod update to {0}. Resetting prompt templates to default to support new API features.", CURRENT_VERSION));
                        llmPromptTemplate = DEFAULT_DIALOGUE_TEMPLATE;
                        llmMonologuePromptTemplate = DEFAULT_MONOLOGUE_TEMPLATE;
                        
                        // Update the version to current so we don't reset again
                        modVersion = CURRENT_VERSION;
                    }
                }
            }
        }

        private static void AssignVoicesToAllColonists()
        {
            if (Find.CurrentMap != null)
            {
                var manager = Current.Game.GetComponent<VoiceAssignmentManager>();
                if (manager != null)
                {
                    // Get all colonists and ensure they have voices assigned
                    List<Pawn> colonists = Find.CurrentMap.mapPawns.FreeColonists;
                    foreach (Pawn colonist in colonists)
                    {
                        if (colonist != null)
                        {
                            // Calling GetOrAssignVoice will assign a voice if one isn't already assigned
                            string voice = manager.GetOrAssignVoice(colonist);
                        }
                    }
                }
            }
        }
    }

    public class SocialInteractionsMod : Mod
    {
        private Vector2 scrollPosition = Vector2.zero;
        private string llmApiUrlBuffer;
        private string llmApiKeyBuffer;
        private string llmPromptTemplateBuffer;
        private string llmMonologuePromptTemplateBuffer;
        private string openAiModelNameBuffer;
        
        // TTS Buffers
        private string ttsApiUrlBuffer;
        private string ttsApiKeyBuffer;
        private string ttsModelBuffer;

        public SocialInteractionsMod(ModContentPack content)
            : base(content)
        {
            SocialInteractions.Settings = GetSettings<SocialInteractionsModSettings>();
            llmApiUrlBuffer = SocialInteractions.Settings.llmApiUrl;
            llmApiKeyBuffer = SocialInteractions.Settings.llmApiKey;
            llmPromptTemplateBuffer = SocialInteractions.Settings.llmPromptTemplate;
            llmMonologuePromptTemplateBuffer = SocialInteractions.Settings.llmMonologuePromptTemplate;
            ttsApiUrlBuffer = SocialInteractions.Settings.ttsApiUrl;
            ttsApiKeyBuffer = SocialInteractions.Settings.ttsApiKey;
            ttsModelBuffer = SocialInteractions.Settings.ttsModel;
            openAiModelNameBuffer = SocialInteractions.Settings.openAiModelName;
        }

        public override string SettingsCategory()
        {
            return "SocialInteractions_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(inRect.x, inRect.y, inRect.width - 16f, inRect.height * 6); // Adjust height as needed
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(viewRect);
            string settingsTitle = string.Format("{0} v{1}", "SocialInteractions_SettingsTitle".Translate(), SocialInteractions.Settings.modVersion);
            listingStandard.Label(settingsTitle);
            
            listingStandard.Gap();
            listingStandard.CheckboxLabeled("SocialInteractions_EnableVerboseLogging".Translate(), ref SocialInteractions.Settings.verboseLogging, "SocialInteractions_EnableVerboseLoggingDesc".Translate());

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("SocialInteractions_PawnsStopOnInteraction".Translate(), ref SocialInteractions.Settings.pawnsStopOnInteraction, "SocialInteractions_PawnsStopOnInteractionDesc".Translate());

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("SocialInteractions_ShowDefaultBubbles".Translate(), ref SocialInteractions.Settings.showDefaultBubbles, "SocialInteractions_ShowDefaultBubblesDesc".Translate());

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("SocialInteractions_ShowLlmBubbles".Translate(), ref SocialInteractions.Settings.showLlmBubbles, "SocialInteractions_ShowLlmBubblesDesc".Translate());

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("SocialInteractions_EnableCombatTaunts".Translate(), ref SocialInteractions.Settings.enableCombatTaunts, "SocialInteractions_EnableCombatTauntsDesc".Translate());

            // Drama interactions setting
            listingStandard.Gap();
            listingStandard.CheckboxLabeled("SocialInteractions_EnableDrama".Translate(), ref SocialInteractions.Settings.enableDrama, "SocialInteractions_EnableDramaDesc".Translate());

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("SocialInteractions_EnableDatingFeature".Translate(), ref SocialInteractions.Settings.enableDatingFeature, "SocialInteractions_EnableDatingFeatureDesc".Translate());
            listingStandard.Label(string.Format("SocialInteractions_JoyThresholdForDate".Translate() + " {0}", SocialInteractions.Settings.joyThresholdForDate.ToString("F2")));
            SocialInteractions.Settings.joyThresholdForDate = listingStandard.Slider(SocialInteractions.Settings.joyThresholdForDate, 0f, 1f);
            listingStandard.Label(string.Format("SocialInteractions_BaseLovinChance".Translate() + " {0}", SocialInteractions.Settings.baseLovinChance.ToString("F2")));
            SocialInteractions.Settings.baseLovinChance = listingStandard.Slider(SocialInteractions.Settings.baseLovinChance, 0f, 1f);
            
            // Children misbehavior settings
            listingStandard.Gap();
            listingStandard.CheckboxLabeled("SocialInteractions_EnableChildrenMisbehavior".Translate(), ref SocialInteractions.Settings.enableChildrenMisbehavior, "SocialInteractions_EnableChildrenMisbehaviorDesc".Translate());
            listingStandard.Label(string.Format("SocialInteractions_BaseChance".Translate() + ": {0:F3}", SocialInteractions.Settings.baseChildrenMisbehaviorChance));
            SocialInteractions.Settings.baseChildrenMisbehaviorChance = listingStandard.Slider(SocialInteractions.Settings.baseChildrenMisbehaviorChance, 0f, 1f);

            // Add a button to open the chat log window
            if (listingStandard.ButtonText("SocialInteractions_OpenChatLogWindow".Translate()))
            {
                // Open the chat log tab
                Find.MainTabsRoot.SetCurrentTab(DefDatabase<RimWorld.MainButtonDef>.GetNamed("SocialInteractions_ChatLog"));
            }


            listingStandard.Gap();
            listingStandard.CheckboxLabeled("SocialInteractions_EnableLLMInteractions".Translate(), ref SocialInteractions.Settings.llmInteractionsEnabled, "SocialInteractions_EnableLLMInteractionsDesc".Translate());

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("SocialInteractions_PreventSpam".Translate(), ref SocialInteractions.Settings.preventSpam, "SocialInteractions_PreventSpamDesc".Translate());

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("SocialInteractions_UseTextBackground".Translate(), ref SocialInteractions.Settings.useBackgroundTextRendering, "SocialInteractions_UseTextBackgroundDesc".Translate());

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("SocialInteractions_EnableInteractiveNegotiation".Translate(), ref SocialInteractions.Settings.enableInteractiveNegotiation, "SocialInteractions_EnableInteractiveNegotiationDesc".Translate());

            listingStandard.Gap();
            listingStandard.Label(string.Format("SocialInteractions_NegotiationCooldownHours".Translate() + ": {0:F1} " + "SocialInteractions_Hours".Translate(), SocialInteractions.Settings.negotiationCooldownHours));
            SocialInteractions.Settings.negotiationCooldownHours = listingStandard.Slider(SocialInteractions.Settings.negotiationCooldownHours, 0f, 168f);

            // Badmouthing settings
            listingStandard.Gap();
            listingStandard.Label("SocialInteractions_BadmouthingSettings".Translate());
            listingStandard.Label(string.Format("SocialInteractions_BaseChance".Translate() + ": {0:F3}", SocialInteractions.Settings.baseBadmouthingChance));
            SocialInteractions.Settings.baseBadmouthingChance = listingStandard.Slider(SocialInteractions.Settings.baseBadmouthingChance, 0f, 1f);

            // Enhanced Chitchat Insult settings
            listingStandard.Gap();
            listingStandard.Label("SocialInteractions_EnhancedChitchatInsultSettings".Translate());
            listingStandard.Label(string.Format("SocialInteractions_BaseChance".Translate() + ": {0:F3}", SocialInteractions.Settings.baseEnhancedChitchatInsultChance));
            SocialInteractions.Settings.baseEnhancedChitchatInsultChance = listingStandard.Slider(SocialInteractions.Settings.baseEnhancedChitchatInsultChance, 0f, 1f);
            
            // listingStandard.Label(string.Format("Mood multiplier (bad mood): {0:F2}", SocialInteractions.Settings.enhancedChitchatInsultMoodMultiplierBad));
            // SocialInteractions.Settings.enhancedChitchatInsultMoodMultiplierBad = listingStandard.Slider(SocialInteractions.Settings.enhancedChitchatInsultMoodMultiplierBad, 0.1f, 5f);
            
            // listingStandard.Label(string.Format("Mood multiplier (good mood): {0:F2}", SocialInteractions.Settings.enhancedChitchatInsultMoodMultiplierGood));
            // SocialInteractions.Settings.enhancedChitchatInsultMoodMultiplierGood = listingStandard.Slider(SocialInteractions.Settings.enhancedChitchatInsultMoodMultiplierGood, 0.1f, 1f);
            
            // listingStandard.Label(string.Format("Opinion multiplier (very negative): {0:F2}", SocialInteractions.Settings.enhancedChitchatInsultOpinionMultiplierVeryNegative));
            // SocialInteractions.Settings.enhancedChitchatInsultOpinionMultiplierVeryNegative = listingStandard.Slider(SocialInteractions.Settings.enhancedChitchatInsultOpinionMultiplierVeryNegative, 0.5f, 5f);
            
            // listingStandard.Label(string.Format("Opinion multiplier (very positive): {0:F2}", SocialInteractions.Settings.enhancedChitchatInsultOpinionMultiplierVeryPositive));
            // SocialInteractions.Settings.enhancedChitchatInsultOpinionMultiplierVeryPositive = listingStandard.Slider(SocialInteractions.Settings.enhancedChitchatInsultOpinionMultiplierVeryPositive, 0.1f, 1f);
            
            // listingStandard.Label(string.Format("Trait multiplier: {0:F2}", SocialInteractions.Settings.enhancedChitchatInsultTraitMultiplier));
            // SocialInteractions.Settings.enhancedChitchatInsultTraitMultiplier = listingStandard.Slider(SocialInteractions.Settings.enhancedChitchatInsultTraitMultiplier, 0.5f, 5f);
            
            // listingStandard.Label(string.Format("Opinion difference impact: {0:F2}", SocialInteractions.Settings.enhancedChitchatInsultOpinionDifferenceMultiplier));
            // SocialInteractions.Settings.enhancedChitchatInsultOpinionDifferenceMultiplier = listingStandard.Slider(SocialInteractions.Settings.enhancedChitchatInsultOpinionDifferenceMultiplier, 0f, 2f);

            // Admiration settings
            listingStandard.Gap();
            listingStandard.Label("SocialInteractions_AdmirationSettings".Translate());
            listingStandard.Label(string.Format("SocialInteractions_BaseChance".Translate() + ": {0:F3}", SocialInteractions.Settings.baseAdmirationChance));
            SocialInteractions.Settings.baseAdmirationChance = listingStandard.Slider(SocialInteractions.Settings.baseAdmirationChance, 0f, 1f);
            
            // listingStandard.Label(string.Format("Attraction multiplier: {0:F2}", SocialInteractions.Settings.admirationAttractionMultiplier));
            // SocialInteractions.Settings.admirationAttractionMultiplier = listingStandard.Slider(SocialInteractions.Settings.admirationAttractionMultiplier, 0.5f, 5f);
            
            // listingStandard.Label(string.Format("Positive opinion multiplier: {0:F2}", SocialInteractions.Settings.admirationPositiveOpinionMultiplier));
            // SocialInteractions.Settings.admirationPositiveOpinionMultiplier = listingStandard.Slider(SocialInteractions.Settings.admirationPositiveOpinionMultiplier, 0.5f, 3f);

            // Admiration opinion impact settings
            // listingStandard.Gap();
            // listingStandard.Label("Admiration Opinion Impact:");
            // listingStandard.Label(string.Format("Opinion increase on success: {0:F1}", SocialInteractions.Settings.admirationOpinionIncreaseOnSuccess));
            // SocialInteractions.Settings.admirationOpinionIncreaseOnSuccess = listingStandard.Slider(SocialInteractions.Settings.admirationOpinionIncreaseOnSuccess, 0f, 10f);
            
            // listingStandard.Label(string.Format("Negative impact chance: {0:F2}", SocialInteractions.Settings.admirationNegativeImpactChance));
            // SocialInteractions.Settings.admirationNegativeImpactChance = listingStandard.Slider(SocialInteractions.Settings.admirationNegativeImpactChance, 0f, 0.5f);
            
            // listingStandard.Label(string.Format("Opinion change on failure: {0:F1}", SocialInteractions.Settings.admirationOpinionDecreaseOnFail));
            // SocialInteractions.Settings.admirationOpinionDecreaseOnFail = listingStandard.Slider(SocialInteractions.Settings.admirationOpinionDecreaseOnFail, -5f, 0f);

            // Backstabbing settings
            listingStandard.Gap();
            listingStandard.CheckboxLabeled("SocialInteractions_EnableBackstabbing".Translate(), ref SocialInteractions.Settings.enableBackstabbing, "SocialInteractions_EnableBackstabbingDesc".Translate());
            listingStandard.Label(string.Format("SocialInteractions_BaseChance".Translate() + ": {0:F3}", SocialInteractions.Settings.baseBackstabbingChance));
            SocialInteractions.Settings.baseBackstabbingChance = listingStandard.Slider(SocialInteractions.Settings.baseBackstabbingChance, 0f, 1f);

            // MakeUp/Apologizing settings
            listingStandard.Gap();
            listingStandard.Label("SocialInteractions_MakeUpSettings".Translate());
            listingStandard.Label(string.Format("SocialInteractions_BaseChance".Translate() + ": {0:F3}", SocialInteractions.Settings.baseMakeUpChance));
            SocialInteractions.Settings.baseMakeUpChance = listingStandard.Slider(SocialInteractions.Settings.baseMakeUpChance, 0f, 1f);

            // TTS Settings
            listingStandard.Gap();
            bool oldEnableTTS = SocialInteractions.Settings.enableTTS;
            listingStandard.CheckboxLabeled("SocialInteractions_EnableTTS".Translate(), ref SocialInteractions.Settings.enableTTS, "SocialInteractions_EnableTTSDesc".Translate());
            
            if (SocialInteractions.Settings.enableTTS && !oldEnableTTS)
            {
                // Auto-fetch when enabled
                TTSManager.FetchVoicesFromApi();
            }

            // Toggle Main Button visibility
            if (oldEnableTTS != SocialInteractions.Settings.enableTTS)
            {
                MainButtonDef ttsDef = DefDatabase<MainButtonDef>.GetNamed("SocialInteractions_TTSMute", false);
                if (ttsDef != null)
                {
                    ttsDef.buttonVisible = SocialInteractions.Settings.enableTTS;
                    // Force refresh of main buttons
                    MainButtonDef rDef = DefDatabase<MainButtonDef>.GetNamed("Research", false); 
                    // Hacky: Changing buttonVisible usually requires a refresh. 
                    // RimWorld checks VisibleMainButtons frequently.
                }
            }
            
            if (SocialInteractions.Settings.enableTTS)
            {
                listingStandard.Label(string.Format("SocialInteractions_TTSVolume".Translate() + ": {0}%", (int)SocialInteractions.Settings.ttsVolume));
                SocialInteractions.Settings.ttsVolume = listingStandard.Slider(SocialInteractions.Settings.ttsVolume, 0f, 200f);
                    
                listingStandard.Label(string.Format("SocialInteractions_TTSSpeed".Translate() + ": {0}x", SocialInteractions.Settings.ttsSpeed.ToString("F2")));
                SocialInteractions.Settings.ttsSpeed = listingStandard.Slider(SocialInteractions.Settings.ttsSpeed, 0.25f, 4.0f);
                    
                listingStandard.Gap();
                
                // TTS API Type Selection (Mirroring LLM style)
                listingStandard.Label("SocialInteractions_TtsApiType".Translate());
                string[] ttsTypeNames = new string[] {
                    "OpenAI",
                    "Player2"
                };
                TtsApiType[] ttsTypeValues = (TtsApiType[])System.Enum.GetValues(typeof(TtsApiType));
                int currentTtsTypeIndex = System.Array.IndexOf(ttsTypeValues, SocialInteractions.Settings.ttsApiType);

                Rect ttsRowRect = listingStandard.GetRect(30f);
                float ttsButtonWidth = ttsRowRect.width / ttsTypeNames.Length;
                for (int i = 0; i < ttsTypeNames.Length; i++)
                {
                    Rect buttonRect = new Rect(ttsRowRect.x + i * ttsButtonWidth, ttsRowRect.y, ttsButtonWidth, ttsRowRect.height);
                    if (Widgets.ButtonText(buttonRect, ttsTypeNames[i]))
                    {
                        SocialInteractions.Settings.ttsApiType = ttsTypeValues[i];
                        switch (ttsTypeValues[i])
                        {
                            case TtsApiType.OpenAI:
                                SocialInteractions.Settings.ttsApiUrl = "http://localhost:8880/v1/audio/speech";
                                ttsApiUrlBuffer = "http://localhost:8880/v1/audio/speech";
                                SocialInteractions.Settings.ttsModel = "tts-1";
                                ttsModelBuffer = "tts-1";
                                break;
                            case TtsApiType.Player2:
                                SocialInteractions.Settings.ttsApiUrl = "http://127.0.0.1:4315/v1/tts/speak";
                                ttsApiUrlBuffer = "http://127.0.0.1:4315/v1/tts/speak";
                                SocialInteractions.Settings.ttsModel = "p2-intelligence-v1";
                                ttsModelBuffer = "p2-intelligence-v1";
                                break;
                        }
                    }
                }
                
                listingStandard.Gap();
                listingStandard.Label("SocialInteractions_TTSApiUrl".Translate());
                ttsApiUrlBuffer = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), ttsApiUrlBuffer);
                SocialInteractions.Settings.ttsApiUrl = ttsApiUrlBuffer;

                listingStandard.Label("SocialInteractions_TTSApiKey".Translate());
                ttsApiKeyBuffer = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), ttsApiKeyBuffer);
                SocialInteractions.Settings.ttsApiKey = ttsApiKeyBuffer;

                listingStandard.Label("SocialInteractions_TTSModel".Translate());
                ttsModelBuffer = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), ttsModelBuffer);
                SocialInteractions.Settings.ttsModel = ttsModelBuffer;
                

                    
                if (listingStandard.ButtonText("SocialInteractions_RemapVoices".Translate()))
                {
                    if (Current.Game != null)
                    {
                        var manager = Current.Game.GetComponent<VoiceAssignmentManager>();
                        if (manager != null)
                        {
                            manager.ResetAllocations();
                            TTSManager.FetchVoicesFromApi();
                            Messages.Message("Voice allocations reset and fetching new voices...", MessageTypeDefOf.PositiveEvent, false);

                            // Also assign voices proactively to all colonists to make them visible
                            LongEventHandler.ExecuteWhenFinished(() => {
                                // Fetch all colonists and ensure they have voices assigned
                                if (Find.CurrentMap != null)
                                {
                                    var voiceManager = Current.Game.GetComponent<VoiceAssignmentManager>();
                                    if (voiceManager != null)
                                    {
                                        // Get all colonists and assign unique voices to avoid duplicates
                                        List<Pawn> colonists = Find.CurrentMap.mapPawns.FreeColonists;
                                        voiceManager.AssignUniqueVoices(colonists);
                                    }
                                }
                            });
                        }
                    }
                }

                if (listingStandard.ButtonText("SocialInteractions_AssignVoicesManually".Translate()))
                {
                    if (Current.Game != null)
                    {
                        // Get all colonists to offer voice assignment
                        List<Pawn> colonists = Find.CurrentMap.mapPawns.FreeColonists;

                        if (colonists.Count == 0)
                        {
                            Messages.Message("No colonists available to assign voices to.", MessageTypeDefOf.RejectInput);
                        }
                        else
                        {
                            // Proactively assign voices to all colonists to ensure current assignments are visible
                            var voiceManager = Current.Game.GetComponent<VoiceAssignmentManager>();
                            if (voiceManager != null)
                            {
                                foreach (Pawn colonist in colonists)
                                {
                                    if (colonist != null)
                                    {
                                        // Calling GetOrAssignVoice will assign a voice if one isn't already assigned
                                        string voice = voiceManager.GetOrAssignVoice(colonist);
                                    }
                                }
                            }

                            if (colonists.Count == 1)
                            {
                                // If only one colonist, open the dialog directly
                                SocialInteractions.OpenVoiceSelectionDialog(colonists[0]);
                            }
                            else
                            {
                                // If multiple colonists, show a dialog to select which one
                                Find.WindowStack.Add(new PawnSelectionDialog(colonists));
                            }
                        }
                    }
                }
                    
                int voiceCount = TTSManager.GetVoices().Count;
                if (voiceCount > 0)
                {
                    listingStandard.Label("SocialInteractions_VoicesAvailable".Translate(voiceCount));
                }
            }
            
            listingStandard.Gap();
            listingStandard.Label("SocialInteractions_LLMConfiguration".Translate());

            // API Type Selection
            listingStandard.Gap();
            listingStandard.Label("SocialInteractions_LLMType".Translate());
            string[] apiTypeNames = new string[] {
                "SocialInteractions_KoboldCpp".Translate(),
                "SocialInteractions_Ollama".Translate(),
                "SocialInteractions_LMStudio".Translate(),
                "SocialInteractions_OpenAI".Translate(),
                "SocialInteractions_Gemini".Translate(),
                "SocialInteractions_Qwen".Translate(),
                "SocialInteractions_Deepseek".Translate(),
                "SocialInteractions_Grok".Translate(),
                "SocialInteractions_Claude".Translate(),
                "SocialInteractions_Player2".Translate()
            };
            LlmApiType[] apiTypeValues = (LlmApiType[])System.Enum.GetValues(typeof(LlmApiType));
            int currentApiTypeIndex = System.Array.IndexOf(apiTypeValues, SocialInteractions.Settings.llmApiType);
            
            // Use a horizontal row of buttons instead of SelectionGrid
            Rect rowRect = listingStandard.GetRect(30f);
            float buttonWidth = rowRect.width / apiTypeNames.Length;
            for (int i = 0; i < apiTypeNames.Length; i++)
            {
                Rect buttonRect = new Rect(rowRect.x + i * buttonWidth, rowRect.y, buttonWidth, rowRect.height);
                bool isSelected = (i == currentApiTypeIndex);
                if (Widgets.ButtonText(buttonRect, apiTypeNames[i]))
                {
                    SocialInteractions.Settings.llmApiType = apiTypeValues[i];
                    // Set default URL based on API type
                    switch (apiTypeValues[i])
                    {
                        case LlmApiType.KoboldCpp:
                            SocialInteractions.Settings.llmApiUrl = "http://localhost:5001";
                            llmApiUrlBuffer = "http://localhost:5001";
                            break;
                        case LlmApiType.Ollama:
                            SocialInteractions.Settings.llmApiUrl = "http://localhost:11434";
                            llmApiUrlBuffer = "http://localhost:11434";
                            break;
                        case LlmApiType.LMStudio:
                            SocialInteractions.Settings.llmApiUrl = "http://localhost:1234";
                            llmApiUrlBuffer = "http://localhost:1234";
                            break;
                        case LlmApiType.OpenAI:
                            SocialInteractions.Settings.llmApiUrl = "https://api.openai.com";
                            llmApiUrlBuffer = "https://api.openai.com";
                            break;
                        case LlmApiType.Gemini:
                            SocialInteractions.Settings.llmApiUrl = "https://generativelanguage.googleapis.com";
                            llmApiUrlBuffer = "https://generativelanguage.googleapis.com";
                            break;
                        case LlmApiType.Qwen:
                            SocialInteractions.Settings.llmApiUrl = "https://dashscope.aliyuncs.com";
                            llmApiUrlBuffer = "https://dashscope.aliyuncs.com";
                            break;
                        case LlmApiType.Deepseek:
                            SocialInteractions.Settings.llmApiUrl = "https://api.deepseek.com";
                            llmApiUrlBuffer = "https://api.deepseek.com";
                            break;
                        case LlmApiType.Grok:
                            SocialInteractions.Settings.llmApiUrl = "https://api.x.ai";
                            llmApiUrlBuffer = "https://api.x.ai";
                            break;
                        case LlmApiType.Claude:
                            SocialInteractions.Settings.llmApiUrl = "https://api.anthropic.com";
                            llmApiUrlBuffer = "https://api.anthropic.com";
                            break;
                        case LlmApiType.Player2:
                            SocialInteractions.Settings.llmApiUrl = "http://127.0.0.1:4315";
                            llmApiUrlBuffer = "http://127.0.0.1:4315";
                            break;
                    }

                    // Manage Player2 health heartbeat when switching API type
                    if (apiTypeValues[i] == LlmApiType.Player2)
                    {
                        if (!string.IsNullOrEmpty(SocialInteractions.Settings.player2GameClientId))
                        {
                            Player2ApiClient.StartHealthHeartbeat(SocialInteractions.Settings.llmApiUrl, SocialInteractions.Settings.player2GameClientId);
                        }
                    }
                    else
                    {
                        Player2ApiClient.StopHealthHeartbeat();
                    }
                }
            }

            listingStandard.Label("SocialInteractions_APIURL".Translate());
            string newApiUrl = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), llmApiUrlBuffer);
            if (newApiUrl != llmApiUrlBuffer)
            {
                llmApiUrlBuffer = newApiUrl;
                SocialInteractions.Settings.llmApiUrl = newApiUrl;
            }

            listingStandard.Label("SocialInteractions_APIKey".Translate());
            string newApiKey = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), llmApiKeyBuffer);
            if (newApiKey != llmApiKeyBuffer)
            {
                llmApiKeyBuffer = newApiKey;
                SocialInteractions.Settings.llmApiKey = newApiKey;
            }

            // Ollama-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.Ollama)
            {
                listingStandard.Gap();
                listingStandard.Label("SocialInteractions_OllamaModelName".Translate());
                string newOllamaModel = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), SocialInteractions.Settings.ollamaModelName);
                if (!string.IsNullOrEmpty(newOllamaModel))
                {
                    SocialInteractions.Settings.ollamaModelName = newOllamaModel;
                }
            }
            
            // LM Studio-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.LMStudio)
            {
                listingStandard.Gap();
                listingStandard.Label("SocialInteractions_LMStudioModelName".Translate());
                string newLMStudioModel = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), SocialInteractions.Settings.lmStudioModelName);
                if (!string.IsNullOrEmpty(newLMStudioModel))
                {
                    SocialInteractions.Settings.lmStudioModelName = newLMStudioModel;
                }
            }
            
            // OpenAI-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.OpenAI)
            {
                listingStandard.Gap();
                listingStandard.Label("SocialInteractions_OpenAIModelName".Translate());
                string newOpenAiModel = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), openAiModelNameBuffer);
                if (!string.IsNullOrEmpty(newOpenAiModel))
                {
                    openAiModelNameBuffer = newOpenAiModel;
                    SocialInteractions.Settings.openAiModelName = newOpenAiModel;
                }
            }
            
            // Gemini-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.Gemini)
            {
                listingStandard.Gap();
                listingStandard.Label("SocialInteractions_GeminiModelName".Translate());
                string newGeminiModel = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), SocialInteractions.Settings.geminiModelName);
                if (!string.IsNullOrEmpty(newGeminiModel))
                {
                    SocialInteractions.Settings.geminiModelName = newGeminiModel;
                }
            }
            
            // Qwen-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.Qwen)
            {
                listingStandard.Gap();
                listingStandard.Label("SocialInteractions_QwenModelName".Translate());
                string newQwenModel = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), SocialInteractions.Settings.qwenModelName);
                if (!string.IsNullOrEmpty(newQwenModel))
                {
                    SocialInteractions.Settings.qwenModelName = newQwenModel;
                }
            }
            
            // Deepseek-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.Deepseek)
            {
                listingStandard.Gap();
                listingStandard.Label("SocialInteractions_DeepseekModelName".Translate());
                string newDeepseekModel = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), SocialInteractions.Settings.deepseekModelName);
                if (!string.IsNullOrEmpty(newDeepseekModel))
                {
                    SocialInteractions.Settings.deepseekModelName = newDeepseekModel;
                }
            }
            
            // Grok-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.Grok)
            {
                listingStandard.Gap();
                listingStandard.Label("SocialInteractions_GrokModelName".Translate());
                string newGrokModel = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), SocialInteractions.Settings.grokModelName);
                if (!string.IsNullOrEmpty(newGrokModel))
                {
                    SocialInteractions.Settings.grokModelName = newGrokModel;
                }
            }
            
            // Claude-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.Claude)
            {
                listingStandard.Gap();
                listingStandard.Label("SocialInteractions_ClaudeModelName".Translate());
                string newClaudeModel = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), SocialInteractions.Settings.claudeModelName);
                if (!string.IsNullOrEmpty(newClaudeModel))
                {
                    SocialInteractions.Settings.claudeModelName = newClaudeModel;
                }
            }
            
            // Player2-specific settings
            if (SocialInteractions.Settings.llmApiType == LlmApiType.Player2)
            {
                listingStandard.Gap();
                listingStandard.Label("SocialInteractions_Player2ModelName".Translate());
                string newPlayer2Model = Widgets.TextField(listingStandard.GetRect(Text.LineHeight), SocialInteractions.Settings.player2ModelName);
                if (!string.IsNullOrEmpty(newPlayer2Model))
                {
                    SocialInteractions.Settings.player2ModelName = newPlayer2Model;
                }
            }

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("SocialInteractions_DisableLlmThinking".Translate(), ref SocialInteractions.Settings.disableLlmThinking, "SocialInteractions_DisableLlmThinkingDesc".Translate());

            if (SocialInteractions.Settings.llmApiType == LlmApiType.KoboldCpp || 
                SocialInteractions.Settings.llmApiType == LlmApiType.Ollama || 
                SocialInteractions.Settings.llmApiType == LlmApiType.LMStudio)
            {
                listingStandard.Gap();
                listingStandard.CheckboxLabeled("SocialInteractions_ForceChatCompletion".Translate(), ref SocialInteractions.Settings.forceChatCompletion, "SocialInteractions_ForceChatCompletionDesc".Translate());
            }

            listingStandard.Gap();
            listingStandard.Label("SocialInteractions_PromptTemplate".Translate());
            string newPromptTemplate = Widgets.TextArea(listingStandard.GetRect(200f), llmPromptTemplateBuffer);
            if (newPromptTemplate != llmPromptTemplateBuffer)
            {
                llmPromptTemplateBuffer = newPromptTemplate;
                SocialInteractions.Settings.llmPromptTemplate = newPromptTemplate;
            }

            listingStandard.Gap();
            listingStandard.Label("SocialInteractions_MonologueTemplate".Translate());
            string newMonologuePromptTemplate = Widgets.TextArea(listingStandard.GetRect(200f), llmMonologuePromptTemplateBuffer);
            if (newMonologuePromptTemplate != llmMonologuePromptTemplateBuffer)
            {
                llmMonologuePromptTemplateBuffer = newMonologuePromptTemplate;
                SocialInteractions.Settings.llmMonologuePromptTemplate = newMonologuePromptTemplate;
            }

            // Add Reset Templates button
            listingStandard.Gap();
            listingStandard.Label("SocialInteractions_ResetTemplates".Translate());
            if (listingStandard.ButtonText("SocialInteractions_ResetTemplates".Translate()))
            {
                SocialInteractions.Settings.llmPromptTemplate = SocialInteractionsModSettings.DEFAULT_DIALOGUE_TEMPLATE;
                SocialInteractions.Settings.llmMonologuePromptTemplate = SocialInteractionsModSettings.DEFAULT_MONOLOGUE_TEMPLATE;
                llmPromptTemplateBuffer = SocialInteractions.Settings.llmPromptTemplate;
                llmMonologuePromptTemplateBuffer = SocialInteractions.Settings.llmMonologuePromptTemplate;
            }

            listingStandard.Gap();
            listingStandard.Label("SocialInteractions_StoppingStrings".Translate());
            SocialInteractions.Settings.llmStoppingStrings = Widgets.TextArea(listingStandard.GetRect(100f), SocialInteractions.Settings.llmStoppingStrings);

            listingStandard.Gap();
            listingStandard.Label("SocialInteractions_WordsPerLine".Translate());
            string wordsPerLineBuffer = SocialInteractions.Settings.wordsPerLineLimit.ToString();
            Widgets.TextFieldNumeric(listingStandard.GetRect(Text.LineHeight), ref SocialInteractions.Settings.wordsPerLineLimit, ref wordsPerLineBuffer, 1, 50);

            listingStandard.Gap();
            listingStandard.Label(string.Format("SocialInteractions_WordsPerSecond".Translate() + ": {0} " + "SocialInteractions_WordsPerSecond".Translate(), SocialInteractions.Settings.wordsPerSecond.ToString("F1")));
            SocialInteractions.Settings.wordsPerSecond = listingStandard.Slider(SocialInteractions.Settings.wordsPerSecond, 0.5f, 10.0f);

            listingStandard.Label("SocialInteractions_MaxDialogueLines".Translate() + ": " + SocialInteractions.Settings.llmMaxDialogueLines);
            SocialInteractions.Settings.llmMaxDialogueLines = (int)listingStandard.Slider(SocialInteractions.Settings.llmMaxDialogueLines, 1f, 40f);

            listingStandard.Gap();
            listingStandard.Label("SocialInteractions_MaxTokens".Translate());
            string maxTokensBuffer = SocialInteractions.Settings.llmMaxTokens.ToString();
            Widgets.TextFieldNumeric(listingStandard.GetRect(Text.LineHeight), ref SocialInteractions.Settings.llmMaxTokens, ref maxTokensBuffer, 1, 2000);

            listingStandard.Gap();
            listingStandard.Label(string.Format("SocialInteractions_Temperature".Translate() + " {0:F3}", SocialInteractions.Settings.llmTemperature));
            SocialInteractions.Settings.llmTemperature = listingStandard.Slider(SocialInteractions.Settings.llmTemperature, 0f, 2f);

            listingStandard.Gap();
            listingStandard.Label(string.Format("SocialInteractions_TopK".Translate() + " {0}", SocialInteractions.Settings.llmTopK));
            SocialInteractions.Settings.llmTopK = (int)listingStandard.Slider(SocialInteractions.Settings.llmTopK, 0, 100);

            listingStandard.Gap();
            listingStandard.Label(string.Format("SocialInteractions_TopP".Translate() + " {0:F3}", SocialInteractions.Settings.llmTopP));
            SocialInteractions.Settings.llmTopP = listingStandard.Slider(SocialInteractions.Settings.llmTopP, 0f, 1f);

            listingStandard.Gap();
            listingStandard.Label(string.Format("SocialInteractions_MinP".Translate() + " {0:F3}", SocialInteractions.Settings.llmMinP));
            SocialInteractions.Settings.llmMinP = listingStandard.Slider(SocialInteractions.Settings.llmMinP, 0f, 1f);

            listingStandard.Gap();
            listingStandard.Label(string.Format("SocialInteractions_RepetitionPenalty".Translate() + " {0:F3}", SocialInteractions.Settings.llmRepetitionPenalty));
            SocialInteractions.Settings.llmRepetitionPenalty = listingStandard.Slider(SocialInteractions.Settings.llmRepetitionPenalty, 1f, 2f);

            listingStandard.Gap();
            listingStandard.CheckboxLabeled("SocialInteractions_XTCSampling".Translate(), ref SocialInteractions.Settings.enableXtcSampling, "SocialInteractions_XTCSamplingDesc".Translate());

            listingStandard.Gap();
            listingStandard.Label("SocialInteractions_EnabledLLMInteractions".Translate());
            listingStandard.CheckboxLabeled("SocialInteractions_EnableChitchat".Translate(), ref SocialInteractions.Settings.enableChitchat);
            //listingStandard.CheckboxLabeled("SocialInteractions_EnableManualChat".Translate(), ref SocialInteractions.Settings.enableManualChat); // already handled in the interactive negotiation settings
            listingStandard.CheckboxLabeled("SocialInteractions_EnableDeepTalk".Translate(), ref SocialInteractions.Settings.enableDeepTalk);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableInsult".Translate(), ref SocialInteractions.Settings.enableInsult);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableRomanceAttempt".Translate(), ref SocialInteractions.Settings.enableRomanceAttempt);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableMarriageProposal".Translate(), ref SocialInteractions.Settings.enableMarriageProposal);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableReassure".Translate(), ref SocialInteractions.Settings.enableReassure);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableDisturbingChat".Translate(), ref SocialInteractions.Settings.enableDisturbingChat);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableTendPatient".Translate(), ref SocialInteractions.Settings.enableTendPatient);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableRescue".Translate(), ref SocialInteractions.Settings.enableRescue);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableVisitSickPawn".Translate(), ref SocialInteractions.Settings.enableVisitSickPawn);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableLovin".Translate(), ref SocialInteractions.Settings.enableLovin);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableDating".Translate(), ref SocialInteractions.Settings.enableDating);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableMarriageCeremony".Translate(), ref SocialInteractions.Settings.enableMarriageCeremony);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableBreakups".Translate(), ref SocialInteractions.Settings.enableBreakups);
            //listingStandard.CheckboxLabeled("SocialInteractions_UseLlmForBreakups".Translate(), ref SocialInteractions.Settings.useLlmForBreakups);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableIdeologyConversionInteractions".Translate(), ref SocialInteractions.Settings.enableIdeologyConversionInteractions);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableKindWordsInteractions".Translate(), ref SocialInteractions.Settings.enableKindWordsInteractions);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableFlirt".Translate(), ref SocialInteractions.Settings.enableFlirt);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableSlight".Translate(), ref SocialInteractions.Settings.enableSlight);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableIncestuousFlirt".Translate(), ref SocialInteractions.Settings.enableIncestuousFlirt);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableRapport".Translate(), ref SocialInteractions.Settings.enableRapport);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableRecruitAttempt".Translate(), ref SocialInteractions.Settings.enableRecruitAttempt);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableReduceResistance".Translate(), ref SocialInteractions.Settings.enableReduceResistance);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableReduceWill".Translate(), ref SocialInteractions.Settings.enableReduceWill);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableEnslaveAttempt".Translate(), ref SocialInteractions.Settings.enableEnslaveAttempt);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableMasterworkMonologue".Translate(), ref SocialInteractions.Settings.enableMasterworkMonologue);
            listingStandard.CheckboxLabeled("SocialInteractions_EnableInspirationMonologue".Translate(), ref SocialInteractions.Settings.enableInspirationMonologue);

            listingStandard.End();

            Widgets.EndScrollView();
            base.DoSettingsWindowContents(inRect);
        }
    }
}