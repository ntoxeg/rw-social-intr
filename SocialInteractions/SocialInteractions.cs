using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine;
using System.Linq;
using System.Text.RegularExpressions;

namespace SocialInteractions
{
    [StaticConstructorOnStartup]
    public static class SocialInteractions
    {
        public static SocialInteractionsModSettings Settings { get; set; }
        public static bool isShowingBubble = false;
        
        // Static dictionary to store date partners for cheaters
        public static Dictionary<string, Pawn> CheaterPartners = new Dictionary<string, Pawn>();
        
        // Static field to store the conversation ID for the last cheating interaction
        public static int lastCheatingInteractionConversationId = -1;
        
        // Static dictionary to store custom flavor text for each pawn
        public static Dictionary<int, string> PawnFlavorTexts = new Dictionary<int, string>();
        
        // --- For LLM Efficiency ---
        private static float lastResponseTimeSeconds = 1.0f; // Initial estimate
        // --- End For LLM Efficiency ---

        static SocialInteractions()
        {
            var harmony = new Harmony("com.gemini.socialinteractions");
            harmony.PatchAll();
            
            // Initialize TTS Manager (reset sequence IDs)
            TTSManager.Initialize();
            
            // Start Player2 health heartbeat if configured
            if (Settings != null && Settings.llmInteractionsEnabled && 
                Settings.llmApiType == LlmApiType.Player2 && 
                !string.IsNullOrEmpty(Settings.player2GameClientId))
            {
                Player2ApiClient.StartHealthHeartbeat(Settings.llmApiUrl, Settings.player2GameClientId);
            }
            
            // Log that patches were applied
            SLog.Message("[SocialInteractions] Harmony patches applied");
        }

        /// <summary>
        /// Gets the custom flavor text for a pawn
        /// </summary>
        /// <param name="pawn">The pawn to get the flavor text for</param>
        /// <returns>The custom flavor text, or an empty string if none exists</returns>
        public static string GetPawnFlavorText(Pawn pawn)
        {
            if (pawn == null)
            {
                return string.Empty;
            }
            
            // Try to get from the game component first, fall back to static dictionary
            PawnFlavorText_GameComponent gameComp = null;
            if (Current.Game != null)
            {
                gameComp = Current.Game.GetComponent<PawnFlavorText_GameComponent>();
            }
            
            if (gameComp != null)
            {
                return gameComp.GetFlavorText(pawn.thingIDNumber);
            }
            else
            {
                // Fallback to static dictionary if game component is not available
                if (PawnFlavorTexts.ContainsKey(pawn.thingIDNumber))
                {
                    return PawnFlavorTexts[pawn.thingIDNumber];
                }
            }
            
            return string.Empty;
        }

        /// <summary>
        /// Sets the custom flavor text for a pawn
        /// </summary>
        /// <param name="pawn">The pawn to set the flavor text for</param>
        /// <param name="flavorText">The flavor text to set</param>
        public static void SetPawnFlavorText(Pawn pawn, string flavorText)
        {
            if (pawn == null)
            {
                return;
            }
            
            // Update the game component if available, otherwise update the static dictionary
            PawnFlavorText_GameComponent gameComp = null;
            if (Current.Game != null)
            {
                gameComp = Current.Game.GetComponent<PawnFlavorText_GameComponent>();
            }
            
            if (gameComp != null)
            {
                gameComp.SetFlavorText(pawn.thingIDNumber, flavorText);
            }
            else
            {
                // Update static dictionary as fallback
                if (string.IsNullOrEmpty(flavorText))
                {
                    // Remove the entry if flavor text is empty
                    PawnFlavorTexts.Remove(pawn.thingIDNumber);
                }
                else
                {
                    PawnFlavorTexts[pawn.thingIDNumber] = flavorText;
                }
            }
        }

        public static bool IsLlmInteractionEnabled(InteractionDef interactionDef)
        {
            //SLog.Message(string.Format("[SocialInteractions] IsLlmInteractionEnabled called for: {0}", interactionDef.defName));
            if (!Settings.llmInteractionsEnabled) return false;

            
            if (interactionDef == InteractionDefOf.Chitchat && Settings.enableChitchat) return true;
            if (interactionDef == InteractionDefOf.DeepTalk && Settings.enableDeepTalk) return true;
            if (interactionDef == InteractionDefOf.Insult && Settings.enableInsult) return true;
            if (interactionDef == InteractionDefOf.RomanceAttempt && Settings.enableRomanceAttempt) return true;
            if (interactionDef == InteractionDefOf.MarriageProposal && Settings.enableMarriageProposal) return true;
            if (interactionDef == InteractionDefOf.Reassure && Settings.enableReassure) return true;
            if (interactionDef == InteractionDefOf.DisturbingChat && Settings.enableDisturbingChat) return true;
            if (interactionDef == InteractionDefOf.ConvertIdeoAttempt && Settings.enableIdeologyConversionInteractions) return true;
            if (interactionDef == SI_InteractionDefOf.TendPatient && Settings.enableTendPatient) return true;
            if (interactionDef == SI_InteractionDefOf.Lovin && Settings.enableLovin) return true;
            if (interactionDef.defName == "GoOnDate" && Settings.enableDating) return true;
            if (interactionDef == SI_InteractionDefOf.DateRejected && Settings.enableDating) return true;
            if (interactionDef == SI_InteractionDefOf.DateAccepted && Settings.enableDating) return true;
            if (interactionDef == SI_InteractionDefOf.DateLovin && Settings.enableDating && Settings.enableLovin) return true;
            if (interactionDef == SI_InteractionDefOf.CaughtCheating && Settings.enableDating) return true;
            if (interactionDef == SI_InteractionDefOf.ManualChat && Settings.enableManualChat) return true;
            if (interactionDef == SI_InteractionDefOf.Badmouthing && Settings.enableDrama) return true;
            if (interactionDef == SI_InteractionDefOf.EnhancedInsult && Settings.enableDrama) return true;
            if (interactionDef == SI_InteractionDefOf.Admiration && Settings.enableDrama) return true;
            if (interactionDef == SI_InteractionDefOf.Backstabbing && Settings.enableDrama) return true;
            if (interactionDef == SI_InteractionDefOf.MakeUp && Settings.enableDrama) return true;
            if (interactionDef == SI_InteractionDefOf.ChildAnnoying && Settings.enableChildrenMisbehavior) return true;
            if (interactionDef == SI_InteractionDefOf.ChildPlayTag && Settings.enableChildrenMisbehavior) return true;
            if (interactionDef.defName == "KindWords" && Settings.enableKindWordsInteractions) return true;
            if (interactionDef.defName == "VRE_FlirtingAttempt" && Settings.enableFlirt) return true;
            if (interactionDef.defName == "Slight" && Settings.enableSlight) return true;
            if (interactionDef.defName == "IncestuousFlirt" && Settings.enableIncestuousFlirt) return true;
            if (interactionDef.defName == "Rapport" && Settings.enableRapport) return true;
            if (interactionDef == InteractionDefOf.RecruitAttempt && Settings.enableRecruitAttempt) return true;
            if (interactionDef.defName == "ReduceResistance" && Settings.enableReduceResistance) return true;
            if (interactionDef == InteractionDefOf.ReduceWill && Settings.enableReduceWill) return true;
            if ((interactionDef.defName == "EnslaveAttempt" || (InteractionDefOf.EnslaveAttempt != null && interactionDef == InteractionDefOf.EnslaveAttempt)) && Settings.enableEnslaveAttempt) return true;
            return false;
        }

        public static bool IsLlmJobEnabled(JobDriver jobDriver)
        {
            if (!Settings.llmInteractionsEnabled) return false;

            if (jobDriver is JobDriver_TendPatient && Settings.enableTendPatient) return true;
            if (jobDriver is JobDriver_VisitSickPawn && Settings.enableVisitSickPawn) return true;
            if (jobDriver is JobDriver_Lovin && Settings.enableLovin) return true;

            return false;
        }

        public static bool IsLlmMarriageCeremonyEnabled()
        {
            return Settings.llmInteractionsEnabled && Settings.enableMarriageCeremony;
        }

        public static bool IsLlmBreakupEnabled()
        {
            return Settings.llmInteractionsEnabled && Settings.enableBreakups && Settings.useLlmForBreakups;
        }

        /// <summary>
        /// Appends API-specific ending to the prompt based on the selected API type.
        /// Text completion APIs get a starter, chat completion APIs get format instructions.
        /// </summary>
        private static string AppendApiSpecificEnding(string prompt, Pawn initiator, Pawn recipient)
        {
            if (string.IsNullOrEmpty(prompt))
                return prompt;

            string pawn1Name = initiator != null ? initiator.LabelShort : "Pawn1";
            string pawn2Name = recipient != null ? recipient.LabelShort : "Pawn2";

            // Determine if using text completion or chat completion API
            bool isLocalApi = Settings.llmApiType == LlmApiType.KoboldCpp || 
                              Settings.llmApiType == LlmApiType.LMStudio || 
                              Settings.llmApiType == LlmApiType.Ollama;
            bool isTextCompletion = isLocalApi && !Settings.forceChatCompletion;

            if (isTextCompletion)
            {
                // Text completion: add starter to begin generation
                prompt += string.Format("\n<start>\n{0}:", pawn1Name);
            }
            else
            {
                // Chat completion: add format instructions
                prompt += string.Format("\n\nFormat the response like this:\r\n{0}: bla bla...\r\n{1}: bla bla...", pawn1Name, pawn2Name);
            }

            return prompt;
        }

        public static string GenerateDeepTalkPrompt(Pawn initiator, Pawn recipient, InteractionDef interactionDef, string subject)
        {
            if (initiator == null || recipient == null)
            {
                return null;
            }
            
            if (interactionDef == null && subject == null)
            {
                return null;
            }

            if (subject == null)
            {
                subject = interactionDef != null ? interactionDef.label : "Custom Interaction"; // Default subject if interactionDef is null
            }

            if (!Settings.llmInteractionsEnabled)
            {
                return null;
            }

            bool isEnabled = false;
            
            if (interactionDef == null)
            {
                // If interactionDef is null, we assume the caller has already checked specific permissions
                // (like for Marriage Ceremony which calls this with null def)
                isEnabled = true;
            }
            else
            {
                isEnabled = IsLlmInteractionEnabled(interactionDef);
            }

            string defName = interactionDef != null ? interactionDef.defName : "Custom";
            SLog.Message(string.Format("[SocialInteractions] GenerateDeepTalkPrompt: isEnabled for {0}: {1}", defName, isEnabled));
            if (!isEnabled)
            {
                return null;
            }

            if (string.IsNullOrEmpty(Settings.llmApiUrl) || string.IsNullOrEmpty(Settings.llmPromptTemplate))
            {
                return null;
            }

            // Placeholder replacement (initial version, will expand later)
            string prompt = Settings.llmPromptTemplate;
            prompt = prompt.Replace("[topic]", "interaction");
            prompt = prompt.Replace("[subject]", subject ?? "");

            // Get relationship
            string relation = GetRelationship(initiator, recipient);
            prompt = prompt.Replace("[relation]", relation);

            // Extract pawn data
            var pawn1Data = ExtractPawnData(initiator, "pawn1", recipient);
            var pawn2Data = ExtractPawnData(recipient, "pawn2", initiator);

            // Replace placeholders for pawn1
            foreach (var kvp in pawn1Data)
            {
                prompt = prompt.Replace("[" + kvp.Key + "]", kvp.Value);
            }
            // Add specific mapping for [initiator_ideo]
            if (pawn1Data.ContainsKey("pawn1_ideology_desc"))
            {
                prompt = prompt.Replace("[initiator_ideo]", pawn1Data["pawn1_ideology_desc"]);
            }

            // Replace placeholders for pawn2
            foreach (var kvp in pawn2Data)
            {
                prompt = prompt.Replace("[" + kvp.Key + "]", kvp.Value);
            }
            // Add specific mapping for [recipient_ideo]
            if (pawn2Data.ContainsKey("pawn2_ideology_desc"))
            {
                prompt = prompt.Replace("[recipient_ideo]", pawn2Data["pawn2_ideology_desc"]);
            }

            // World info attributes
            long absTicks = Find.TickManager.TicksAbs;
            string currentDate = "Unknown";
            string currentTime = "Unknown";
            string currentWeather = "Unknown";

            if (initiator.Map != null)
            {
                float longitude = Find.WorldGrid.LongLatOf(initiator.Tile).x;
                int day = GenDate.DayOfQuadrum(absTicks, longitude);
                Quadrum quadrum = GenDate.Quadrum(absTicks, longitude);
                int year = GenDate.Year(absTicks, longitude);
                currentDate = string.Format("{0} of {1}, {2}", day, quadrum.Label(), year);
                int hour = (int)(GenDate.DayPercent(absTicks, longitude) * 24f);
                currentTime = hour.ToString("D2") + ":00";
                float temperature = initiator.Map.mapTemperature.OutdoorTemp;
                currentWeather = string.Format("{0} ({1}°C)", initiator.Map.weatherManager.curWeather.label, temperature.ToString("F0"));
            }

            // Replace world placeholders
            prompt = prompt.Replace("[date]", currentDate);
            prompt = prompt.Replace("[time]", currentTime);
            prompt = prompt.Replace("[weather]", currentWeather);
            prompt = prompt.Replace("[tile]", GetBiomeInfo(initiator.Map));
            prompt = prompt.Replace("[colony]", GetColonyInfo(initiator.Map));
            prompt = prompt.Replace("[event]", GetLastEventInfo());

            return AppendApiSpecificEnding(prompt, initiator, recipient);
        }

        public static string GenerateMonologuePrompt(Pawn pawn, string subject, string topic = "monologue")
        {
            if (pawn == null)
            {
                return null;
            }
            if (subject == null)
            {
                subject = "Thinking to themselves";
            }

            if (!Settings.llmInteractionsEnabled)
            {
                return null;
            }

            if (string.IsNullOrEmpty(Settings.llmApiUrl) || string.IsNullOrEmpty(Settings.llmMonologuePromptTemplate))
            {
                return null;
            }

            // Placeholder replacement (initial version, will expand later)
            string prompt = Settings.llmMonologuePromptTemplate;
            prompt = prompt.Replace("[topic]", topic);
            prompt = prompt.Replace("[subject]", subject ?? "");

            // Extract pawn data
            var pawn1Data = ExtractPawnData(pawn, "pawn1", null);

            // Replace placeholders for pawn1
            foreach (var kvp in pawn1Data)
            {
                prompt = prompt.Replace("[" + kvp.Key + "]", kvp.Value);
            }

            // World info attributes
            long absTicks = Find.TickManager.TicksAbs;
            string currentDate = "Unknown";
            string currentTime = "Unknown";
            string currentWeather = "Unknown";

            if (pawn.Map != null)
            {
                float longitude = Find.WorldGrid.LongLatOf(pawn.Tile).x;
                int day = GenDate.DayOfQuadrum(absTicks, longitude);
                Quadrum quadrum = GenDate.Quadrum(absTicks, longitude);
                int year = GenDate.Year(absTicks, longitude);
                currentDate = string.Format("{0} of {1}, {2}", day, quadrum.Label(), year);
                int hour = (int)(GenDate.DayPercent(absTicks, longitude) * 24f);
                currentTime = hour.ToString("D2") + ":00";
                float temperature = pawn.Map.mapTemperature.OutdoorTemp;
                currentWeather = string.Format("{0} ({1}°C)", pawn.Map.weatherManager.curWeather.label, temperature.ToString("F0"));
            }

            // Replace world placeholders
            prompt = prompt.Replace("[date]", currentDate);
            prompt = prompt.Replace("[time]", currentTime);
            prompt = prompt.Replace("[weather]", currentWeather);
            prompt = prompt.Replace("[tile]", GetBiomeInfo(pawn.Map));
            prompt = prompt.Replace("[colony]", GetColonyInfo(pawn.Map));
            prompt = prompt.Replace("[event]", GetLastEventInfo());

            // Add API-specific ending for monologue
            string pawnName = pawn != null ? pawn.Name.ToStringShort : "Pawn";
            bool isLocalApi = Settings.llmApiType == LlmApiType.KoboldCpp || 
                              Settings.llmApiType == LlmApiType.LMStudio || 
                              Settings.llmApiType == LlmApiType.Ollama;
            bool isTextCompletion = isLocalApi && !Settings.forceChatCompletion;

            if (isTextCompletion)
            {
                // Text completion: add starter
                prompt += string.Format("\n<start>\n{0}:", pawnName);
            }
            else
            {
                // Chat completion: add format instruction for monologue
                prompt += string.Format("\n\nFormat the response as a brief monologue from {0}'s perspective like this:\r\n{0}: bla bla...\r\n{0}: bla bla...", pawnName);
            }

            return prompt;
        }

        public static string WrapText(string text, int wordsPerLine)
        {
            if (wordsPerLine <= 0) return text; // No wrapping if limit is zero or negative

            string[] words = text.Split(' ');
            System.Text.StringBuilder wrappedText = new System.Text.StringBuilder();
            int wordCount = 0;

            for (int i = 0; i < words.Length; i++)
            {
                wrappedText.Append(words[i]);
                wordCount++;

                if (wordCount >= wordsPerLine && i < words.Length - 1)
                {
                    wrappedText.Append("\n");
                    wordCount = 0;
                }
                else if (i < words.Length - 1)
                {
                    wrappedText.Append(" ");
                }
            }
            return wrappedText.ToString();
        }

        public static float EstimateReadingTime(string text)
        {
            // Simple estimate: words per second from settings.
            int wordCount = text.Split(new string[] { " ", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries).Length;
            float estimatedTime = 0f;
            if (SocialInteractions.Settings.wordsPerSecond <= 0)
            {
                estimatedTime = wordCount * 0.3f; // Fallback if setting is zero or negative
            }
            else
            {
                estimatedTime = wordCount / SocialInteractions.Settings.wordsPerSecond; // Seconds
            }
            return estimatedTime;
        }

        /// <summary>
        /// Generates a formatted description of a pawn for use in prompts.
        /// Includes sex, age, and title information.
        /// </summary>
        /// <param name="pawn">The pawn to describe</param>
        /// <returns>Formatted string with pawn details (e.g., "male, 25 years old, colonist")</returns>
        public static string GetPawnDescription(Pawn pawn)
        {
            if (pawn == null)
            {
                return "unknown";
            }

            // Extract pawn data using the existing helper method
            var pawnData = ExtractPawnData(pawn, "target");
            
            // Get the key information
            string sex = pawnData.ContainsKey("target_sex") ? pawnData["target_sex"].ToLower() : "unknown";
            string ageStr = pawnData.ContainsKey("target_age") ? pawnData["target_age"] : "unknown";
            string title = pawnData.ContainsKey("target_title") ? pawnData["target_title"] : "outsider";
            
            // Parse age to get the main age value
            string age = "unknown age";
            if (ageStr != "Unknown")
            {
                // If it's in format "25 (27)", take the first number
                if (ageStr.Contains(" "))
                {
                    age = ageStr.Split(' ')[0] + " years old";
                }
                else
                {
                    age = ageStr + " years old";
                }
            }
            
            return string.Format("{0}, {1}, {2}", sex, age, title);
        }

        /// <summary>
        /// Extracts pawn data into a dictionary for use in prompt templates.
        /// </summary>
        /// <param name="pawn">The pawn to extract data from.</param>
        /// <param name="prefix">The prefix to use for the dictionary keys (e.g., "pawn1", "pawn2").</param>
        /// <returns>A dictionary containing the pawn's data.</returns>
        public static Dictionary<string, string> ExtractPawnData(Pawn pawn, string prefix, Pawn target = null)
        {
            var data = new Dictionary<string, string>();

            if (pawn == null)
            {
                // Return empty data if pawn is null
                data[prefix] = "Unknown";
                data[prefix + "_age"] = "Unknown";
                data[prefix + "_sex"] = "Unknown";
                data[prefix + "_title"] = "Unknown";
                data[prefix + "_ideology"] = "None";
                data[prefix + "_traits"] = "None";
                data[prefix + "_mood"] = "N/A";
                data[prefix + "_dislikes"] = "None";
                data[prefix + "_afflictions"] = "None";
                data[prefix + "_likes"] = "None";
                data[prefix + "_tech"] = "None";
                data[prefix + "_action"] = "None";
                data[prefix + "_proficiencies"] = "None";
                data[prefix + "_genes"] = "None";
                data[prefix + "_family"] = "None";
                data[prefix + "_journal"] = "No recent conversations";
                return data;
            }

            // Basic pawn info
            data[prefix] = pawn.LabelShort;
            // Format age as "bio_age (real_age)" if they differ, otherwise just the bio age
            int biologicalAge = pawn.ageTracker.AgeBiologicalYears;
            int chronologicalAge = pawn.ageTracker.AgeChronologicalYears;
            if (biologicalAge != chronologicalAge)
            {
                data[prefix + "_age"] = string.Format("{0} ({1})", biologicalAge, chronologicalAge);
            }
            else
            {
                data[prefix + "_age"] = biologicalAge.ToString();
            }
            data[prefix + "_sex"] = pawn.gender.ToString();
            
            // Title (colonist/prisoner/slave/outsider/guest/animal) with optional royalty title
            string title = "outsider"; // Default to outsider
            if (!pawn.RaceProps.Humanlike)
            {
                // More specific categorization for non-humanlike entities
                if (pawn.RaceProps.IsMechanoid)
                {
                    title = "mech";
                }
                else if (pawn.RaceProps.IsAnomalyEntity)
                {
                    // Further categorize anomaly entities
                    // Only check IsGhoul and IsShambler if the Anomaly DLC is active
                    if (ModsConfig.AnomalyActive)
                    {
                        if (pawn.IsGhoul)
                        {
                            title = "ghoul";
                        }
                        else if (pawn.IsShambler)
                        {
                            title = "shambler";
                        }
                        else if (pawn.RaceProps.FleshType == FleshTypeDefOf.EntityFlesh)
                        {
                            title = "entity (flesh)";
                        }
                        else if (pawn.RaceProps.FleshType == FleshTypeDefOf.EntityMechanical)
                        {
                            title = "entity (mechanical)";
                        }
                        else if (pawn.RaceProps.FleshType == FleshTypeDefOf.Fleshbeast)
                        {
                            title = "fleshbeast";
                        }
                        else
                        {
                            title = "entity";
                        }
                    }
                    else
                    {
                        // If Anomaly DLC is not active, but pawn has IsAnomalyEntity flag,
                        // treat it as a generic entity to avoid accessing unavailable properties
                        title = "entity";
                    }
                }
                else if (pawn.RaceProps.IsDrone)
                {
                    title = "drone";
                }
                else if (pawn.RaceProps.Animal)
                {
                    // More specific animal types
                    if (pawn.RaceProps.Insect)
                    {
                        title = "insect";
                    }
                    else if (pawn.RaceProps.Dryad)
                    {
                        title = "dryad";
                    }
                    else
                    {
                        title = "animal (" + pawn.kindDef.race.defName + ")";
                    }
                }
                else
                {
                    title = "animal";
                }
            }
            else if (pawn.IsColonist)
            {
                title = (pawn.story != null && !pawn.story.TitleShort.NullOrEmpty()) ? pawn.story.TitleShort : "colonist";
            }
            else if (pawn.IsPrisonerOfColony)
            {
                title = "prisoner";
            }
            else if (pawn.IsSlaveOfColony)
            {
                title = "slave";
            }
            else if (pawn.guest != null && pawn.guest.GuestStatus == GuestStatus.Guest)
            {
                title = (pawn.story != null && !pawn.story.TitleShort.NullOrEmpty()) ? pawn.story.TitleShort : "guest";
            }
            
            // Append royalty title if the pawn has one
            if (pawn.royalty != null)
            {
                RoyalTitleDef royalTitle = pawn.royalty.MainTitle();
                if (royalTitle != null)
                {
                    title += " (" + royalTitle.GetLabelCapFor(pawn);
                    
                    // If the pawn has a noble rank and belongs to a faction, add the faction name
                    // Check the title's faction first, then fall back to pawn's faction
                    Faction titleFaction = null;
                    List<RoyalTitle> allTitles = pawn.royalty.AllTitlesForReading;
                    if (allTitles != null)
                    {
                        foreach (RoyalTitle rt in allTitles)
                        {
                            if (rt.def == royalTitle && rt.faction != null)
                            {
                                titleFaction = rt.faction;
                                break;
                            }
                        }
                    }
                    
                    // If we couldn't get the title's faction, fall back to pawn's faction
                    if (titleFaction == null && pawn.Faction != null && !pawn.Faction.IsPlayer)
                    {
                        titleFaction = pawn.Faction;
                    }
                    
                    if (titleFaction != null)
                    {
                        // Add more detailed logging to help debug faction name issues
                        string factionName = titleFaction.Name;
                        if (!string.IsNullOrEmpty(factionName))
                        {
                            title += " of " + factionName;
                        }
                        else
                        {
                            // If faction name is empty, use the faction def name as fallback
                            title += " of " + titleFaction.def.label;
                        }
                    }
                    
                    title += ")";
                }
            }
            
            data[prefix + "_title"] = title;

            // Faction
            string faction = "Unknown";
            if (pawn.Faction != null)
            {
               faction = pawn.Faction.Name;
            }
            data[prefix + "_faction"] = faction;
            
            // Ideology
            string ideology = "None";
            string ideologyDesc = "None";
            if (pawn.Ideo != null)
            {
                ideology = pawn.Ideo.name;
                
                // Build detailed description with memes
                List<string> memesList = new List<string>();
                foreach (MemeDef meme in pawn.Ideo.memes)
                {
                    memesList.Add(meme.LabelCap);
                }
                
                if (memesList.Count > 0)
                {
                    ideologyDesc = string.Format("{0} (Memes: {1})", ideology, string.Join(", ", memesList.ToArray()));
                }
                else
                {
                    ideologyDesc = ideology;
                }
            }
            
            data[prefix + "_ideology"] = ideology;
            data[prefix + "_ideology_desc"] = ideologyDesc;

            // Traits
            string traits = "None";
            if (pawn.story != null && pawn.story.traits != null)
            {
                List<string> traitsList = new List<string>();
                foreach (Trait trait in pawn.story.traits.allTraits)
                {
                    traitsList.Add(trait.Label);
                }
                traits = string.Join(", ", traitsList.ToArray());
            }
            data[prefix + "_traits"] = traits;

            // Mood
            string mood = "N/A";
            if (pawn.needs != null && pawn.needs.mood != null)
            {
                float level = pawn.needs.mood.CurLevelPercentage;
                mood = string.Format("{0}% ({1})", (level * 100).ToString("F0"), GetMoodLabel(level));
            }
            data[prefix + "_mood"] = mood;

            // Dislikes, Afflictions, Likes, Tech
            data[prefix + "_dislikes"] = GetDislikes(pawn);
            data[prefix + "_afflictions"] = GetAfflictions(pawn);
            data[prefix + "_likes"] = GetLikes(pawn);
            data[prefix + "_tech"] = GetTech(pawn);

            // Current action/job
            string action = "None";
            if (pawn.jobs != null && pawn.jobs.curJob != null)
            {
                try
                {
                    action = pawn.GetJobReport().CapitalizeFirst();
                }
                catch (Exception)
                {
                    action = "None";
                }
            }
            data[prefix + "_action"] = action;

            // Proficiencies (top skills)
            data[prefix + "_proficiencies"] = GetProficiencies(pawn);
            
            // Cannot do (disabled skills)
            data[prefix + "_noskills"] = GetNoSkills(pawn);

            // Genes/Xenotype
            string genes = "None";
            if (pawn.genes != null)
            {
                try
                {
                    // Access XenotypeLabel safely, as it may not exist in base game without DLCs
                    genes = pawn.genes.XenotypeLabel;
                    List<string> geneList = new List<string>();
                    foreach (Gene gene in pawn.genes.GenesListForReading)
                    {
                        if (gene.def != null && !gene.def.skinColorBase.HasValue && !gene.Overridden)
                        {
                            geneList.Add(gene.def.label);
                        }
                    }
                    if (geneList.Count > 0)
                    {
                        genes += " (" + string.Join(", ", geneList.ToArray()) + ")";
                    }
                }
                catch (NullReferenceException)
                {
                    // In base game without DLCs, genes may exist but not have xenotype info
                    // Set to "None" as fallback
                    genes = "None";
                }
            }
            data[prefix + "_genes"] = genes;

            // Family information
            data[prefix + "_family"] = GetFamily(pawn);

            // Add social log information
            data[prefix + "_journal"] = GetLastSocialLogEntry(pawn, target);
            
            // Add attire information (what they're wearing on chest/body)
            data[prefix + "_attire"] = GetAttire(pawn);
            
            // Add custom flavor text (bio) for the pawn
            data[prefix + "_bio"] = GetPawnFlavorText(pawn);

            // Opinion of target
            data[prefix + "_opinion"] = GetOpinion(pawn, target);

            return data;
        }

        private static string GetOpinion(Pawn pawn, Pawn target)
        {
            if (pawn == null || target == null || pawn == target) return "N/A";
            if (pawn.relations == null) return "N/A";

            int opinion = pawn.relations.OpinionOf(target);
            string rawReasons = pawn.relations.OpinionExplanation(target);
            string reasons = "No specific reasons";

            if (!string.IsNullOrEmpty(rawReasons))
            {
                // Split by common line endings
                string[] lines = rawReasons.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                
                // Skip the first line (header) and process the rest
                List<string> cleanedReasons = new List<string>();
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    // Remove leading "- " if present
                    if (line.StartsWith("- "))
                    {
                        line = line.Substring(2);
                    }
                    else if (line.StartsWith("-"))
                    {
                        line = line.Substring(1);
                    }
                    
                    if (!string.IsNullOrEmpty(line))
                    {
                        cleanedReasons.Add(line);
                    }
                }

                if (cleanedReasons.Count > 0)
                {
                    reasons = string.Join(", ", cleanedReasons.ToArray());
                }
            }

            return string.Format("{0}% ({1})", opinion, reasons);
        }

        private static string GetMoodLabel(float level)
        {
            if (level < 0.15f) return "Deeply Upset";
            if (level < 0.35f) return "Upset";
            if (level < 0.60f) return "Neutral";
            if (level < 0.80f) return "Content";
            return "Happy";
        }

        public static string GetBiomeInfo(Map map)
        {
            if (map == null) return "Unknown";
            return map.Biome.label;
        }

        private static string GetColonyInfo(Map map)
        {
            if (map == null) return "Unknown";

            int colonists = map.mapPawns.FreeColonists.Count;
            int prisoners = map.mapPawns.PrisonersOfColony.Count;
            // Use Spawned property as it exists in the extracted MapPawns class
            int slaves = map.mapPawns.SlavesOfColonySpawned.Count;
            int guests = map.mapPawns.AllPawnsSpawned.Count(p => p.guest != null && p.guest.GuestStatus == GuestStatus.Guest && !p.HostileTo(Faction.OfPlayer) && !p.IsColonist);
            int enemies = map.mapPawns.AllPawnsSpawned.Count(p => p.HostileTo(Faction.OfPlayer) && !p.Downed && !p.IsPrisoner);

            List<string> parts = new List<string>();
            parts.Add(string.Format("{0} colonists", colonists));
            if (prisoners > 0) parts.Add(string.Format("{0} prisoners", prisoners));
            if (slaves > 0) parts.Add(string.Format("{0} slaves", slaves));
            if (guests > 0) parts.Add(string.Format("{0} guests", guests));
            if (enemies > 0) parts.Add(string.Format("{0} enemies", enemies));

            return string.Join(", ", parts.ToArray());
        }

        private static string GetLastEventInfo()
        {
            if (Find.Archive == null) return "No recent events";

            var archivables = Find.Archive.ArchivablesListForReading;
            for (int i = archivables.Count - 1; i >= 0; i--)
            {
                // C# 5 compatible cast
                Letter letter = archivables[i] as Letter;
                if (letter != null && letter.def != null)
                {
                    // Check against known negative letter defs
                    if (letter.def == LetterDefOf.ThreatBig || 
                        letter.def == LetterDefOf.ThreatSmall || 
                        letter.def == LetterDefOf.Death)
                    {
                        int ticksSince = Find.TickManager.TicksAbs - letter.arrivalTick;
                        int daysSince = ticksSince / 60000;
                        return string.Format("The last negative event was {0}, {1} days ago", letter.Label, daysSince);
                    }
                }
            }
            return "No recent negative events";
        }

        private static string GetAttire(Pawn pawn)
        {
            if (pawn == null)
            {
                return "naked";
            }

            // Check if the pawn has the SI_Naked hediff (used during dating activities)
            if (pawn.health != null && pawn.health.hediffSet != null && pawn.health.hediffSet.HasHediff(SI_HediffDefOf.SI_Naked))
            {
                return "naked";
            }

            if (pawn.apparel == null)
            {
                return "naked";
            }

            // Look for apparel worn on the torso/chest area
            // In RimWorld, apparel covers specific body part groups
            var wornApparel = pawn.apparel.WornApparel;
            if (wornApparel == null)
            {
                return "naked";
            }

            // Define the torso body part group (chest area)
            BodyPartGroupDef torsoGroup = BodyPartGroupDefOf.Torso;
            
            // Find the outermost apparel that covers the torso
            Apparel torsoApparel = null;
            foreach (Apparel apparel in wornApparel)
            {
                if (apparel != null && apparel.def != null && apparel.def.apparel != null)
                {
                    // Check if this apparel covers the torso body part group
                    // This ensures we only consider apparel that actually covers the chest/torso area
                    // (e.g., excludes hats, helmets, etc. that only cover head parts)
                    if (apparel.def.apparel.bodyPartGroups.Contains(torsoGroup))
                    {
                        // For layer-based priority, we want to find the outermost visible layer
                        // We'll prioritize outer layers (Shell > Middle > OnSkin)
                        if (torsoApparel == null || 
                            (apparel.def.apparel.layers.Contains(ApparelLayerDefOf.Shell) && 
                             !torsoApparel.def.apparel.layers.Contains(ApparelLayerDefOf.Shell)) ||
                            (apparel.def.apparel.layers.Contains(ApparelLayerDefOf.Middle) && 
                             torsoApparel.def.apparel.layers.Contains(ApparelLayerDefOf.OnSkin)))
                        {
                            torsoApparel = apparel;
                        }
                    }
                }
            }

            // If we found torso-covering apparel, return its label
            if (torsoApparel != null)
            {
                return torsoApparel.Label;
            }
            
            // If no apparel covers the torso/chest area specifically, they are naked on the upper body
            return "naked";
        }

        private static string GetLastSocialLogEntry(Pawn pawn, Pawn target = null)
        {
            try
            {
                if (Find.PlayLog == null || pawn == null)
                {
                    return "No recent conversations";
                }

                // Get the play log entries
                var entries = Find.PlayLog.AllEntries;
                if (entries == null)
                {
                    return "No recent conversations";
                }

                // Get the current game tick to filter out very recent entries
                int currentTick = Find.TickManager.TicksGame;
                // Only consider entries that are at least 1 hour old (1800 ticks = 1 hour in RimWorld)
                int minAgeTicks = 1800;

                // Look for the most recent interaction entry involving this pawn
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    var entry = entries[i];
                    if (entry == null) continue;

                    // Check if this is a PlayLogEntry_Interaction
                    if (entry.GetType().Name == "PlayLogEntry_Interaction")
                    {
                        // Check if this entry is old enough (not the current interaction)
                        var tickField = entry.GetType().GetField("tick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (tickField != null)
                        {
                            int entryTick = (int)tickField.GetValue(entry);
                            // Skip entries that are too recent
                            if (currentTick - entryTick < minAgeTicks)
                            {
                                continue;
                            }
                        }

                        // For monologues (no target specified), return the last entry involving this pawn
                        if (target == null)
                        {
                            // Check if this entry concerns our pawn
                            var concernsMethod = entry.GetType().GetMethod("Concerns", new Type[] { typeof(Pawn) });
                            if (concernsMethod == null) continue;

                            bool concernsPawn = (bool)concernsMethod.Invoke(entry, new object[] { pawn });
                            if (!concernsPawn) continue;

                            // Get the text representation of the entry and clean up rich text formatting
                            string entryText = entry.ToGameStringFromPOV(pawn);
                            if (!string.IsNullOrEmpty(entryText))
                            {
                                return RemoveRichTextTags(entryText);
                            }
                        }
                        // For conversations (target specified), look for the most recent entry involving both pawns
                        else
                        {
                            // Check if this entry concerns BOTH pawns (regardless of who initiated it)
                            var concernsMethod = entry.GetType().GetMethod("Concerns", new Type[] { typeof(Pawn) });
                            if (concernsMethod == null) continue;

                            bool concernsPawn = (bool)concernsMethod.Invoke(entry, new object[] { pawn });
                            bool concernsTarget = (bool)concernsMethod.Invoke(entry, new object[] { target });
                            
                            // Only return entries that involve BOTH pawns
                            if (concernsPawn && concernsTarget)
                            {
                                // Get the text representation of the entry and clean up rich text formatting
                                string entryText = entry.ToGameStringFromPOV(pawn);
                                if (!string.IsNullOrEmpty(entryText))
                                {
                                    return RemoveRichTextTags(entryText);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] GetLastSocialLogEntry: Exception while getting social log for {0}: {1}", 
                    pawn != null ? pawn.LabelShort : "null", ex.Message));
            }

            return "No recent conversations";
        }

        public static string GetRelationship(Pawn initiator, Pawn recipient)
        {
            // Check for the most important direct relationships first
            if (initiator.relations == null || recipient == null) return "Acquaintance";
            if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Spouse, recipient)) return "Spouse";
            if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Lover, recipient)) return "Lover";
            if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Fiance, recipient)) return "Fiance";

            // Check for family relationships - Convert to List to avoid enumeration issues
            // The IEnumerable from GetRelations can throw if the underlying collection is modified during enumeration.
            try
            {
                var relationsList = initiator.GetRelations(recipient).ToList(); // Force enumeration here
                PawnRelationDef relationDef = relationsList.FirstOrDefault();
                if (relationDef != null) 
                {
                    // Use gender-specific label if available
                    string genderedLabel = relationDef.GetGenderSpecificLabel(recipient);
                    if (!string.IsNullOrEmpty(genderedLabel))
                    {
                        return genderedLabel;
                    }
                    return relationDef.label;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(string.Format("[SocialInteractions] GetRelationship: Exception while getting relations list for {0} and {1}: {2}", initiator.LabelShort, recipient.LabelShort, ex.Message));
                // Fall through to other checks
            }

            // Check for bond
            if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Bond, recipient)) return "Bonded";

            // Fallback to opinion-based relationship
            if (recipient.relations != null)
            {
                int opinion = recipient.relations.OpinionOf(initiator);
                if (opinion >= 20) return "Friend";
                if (opinion <= -20) return "Rival";
            }

            return "Acquaintance";
        }

        private static string GetDislikes(Pawn pawn)
        {
            if (pawn.needs == null || pawn.needs.mood == null)
            {
                return "None";
            }

            List<Thought> thoughts = new List<Thought>();
            pawn.needs.mood.thoughts.GetDistinctMoodThoughtGroups(thoughts);

            // Get all negative thoughts with their absolute mood offset as weight
            var negativeThoughtsList = new List<Thought>(thoughts).Where(t =>
            {
                try
                {
                    return t != null && t.MoodOffset() < 0;
                }
                catch (Exception) { }
                return false;
            }).ToList();

            if (!negativeThoughtsList.Any())
            {
                return "None";
            }

            // Use weighted random selection to pick up to 3 thoughts
            var selectedThoughts = SelectWeightedRandom(negativeThoughtsList, 3, t => Math.Abs(t.MoodOffset()));

            if (selectedThoughts.Any())
            {
                var thoughtLabels = selectedThoughts.Select(t => t.LabelCap);
                return string.Join(", ", thoughtLabels.ToArray());
            }

            return "None";
        }

        private static string GetAfflictions(Pawn pawn)
        {
            if (pawn.health == null || pawn.health.hediffSet == null)
            {
                return "None";
            }

            List<string> afflictionsList = new List<string>();

            // 1. Get missing external parts (not replaced by prosthetics)
            var missingParts = pawn.health.hediffSet.GetMissingPartsCommonAncestors()
                .Where(mp => mp.Part != null && mp.Part.depth == BodyPartDepth.Outside)
                .Select(mp => mp.Part.LabelCap + " " + mp.Label);
            
            afflictionsList.AddRange(missingParts);

            // 2. Get significant bad hediffs (excluding implants and missing parts which are handled above)
            var significantHediffs = pawn.health.hediffSet.hediffs
                .Where(h => h.Visible && h.def.defName != "OnDate" && h.def.defName != "ImplantedIUD" && !(h is Hediff_MissingPart) && !(h is Hediff_Implant) && (h.def.isBad || h.def.makesSickThought))
                .OrderByDescending(h => h.Severity)
                .Select(h => (h.Part != null ? h.Part.LabelCap + " " : "") + h.LabelCap);

            afflictionsList.AddRange(significantHediffs);

            // 3. Check for pregnancy separately since it's not considered a "bad" hediff
            Hediff pregnancyHediff = null;
            if (ModsConfig.BiotechActive)
            {
                pregnancyHediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PregnantHuman);
            }
            else
            {
                pregnancyHediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Pregnant);
            }

            if (pregnancyHediff != null)
            {
                // Add pregnancy information with trimester if available
                string pregnancyInfo = "pregnant";
                Hediff_Pregnant pregnant = pregnancyHediff as Hediff_Pregnant;
                if (pregnant != null)
                {
                    switch (pregnant.CurStageIndex)
                    {
                        case 0:
                            pregnancyInfo = "pregnant (1st trimester)";
                            break;
                        case 1:
                            pregnancyInfo = "pregnant (2nd trimester)";
                            break;
                        case 2:
                            pregnancyInfo = "pregnant (3rd trimester)";
                            break;
                    }
                }
                afflictionsList.Add(pregnancyInfo);
            }

            // Limit to 3 most significant afflictions, removing duplicates if any (though unlikely with this logic)
            var finalAfflictions = afflictionsList.Distinct().Take(3);

            if (finalAfflictions.Any())
            {
                return string.Join(", ", finalAfflictions.ToArray());
            }

            return "None";
        }

        private static string GetTech(Pawn pawn)
        {
            if (pawn.health == null || pawn.health.hediffSet == null)
            {
                return "None";
            }

            var techHediffs = pawn.health.hediffSet.hediffs
                .Where(h => h.Visible && h is Hediff_Implant)
                .OrderByDescending(h => h.Severity)
                .Take(3)
                .Select(h => h.LabelCap);

            if (techHediffs.Any())
            {
                return string.Join(", ", techHediffs.ToArray());
            }

            return "None";
        }

        private static string GetLikes(Pawn pawn)
        {
            if (pawn.needs == null || pawn.needs.mood == null)
            {
                return "None";
            }

            List<Thought> thoughts = new List<Thought>();
            pawn.needs.mood.thoughts.GetDistinctMoodThoughtGroups(thoughts);

            // Get all positive thoughts with their mood offset as weight
            var positiveThoughtsList = new List<Thought>(thoughts).Where(t =>
            {
                try
                {
                    return t != null && t.MoodOffset() > 0;
                }
                catch (Exception) { }
                return false;
            }).ToList();

            if (!positiveThoughtsList.Any())
            {
                return "None";
            }

            // Use weighted random selection to pick up to 3 thoughts
            var selectedThoughts = SelectWeightedRandom(positiveThoughtsList, 3, t => t.MoodOffset());

            if (selectedThoughts.Any())
            {
                var thoughtLabels = selectedThoughts.Select(t => t.LabelCap);
                return string.Join(", ", thoughtLabels.ToArray());
            }

            return "None";
        }

        private static string GetProficiencies(Pawn pawn)
        {
            if (pawn.skills == null)
            {
                return "None";
            }

            var topSkills = pawn.skills.skills.OrderByDescending(s => s.Level).Take(3);
            List<string> skillLabels = new List<string>();
            foreach (var skill in topSkills)
            {
                skillLabels.Add(skill.def.LabelCap);
            }
            return string.Join(", ", skillLabels);
        }

        private static string GetNoSkills(Pawn pawn)
        {
            if (pawn.skills == null)
            {
                return "None";
            }

            var disabledSkills = pawn.skills.skills.Where(s => s.TotallyDisabled).Select(s => s.def.LabelCap);
            if (disabledSkills.Any())
            {
                return string.Join(", ", disabledSkills.ToArray());
            }
            return "None";
        }

        public static void HandleInteraction(Pawn initiator, Pawn recipient, InteractionDef interactionDef, string defaultText)
        {
            // If pawns stop on interaction, let the job-based system handle it
            if (Settings.pawnsStopOnInteraction && 
                (interactionDef == InteractionDefOf.Chitchat || 
                 interactionDef == InteractionDefOf.DeepTalk || 
                 interactionDef == InteractionDefOf.Insult || 
                 interactionDef == InteractionDefOf.RomanceAttempt || 
                 interactionDef == InteractionDefOf.MarriageProposal || 
                 interactionDef == InteractionDefOf.Reassure || 
                 interactionDef == InteractionDefOf.DisturbingChat))
            {
                // For these interactions, when pawnsStopOnInteraction is true, 
                // the InteractionWorker_Interacted_Patch will create jobs to keep pawns in place.
                // We don't need to do anything here.
                return;
            }
            
            if (IsLlmInteractionEnabled(interactionDef))
            {
                HandleNonStoppingInteraction(initiator, recipient, interactionDef, defaultText);
            }
            else
            {
                if (!string.IsNullOrEmpty(defaultText))
                {
                    SpeechBubbleManager.ShowDefaultBubble(initiator, defaultText);
                }
            }
        }

        public static void HandleNonStoppingInteraction(Pawn initiator, Pawn recipient, InteractionDef interactionDef, string subject)
        {
            HandleNonStoppingInteraction(initiator, recipient, interactionDef, subject, false);
        }

        public static int HandleCaughtCheatingInteraction(Pawn initiator, Pawn recipient, Pawn partner = null)
        {
            // Generate a descriptive subject line for the LLM
            // If partner is provided, use it. Otherwise, look it up.
            string subject;
            if (partner != null)
            {
                subject = string.Format("{0} caught {1} cheating with {2}", 
                    initiator.LabelShort, recipient.LabelShort, partner.LabelShort);
            }
            else
            {
                Pawn foundPartner = DatingManager.GetPartnerOfDateWith(recipient);
                if (foundPartner != null)
                {
                    subject = string.Format("{0} caught {1} cheating with {2}", 
                        initiator.LabelShort, recipient.LabelShort, foundPartner.LabelShort);
                }
                else
                {
                    subject = string.Format("{0} caught {1} cheating", 
                        initiator.LabelShort, recipient.LabelShort);
                }
            }
                
            // Trigger the LLM interaction and return the conversation ID
            int conversationId = HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.CaughtCheating, subject, true, true);
            
            // Store the conversation ID for this cheating interaction
            lastCheatingInteractionConversationId = conversationId;
            
            return conversationId;
        }

        public static int HandleThreewayLovinInteraction(Pawn spouse, Pawn cheater, Pawn partner)
        {
            // Generate a descriptive subject line for the LLM
            string subject = string.Format("{0} caught {1} cheating with {2}, but instead of getting mad they join in a 3p lovin' session", 
                spouse.LabelShort, cheater.LabelShort, partner.LabelShort);
                
            // Trigger the LLM interaction and return the conversation ID
            // We'll use the DateLovin interaction def for this
            // Only if lovin interactions are enabled in settings
            int conversationId = -1;
            if (Settings.enableLovin)
            {
                conversationId = HandleNonStoppingInteraction(spouse, cheater, SI_InteractionDefOf.DateLovin, subject, true, true);
            }
            
            return conversationId;
        }

        public static int HandleAbusiveThreesomePrompt(Pawn initiator, Pawn partner, Pawn victim)
        {
            // Generate a descriptive subject line for the LLM
            string subject = string.Format("After bullying {2}, {0} and {1} decide to have some 'fun' with {2}", 
                initiator.LabelShort, partner.LabelShort, victim.LabelShort);
                
            // Trigger the LLM interaction and return the conversation ID
            // We'll try to use DateLovin first if available, otherwise fallback to generic DeepTalk
            InteractionDef def = SI_InteractionDefOf.DateLovin;
            if (!IsLlmInteractionEnabled(def))
            {
                def = InteractionDefOf.DeepTalk;
            }
            
            int conversationId = HandleNonStoppingInteraction(initiator, partner, def, subject, true, true);
            
            return conversationId;
        }

        public static int HandleSoloPesterPrompt(Pawn abuser, Pawn victim)
        {
            // Generate a descriptive subject line for the LLM
            string subject = string.Format("{0} has met {1} and begins to mockingly pester and insult them.",
                abuser.LabelShort, victim.LabelShort);

            // Trigger the LLM interaction and return the conversation ID
            // We'll use Insult as the interaction definition
            InteractionDef def = InteractionDefOf.Insult;
            if (!IsLlmInteractionEnabled(def))
            {
                def = InteractionDefOf.DeepTalk;
            }

            int conversationId = HandleNonStoppingInteraction(abuser, victim, def, subject, true, true);

            return conversationId;
        }

        public static int HandleMonologue(Pawn pawn, string subject, bool skipSpamProtection = false, string topic = "monologue")
        {
            bool isCurrentlyBusy = SpeechBubbleManager.IsLlmCurrentlyBusy();
            SLog.Message(string.Format("[SocialInteractions] HandleMonologue called for: {0}. preventSpam: {1}, isLlmBusy: {2}, skipSpamProtection: {3}, topic: {4}", pawn.LabelShort, Settings.preventSpam, isCurrentlyBusy, skipSpamProtection, topic));
            if (!skipSpamProtection && Settings.preventSpam && isCurrentlyBusy)
            {
                // Show default bubble when LLM is busy and we're preventing spam
                if (!string.IsNullOrEmpty(subject))
                {
                    SpeechBubbleManager.ShowDefaultBubble(pawn, subject);
                }
                return -1; // Return -1 to indicate no conversation was started
            }

            string prompt = GenerateMonologuePrompt(pawn, subject, topic);

            // If we can't generate a prompt, show a default bubble and return
            if (string.IsNullOrEmpty(prompt))
            {
                SLog.Message(string.Format("[SocialInteractions] HandleMonologue: No prompt generated for pawn {0}, showing default bubble", pawn.LabelShort));
                if (!string.IsNullOrEmpty(subject))
                {
                    SpeechBubbleManager.ShowDefaultBubble(pawn, subject);
                }
                return -1; // Return -1 to indicate no conversation was started
            }

            // SLog.Message(string.Format("[SocialInteractions] HandleMonologue: Prompt generated for pawn {0}", pawn.LabelShort));

            // Start the conversation immediately to get the ID
            // This will make IsLlmCurrentlyBusy() return true, blocking subsequent requests
            int conversationId = SpeechBubbleManager.StartConversation();
            // SLog.Message(string.Format("[SocialInteractions] Started conversation ID: {0} for monologue by {1}", conversationId, pawn.LabelShort));

            Task.Run(async () => {
                // --- For LLM Efficiency Timing ---
                DateTime startTime = DateTime.UtcNow;
                // --- End For LLM Efficiency Timing ---
                try
                {
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        string llmResponse = await GenerateTextWithApiClient(prompt);

                        // --- For LLM Efficiency Timing ---
                        DateTime endTime = DateTime.UtcNow;
                        TimeSpan responseTime = endTime - startTime;
                        float responseSeconds = (float)responseTime.TotalSeconds;
                        lastResponseTimeSeconds = responseSeconds;
                        // Log on main thread
                        SpeechBubbleManager.EnqueueJob(() => {
                            // SLog.Message(string.Format("[SocialInteractions] LLM Response time for monologue by {0}: {1:F2}s", pawn.LabelShort, responseSeconds));
                        });
                        // --- End For LLM Efficiency Timing ---

                        if (llmResponse == null)
                        {
                            Log.Warning(string.Format("[SocialInteractions] HandleMonologue: LLM API returned null response for pawn {0}", pawn.LabelShort));
                            // Fallback to default monologue text
                            string fallbackText = string.IsNullOrEmpty(subject)
                                ? string.Format("{0} thinks to themselves.", pawn.LabelShort)
                                : string.Format("{0} ponders about {1}", pawn.LabelShort, subject);
                            string ttsFallbackText = string.IsNullOrEmpty(subject) ? "Thinking to themselves." : string.Format("Ponders about {0}", subject);
                            SpeechBubbleManager.EnqueueJob(() => SpeechBubbleManager.EnqueueMonologue(pawn, fallbackText, 2f, true, conversationId, null, false, subject, ttsFallbackText)); // Use monologue method
                            return;
                        }

                        if (!string.IsNullOrEmpty(llmResponse))
                        {
                            // Split the response using multiple possible line break characters
                            string[] messages = llmResponse.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Take(Settings.llmMaxDialogueLines)
                                .ToArray();
                            if (messages.Any())
                            {
                                // --- For LLM Efficiency Timing ---
                                float totalDisplaySeconds = 0f;
                                // --- End For LLM Efficiency Timing ---

                                for (int i = 0; i < messages.Length; i++)
                                {
                                    string rawMessage = messages[i].Trim();

                                    if (!string.IsNullOrWhiteSpace(rawMessage))
                                    {
                                        // Format the message for a monologue
                                        string formattedMessage = SpeechBubbleManager.FormatMonologueMessage(rawMessage, pawn, true);
                                        string wrappedMessage = SocialInteractions.WrapText(formattedMessage, SocialInteractions.Settings.wordsPerLineLimit);
                                        
                                        // Extract clean text for TTS (removing potential pawn name prefix)
                                        string ttsText = rawMessage;
                                        if (rawMessage.StartsWith(pawn.LabelShort + ":", StringComparison.OrdinalIgnoreCase))
                                        {
                                            ttsText = rawMessage.Substring(pawn.Name.ToStringShort.Length + 1).Trim();
                                        }

                                        float duration = EstimateReadingTime(rawMessage);
                                        // --- For LLM Efficiency Timing ---
                                        totalDisplaySeconds += duration;
                                        // --- End For LLM Efficiency Timing ---
                                        // --- Pass conversationId ---
                                        // Capture the variables to avoid closure issues
                                        int currentIndex = i;
                                        string capturedSubject = subject; // Capture the subject for the monologue
                                        string capturedTtsText = ttsText; // Capture the clean TTS text
                                        SpeechBubbleManager.EnqueueJob(() => SpeechBubbleManager.EnqueueMonologue(pawn, wrappedMessage, duration, currentIndex == 0, conversationId, null, true, capturedSubject, capturedTtsText)); // Orange for high priority
                                        // --- End Pass conversationId ---
                                    }
                                }

                                // With ScheduleUnlock removed, the isLlmBusy flag will be managed by the queue state
                                // No need to calculate or schedule unlocks anymore
                                // --- End For LLM Efficiency Unlock ---
                            }
                            else
                            {
                                SLog.Warning(string.Format("[SocialInteractions] HandleMonologue: LLM API returned empty messages for pawn {0}", pawn.LabelShort));
                                // Fallback to default monologue text
                                string fallbackText = string.IsNullOrEmpty(subject)
                                    ? string.Format("{0} thinks to themselves.", pawn.Name.ToStringShort)
                                    : string.Format("{0} ponders about {1}", pawn.Name.ToStringShort, subject);
                                string wrappedFallbackText = SocialInteractions.WrapText(fallbackText, SocialInteractions.Settings.wordsPerLineLimit);
                                string ttsFallbackText = string.IsNullOrEmpty(subject) ? "Thinking to themselves." : string.Format("Ponders about {0}", subject);
                                SpeechBubbleManager.EnqueueJob(() => SpeechBubbleManager.EnqueueMonologue(pawn, wrappedFallbackText, 2f, true, conversationId, null, false, subject, ttsFallbackText)); // Use monologue method

                                // With ScheduleUnlock removed, the isLlmBusy flag will be managed by the queue state
                                // No need to schedule unlocks anymore
                                // --- End For LLM Efficiency Unlock (Fallback) ---
                            }
                        }
                    }
                    else
                    {
                        Log.Warning(string.Format("[SocialInteractions] HandleMonologue: Failed to generate prompt for pawn {0}", pawn.LabelShort));
                        // Fallback to default monologue text
                        string fallbackText = string.IsNullOrEmpty(subject)
                            ? string.Format("{0} thinks to themselves.", pawn.Name.ToStringShort)
                            : string.Format("{0} ponders about {1}", pawn.Name.ToStringShort, subject);
                        string wrappedFallbackText = SocialInteractions.WrapText(fallbackText, SocialInteractions.Settings.wordsPerLineLimit);

                        // With ScheduleUnlock removed, the isLlmBusy flag will be managed by the queue state
                        // No need to schedule unlocks anymore
                        // --- End For LLM Efficiency Unlock (Prompt Fail) ---
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("Error in HandleMonologue: {0} {1}", ex.Message, ex.StackTrace));
                    // Fallback to default monologue text
                    try
                    {
                        string fallbackText = string.IsNullOrEmpty(subject)
                            ? string.Format("{0} thinks to themselves.", pawn.Name.ToStringShort)
                            : string.Format("{0} ponders about {1}", pawn.Name.ToStringShort, subject);
                        string wrappedFallbackText = SocialInteractions.WrapText(fallbackText, SocialInteractions.Settings.wordsPerLineLimit);
                    }
                    catch (Exception fallbackEx)
                    {
                        Log.Error(string.Format("Error in HandleMonologue fallback: {0} {1}", fallbackEx.Message, fallbackEx.StackTrace));
                    }
                }
                finally
                {
                    // --- End Conversation ---
                    if (conversationId != -1)
                    {
                        // SLog.Message(string.Format("[SocialInteractions] Ending conversation ID: {0} for monologue by {1}", conversationId, pawn.LabelShort));
                        SpeechBubbleManager.EndConversation(conversationId);
                    }
                }
            });

            // Return the conversation ID
            return conversationId;
        }

        public static int HandleNonStoppingInteraction(Pawn initiator, Pawn recipient, InteractionDef interactionDef, string subject, bool skipSpamProtection = false, bool clearQueueOnResponse = false)
        {
            bool isCurrentlyBusy = SpeechBubbleManager.IsLlmCurrentlyBusy();
            string defName = interactionDef != null ? interactionDef.defName : "Custom";
            SLog.Message(string.Format("[SocialInteractions] HandleNonStoppingInteraction called for: {0}. preventSpam: {1}, isLlmBusy: {2}, skipSpamProtection: {3}, clearQueueOnResponse: {4}", defName, Settings.preventSpam, isCurrentlyBusy, skipSpamProtection, clearQueueOnResponse));
            if (!skipSpamProtection && Settings.preventSpam && isCurrentlyBusy)
            {
                // Show default bubble when LLM is busy and we're preventing spam
                if (!string.IsNullOrEmpty(subject))
                {
                    SpeechBubbleManager.ShowDefaultBubble(initiator, subject);
                }
                return -1; // Return -1 to indicate no conversation was started
            }


            string prompt = GenerateDeepTalkPrompt(initiator, recipient, interactionDef, subject);

            // If we can't generate a prompt, show a default bubble and return
            if (string.IsNullOrEmpty(prompt))
            {
                SLog.Message(string.Format("[SocialInteractions] HandleNonStoppingInteraction: No prompt generated for interaction {0}, showing default bubble", defName));
                if (!string.IsNullOrEmpty(subject))
                {
                    SpeechBubbleManager.ShowDefaultBubble(initiator, subject);
                    // Don't add to chat log when there's no actual LLM interaction - only show default bubble
                }
                return -1; // Return -1 to indicate no conversation was started
            }

            // SLog.Message(string.Format("[SocialInteractions] HandleNonStoppingInteraction: Prompt generated for interaction {0}", defName));

            // Start the conversation immediately to get the ID
            int conversationId = SpeechBubbleManager.StartConversation();
            // SLog.Message(string.Format("[SocialInteractions] Started conversation ID: {0} for interaction {1}", conversationId, defName));

            Task.Run(async () => {
                // --- For LLM Efficiency Timing ---
                DateTime startTime = DateTime.UtcNow;
                // --- End For LLM Efficiency Timing ---
                try
                {
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        string llmResponse = await GenerateTextWithApiClient(prompt);

                        // --- For LLM Efficiency Timing ---
                        DateTime endTime = DateTime.UtcNow;
                        TimeSpan responseTime = endTime - startTime;
                        float responseSeconds = (float)responseTime.TotalSeconds;
                        lastResponseTimeSeconds = responseSeconds;
                        // Log on main thread
                        SpeechBubbleManager.EnqueueJob(() => {
                            // SLog.Message(string.Format("[SocialInteractions] LLM Response time for interaction {0}: {1:F2}s", interactionDef.defName, responseSeconds));
                        });
                        // --- End For LLM Efficiency Timing ---

                        if (llmResponse == null)
                        {
                            Log.Warning(string.Format("[SocialInteractions] HandleNonStoppingInteraction: LLM API returned null response for interaction {0}", interactionDef.defName));
                            // Fallback to default interaction text
                            string fallbackText = string.Format("{0} talks with {1}.", initiator.Name.ToStringShort, recipient.Name.ToStringShort);
                            SpeechBubbleManager.EnqueueJob(() => SpeechBubbleManager.Enqueue(initiator, fallbackText, 2f, true, conversationId, null, false)); // Use standard mote for fallback
                            return;
                        }

                        // --- Clear queue for high-priority response ---
                        // If this interaction requested to clear the queue upon receiving a response,
                        // and the response is valid, do so before processing the messages.
                        // This ensures the interruption happens precisely when the high-impact content is ready.
                        if (clearQueueOnResponse)
                        {
                            SpeechBubbleManager.EnqueueJob(() => {
                                SLog.Message(string.Format("[SocialInteractions] Clearing speech queue for high-priority response from interaction: {0}", (interactionDef != null) ? interactionDef.defName : "Unknown"));
                                SpeechBubbleManager.ClearQueues();
                            });
                        }
                        // --- End Clear queue for high-priority response ---

                        if (!string.IsNullOrEmpty(llmResponse))
                        {
                            // Split the response using multiple possible line break characters
                            string[] messages = llmResponse.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Take(Settings.llmMaxDialogueLines)
                                .ToArray();
                            if (messages.Any())
                            {
                                // --- For LLM Efficiency Timing ---
                                float totalDisplaySeconds = 0f;
                                // --- End For LLM Efficiency Timing ---

                                for (int i = 0; i < messages.Length; i++)
                                {
                                    string rawMessage = messages[i].Trim();
                                    Pawn speaker = null;

                                    // Determine speaker and extract dialogue
                                    // More robust speaker detection
                                    if (rawMessage.StartsWith(initiator.Name.ToStringShort + ":", StringComparison.OrdinalIgnoreCase))
                                    {
                                        speaker = initiator;
                                        rawMessage = rawMessage.Substring(initiator.Name.ToStringShort.Length + 1).Trim();
                                    }
                                    else if (rawMessage.StartsWith(recipient.Name.ToStringShort + ":", StringComparison.OrdinalIgnoreCase))
                                    {
                                        speaker = recipient;
                                        rawMessage = rawMessage.Substring(recipient.Name.ToStringShort.Length + 1).Trim();
                                    }
                                    else
                                    {
                                        speaker = initiator; // Default to initiator if speaker not specified
                                    }

                                    if (!string.IsNullOrWhiteSpace(rawMessage) && speaker != null)
                                    {
                                        float duration = EstimateReadingTime(rawMessage);
                                        // --- For LLM Efficiency Timing ---
                                        totalDisplaySeconds += duration;
                                        // --- End For LLM Efficiency Timing ---
                                        // --- Pass conversationId ---
                                        // Capture the loop variable to avoid closure issues
                                        int currentIndex = i;
                                        SpeechBubbleManager.EnqueueJob(() => SpeechBubbleManager.Enqueue(speaker, rawMessage, recipient, duration, currentIndex == 0, conversationId, true, true, subject, interactionDef)); // Orange for high priority, pass subject as fallback text and interactionDef for proper chat log coloring
                                        // --- End Pass conversationId ---
                                    }
                                }

                                // --- For LLM Efficiency Unlock ---
                                // Calculate unlock delay based on last response time estimate and current display time
								// With ScheduleUnlock removed, the isLlmBusy flag will be managed by the queue state
                                // No need to calculate or log unlock delays anymore
                                // Log on main thread
                                SpeechBubbleManager.EnqueueJob(() => {
                                    // SLog.Message(string.Format("[SocialInteractions] Total Display Time: {0:F2}s, Estimated Next Response Time: {1:F2}s", totalDisplaySeconds, lastResponseTimeSeconds));
                                });

                                // With ScheduleUnlock removed, the isLlmBusy flag will be managed by the queue state
                                // No need to schedule unlocks anymore
                                // --- End For LLM Efficiency Unlock ---
                            }
                            else
                            {
                                SLog.Warning(string.Format("[SocialInteractions] HandleNonStoppingInteraction: LLM API returned empty messages for interaction {0}", interactionDef.defName));
                                // Fallback to default interaction text
                                string fallbackText = string.Format("{0} talks with {1}.", initiator.Name.ToStringShort, recipient.Name.ToStringShort);
                                SpeechBubbleManager.EnqueueJob(() => SpeechBubbleManager.Enqueue(initiator, fallbackText, 2f, true, conversationId, null, false)); // Use standard mote for fallback

                                // With ScheduleUnlock removed, the isLlmBusy flag will be managed by the queue state
                                // No need to schedule unlocks anymore
                                // --- End For LLM Efficiency Unlock (Fallback) ---
                            }
                        }
                    }
                    else
                    {
                        Log.Warning(string.Format("[SocialInteractions] HandleNonStoppingInteraction: Failed to generate prompt for interaction {0}", interactionDef.defName));
                        // Fallback to default interaction text
                        string fallbackText = string.Format("{0} talks with {1}.", initiator.Name.ToStringShort, recipient.Name.ToStringShort);
                        SpeechBubbleManager.EnqueueJob(() => SpeechBubbleManager.Enqueue(initiator, fallbackText, 2f, true, conversationId, null, false)); // Use standard mote for fallback

                        // With ScheduleUnlock removed, the isLlmBusy flag will be managed by the queue state
                        // No need to schedule unlocks anymore
                        // --- End For LLM Efficiency Unlock (Prompt Fail) ---
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("Error in HandleNonStoppingInteraction: {0} {1}", ex.Message, ex.StackTrace));
                    // Fallback to default interaction text
                    try
                    {
                        string fallbackText = string.Format("{0} talks with {1}.", initiator.Name.ToStringShort, recipient.Name.ToStringShort);
                        SpeechBubbleManager.EnqueueJob(() => SpeechBubbleManager.Enqueue(initiator, fallbackText, 2f, true, conversationId));

                        // With ScheduleUnlock removed, the isLlmBusy flag will be managed by the queue state
                        // No need to schedule unlocks anymore
                        // --- End For LLM Efficiency Unlock (Exception) ---
                    }
                    catch (Exception fallbackEx)
                    {
                        Log.Error(string.Format("Error in HandleNonStoppingInteraction fallback: {0} {1}", fallbackEx.Message, fallbackEx.StackTrace));
                    }
                }
                finally
                {
                    // --- End Conversation ---
                    if (conversationId != -1)
                    {
                        string interactionDefName = (interactionDef != null) ? interactionDef.defName : "Unknown";

                        // Special handling for CaughtCheating interaction
                        // Let the JobDriver_CaughtCheating handle ending the conversation
                        // since it needs to coordinate with the BeTalkedTo job
                        if (interactionDef == SI_InteractionDefOf.CaughtCheating)
                        {
                            SLog.Message(string.Format("[SocialInteractions] Not ending conversation ID: {0} for interaction {1} - will be handled by JobDriver_CaughtCheating", conversationId, interactionDefName));
                        }
                        else
                        {
                            // SLog.Message(string.Format("[SocialInteractions] Ending conversation ID: {0} for interaction {1}", conversationId, interactionDefName));
                            SpeechBubbleManager.EndConversation(conversationId);
                        }

                        // Note: isLlmBusy will be handled by the scheduled unlock or immediately if delay <= 0
                    }
                }
            });

            // Return the conversation ID
            return conversationId;
        }

        public static void HandleJobGiverInteraction(Pawn initiator, Pawn recipient, InteractionDef interactionDef, string subject)
        {
            // Always show a default bubble immediately
            SpeechBubbleManager.ShowDefaultBubble(initiator, interactionDef.label);

            if (Settings.preventSpam && SpeechBubbleManager.IsLlmCurrentlyBusy()) return;

            Task.Run(async () => {
                KoboldApiClient client = null;
                try
                {
                    string prompt = GenerateDeepTalkPrompt(initiator, recipient, interactionDef, subject);
                    SLog.Message(string.Format("[SocialInteractions] Generated prompt: {0}", prompt != null ? prompt.Substring(0, Math.Min(prompt.Length, 200)) : "NULL"));
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        string llmResponse = await GenerateTextWithApiClient(prompt);
                        SLog.Message(string.Format("[SocialInteractions] LLM Response: {0}", llmResponse != null ? llmResponse.Substring(0, Math.Min(llmResponse.Length, 200)) : "NULL"));
                        
                        if (llmResponse == null)
                        {
                            Log.Warning(string.Format("[SocialInteractions] HandleJobGiverInteraction: LLM API returned null response for interaction {0}", interactionDef.defName));
                            // Fallback to default interaction text
                            string fallbackText = string.Format("{0} talks with {1}.", initiator.Name.ToStringShort, recipient.Name.ToStringShort);
                            SpeechBubbleManager.EnqueueInstant(initiator, fallbackText, 2f, Color.grey); // Use standard mote for fallback
                            return;
                        }
                        
                        if (!string.IsNullOrEmpty(llmResponse))
                        {
                            // Split the response using multiple possible line break characters
                            string[] messages = llmResponse.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Take(Settings.llmMaxDialogueLines)
                                .ToArray();
                            if (messages.Any())
                            {
                                string rawMessage = messages[0].Trim();
                                Pawn speaker = null;

                                // Determine speaker and extract dialogue
                                // More robust speaker detection
                                if (rawMessage.StartsWith(initiator.Name.ToStringShort + ":", StringComparison.OrdinalIgnoreCase))
                                {
                                    speaker = initiator;
                                    rawMessage = rawMessage.Substring(initiator.Name.ToStringShort.Length + 1).Trim();
                                }
                                else if (rawMessage.StartsWith(recipient.Name.ToStringShort + ":", StringComparison.OrdinalIgnoreCase))
                                {
                                    speaker = recipient;
                                    rawMessage = rawMessage.Substring(recipient.Name.ToStringShort.Length + 1).Trim();
                                }
                                else
                                {
                                    speaker = initiator; // Default to initiator if speaker not specified
                                }

                                if (!string.IsNullOrWhiteSpace(rawMessage) && speaker != null)
                                {
                                    SpeechBubbleManager.EnqueueInstant(speaker, messages[0].Trim(), recipient, true); // Light sky blue for job giver interactions
                                }
                            }
                            else
                            {
                                Log.Warning(string.Format("[SocialInteractions] HandleJobGiverInteraction: LLM API returned empty messages for interaction {0}", interactionDef.defName));
                                // Fallback to default interaction text
                                string fallbackText = string.Format("{0} talks with {1}.", initiator.Name.ToStringShort, recipient.Name.ToStringShort);
                                SpeechBubbleManager.EnqueueInstant(initiator, fallbackText, 2f, Color.grey); // Use standard mote for fallback
                            }
                        }
                    }
                    else
                    {
                        Log.Warning(string.Format("[SocialInteractions] HandleJobGiverInteraction: Failed to generate prompt for interaction {0}", interactionDef.defName));
                        // Fallback to default interaction text
                        string fallbackText = string.Format("{0} talks with {1}.", initiator.Name.ToStringShort, recipient.Name.ToStringShort);
                        SpeechBubbleManager.EnqueueInstant(initiator, fallbackText, 2f, Color.grey); // Use standard mote for fallback
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("Error in HandleJobGiverInteraction: {0} {1}", ex.Message, ex.StackTrace));
                    // Fallback to default interaction text
                    try
                    {
                        string fallbackText = string.Format("{0} talks with {1}.", initiator.Name.ToStringShort, recipient.Name.ToStringShort);
                        SpeechBubbleManager.EnqueueInstant(initiator, fallbackText, 2f, Color.grey); // Use standard mote for fallback
                    }
                    catch (Exception fallbackEx)
                    {
                        Log.Error(string.Format("Error in HandleJobGiverInteraction fallback: {0} {1}", fallbackEx.Message, fallbackEx.StackTrace));
                    }
                }
                finally
                {
                    if (client != null)
                    {
                        client.Dispose();
                    }
                }
            });
        }

        /// <summary>
        /// Gets the first-degree relatives and ex-lovers of a pawn as a formatted string.
        /// </summary>
        /// <param name="pawn">The pawn to get relatives for</param>
        /// <returns>A formatted string listing the pawn's first-degree relatives and ex-lovers, or "None" if none exist</returns>
        private static string GetFamily(Pawn pawn)
        {
            if (pawn == null || pawn.relations == null)
            {
                return "None";
            }

            try
            {
                List<string> relatives = new List<string>();
                HashSet<int> addedRelativeIds = new HashSet<int>(); // Track pawn IDs to prevent duplicates
                
                // Get direct relations (spouse, lover, etc.)
                foreach (var relation in pawn.relations.PotentiallyRelatedPawns)
                {
                    if (relation == null || relation == pawn) continue;
                    
                    // Only include living relatives
                    if (relation.Dead || !relation.Spawned) continue;
                    
                    // Skip if already added (use thingIDNumber as unique identifier)
                    if (addedRelativeIds.Contains(relation.thingIDNumber)) continue;
                    
                    PawnRelationDef relationDef = pawn.GetMostImportantRelation(relation);
                    if (relationDef != null)
                    {
                        // Include first-degree relatives: parents/children/siblings/spouse/fiance/lover
                        if (relationDef == PawnRelationDefOf.Parent || 
                            relationDef == PawnRelationDefOf.Child ||
                            relationDef == PawnRelationDefOf.Sibling ||
                            relationDef == PawnRelationDefOf.Spouse || 
                            relationDef == PawnRelationDefOf.Fiance ||
                            relationDef == PawnRelationDefOf.Lover)
                        {
                            string relationLabel = relationDef.GetGenderSpecificLabelCap(relation);
                            relatives.Add(string.Format("{0} ({1})", relation.Name.ToStringShort, relationLabel));
                            addedRelativeIds.Add(relation.thingIDNumber); // Mark this pawn as added
                        }
                    }
                }
                
                // Add ex-relations (ex-lovers, ex-spouses, etc.) to the list by finding all direct relations of ex-types
                if (pawn.relations != null && pawn.relations.DirectRelations != null)
                {
                    foreach (DirectPawnRelation relation in pawn.relations.DirectRelations)
                    {
                        if (relation == null || relation.otherPawn == null) continue;
                        
                        // Check if this relation is an ex-relation type
                        if (relation.def == PawnRelationDefOf.ExLover || relation.def == PawnRelationDefOf.ExSpouse)
                        {
                            // Only include living ex-relations
                            if (relation.otherPawn.Dead || !relation.otherPawn.Spawned) continue;
                            
                            // Skip if already added
                            if (addedRelativeIds.Contains(relation.otherPawn.thingIDNumber)) continue;
                            
                            // Get the appropriate label for the relation type
                            string relationLabel = relation.def.GetGenderSpecificLabelCap(relation.otherPawn);
                            if (relationLabel == null || relationLabel.ToString().ToLower() == "null") // Check if label is not properly formatted
                            {
                                // Use specific label based on relation type
                                if (relation.def == PawnRelationDefOf.ExLover)
                                {
                                    relationLabel = "ex-lover";
                                }
                                else if (relation.def == PawnRelationDefOf.ExSpouse)
                                {
                                    relationLabel = "ex-spouse";
                                }
                            }
                            
                            // Add ex-relation to the list
                            relatives.Add(string.Format("{0} ({1})", relation.otherPawn.Name.ToStringShort, relationLabel));
                            addedRelativeIds.Add(relation.otherPawn.thingIDNumber); // Mark this pawn as added
                        }
                    }
                }

                if (relatives.Count > 0)
                {
                    return string.Join(", ", relatives.ToArray());
                }
            }
            catch (Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] GetFamily: Exception while getting family for {0}: {1}", 
                    pawn != null ? pawn.LabelShort : "null", ex.Message));
            }

            return "None";
        }
        
        /// <summary>
        /// Removes rich text tags from a string
        /// </summary>
        /// <param name="text">The text to process</param>
        /// <returns>The text with rich text tags removed</returns>
        public static string RemoveRichTextTags(string text)
        {
            return Regex.Replace(text, "<color=#.{8}>|</color>", "");
        }

        /// <summary>
        /// Selects a specified number of items using weighted random selection
        /// </summary>
        /// <typeparam name="T">The type of items to select</typeparam>
        /// <param name="items">The list of items to select from</param>
        /// <param name="count">The maximum number of items to select</param>
        /// <param name="weightSelector">A function to get the weight of each item</param>
        /// <returns>A list of selected items</returns>
        private static List<T> SelectWeightedRandom<T>(List<T> items, int count, Func<T, float> weightSelector)
        {
            if (items == null || items.Count == 0 || count <= 0)
            {
                return new List<T>();
            }

            var selectedItems = new List<T>();
            var availableItems = new List<T>(items);

            // Select up to 'count' items using weighted random selection
            for (int i = 0; i < Math.Min(count, items.Count); i++)
            {
                if (availableItems.Count == 0)
                {
                    break;
                }

                // Calculate total weight of all remaining items
                float totalWeight = 0f;
                foreach (T item in availableItems)
                {
                    float weight = Math.Max(0.0001f, weightSelector(item)); // Ensure minimum weight to avoid division by zero
                    totalWeight += weight;
                }

                // Select an item using weighted random selection
                float randomValue = UnityEngine.Random.value * totalWeight;
                float currentWeight = 0f;
                T selectedItem = default(T);

                foreach (T item in availableItems)
                {
                    float weight = Math.Max(0.0001f, weightSelector(item));
                    currentWeight += weight;
                    if (randomValue <= currentWeight)
                    {
                        selectedItem = item;
                        break;
                    }
                }

                if (!selectedItem.Equals(default(T)))
                {
                    selectedItems.Add(selectedItem);
                    availableItems.Remove(selectedItem);
                }
            }

            return selectedItems;
        }

        /// <summary>
        /// Gets the appropriate API client based on the selected API type
        /// </summary>
        /// <returns>IDisposable client instance</returns>
        private static IDisposable GetApiClient()
        {
            if (Settings.llmApiType == LlmApiType.Ollama)
            {
                return new OllamaApiClient(Settings.llmApiUrl, Settings.ollamaModelName);
            }
            else if (Settings.llmApiType == LlmApiType.OpenAI)
            {
                return new OpenAiApiClient(Settings.llmApiUrl, Settings.openAiModelName, Settings.llmApiKey);
            }
            else if (Settings.llmApiType == LlmApiType.LMStudio)
            {
                return new LMStudioApiClient(Settings.llmApiUrl, Settings.lmStudioModelName);
            }
            else if (Settings.llmApiType == LlmApiType.Gemini)
            {
                return new GeminiApiClient(Settings.llmApiUrl, Settings.llmApiKey);
            }
            else if (Settings.llmApiType == LlmApiType.Qwen)
            {
                return new QwenApiClient(Settings.llmApiUrl, Settings.qwenModelName, Settings.llmApiKey);
            }
            else if (Settings.llmApiType == LlmApiType.Deepseek)
            {
                return new DeepseekApiClient(Settings.llmApiUrl, Settings.deepseekModelName, Settings.llmApiKey);
            }
            else if (Settings.llmApiType == LlmApiType.Grok)
            {
                return new GrokApiClient(Settings.llmApiUrl, Settings.grokModelName, Settings.llmApiKey);
            }
            else if (Settings.llmApiType == LlmApiType.Claude)
            {
                return new ClaudeApiClient(Settings.llmApiUrl, Settings.claudeModelName, Settings.llmApiKey);
            }
            else if (Settings.llmApiType == LlmApiType.Player2)
            {
                return new Player2ApiClient(Settings.llmApiUrl, Settings.player2ModelName, Settings.llmApiKey, Settings.player2GameClientId);
            }
            else
            {
                return new KoboldApiClient(Settings.llmApiUrl, Settings.llmApiKey);
            }
        }

        /// <summary>
        /// Generates text using the configured API client
        /// </summary>
        /// <param name="prompt">The prompt to send to the LLM</param>
        /// <returns>The generated text response</returns>
        private static async Task<string> GenerateTextWithApiClient(string prompt)
        {
            IDisposable client = null;
            try
            {
                // Prepare sampling parameters once
                int? topK = Settings.llmTopK > 0 ? (int?)Settings.llmTopK : null;
                float? topP = Settings.llmTopP < 1.0f ? (float?)Settings.llmTopP : null;
                float? minP = Settings.llmMinP > 0.0f ? (float?)Settings.llmMinP : null;
                float? repPen = Settings.llmRepetitionPenalty != 1.0f ? (float?)Settings.llmRepetitionPenalty : null;

                if (Settings.llmApiType == LlmApiType.Ollama)
                {
                    client = new OllamaApiClient(Settings.llmApiUrl, Settings.ollamaModelName);
                    OllamaApiClient ollamaClient = client as OllamaApiClient;
                    if (ollamaClient != null)
                    {
                        return await ollamaClient.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                }
                else if (Settings.llmApiType == LlmApiType.LMStudio)
                {
                    client = new LMStudioApiClient(Settings.llmApiUrl, Settings.lmStudioModelName);
                    LMStudioApiClient lmStudioClient = client as LMStudioApiClient;
                    if (lmStudioClient != null)
                    {
                        return await lmStudioClient.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                }
                else if (Settings.llmApiType == LlmApiType.OpenAI)
                {
                    client = new OpenAiApiClient(Settings.llmApiUrl, Settings.openAiModelName, Settings.llmApiKey);
                    OpenAiApiClient openAiClient = client as OpenAiApiClient;
                    if (openAiClient != null)
                    {
                        return await openAiClient.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                }
                else if (Settings.llmApiType == LlmApiType.Gemini)
                {
                    client = new GeminiApiClient(Settings.llmApiUrl, Settings.llmApiKey);
                    GeminiApiClient geminiClient = client as GeminiApiClient;
                    if (geminiClient != null)
                    {
                        return await geminiClient.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                }
                else if (Settings.llmApiType == LlmApiType.Qwen)
                {
                    client = new QwenApiClient(Settings.llmApiUrl, Settings.qwenModelName, Settings.llmApiKey);
                    QwenApiClient qwenClient = client as QwenApiClient;
                    if (qwenClient != null)
                    {
                        return await qwenClient.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                }
                else if (Settings.llmApiType == LlmApiType.Deepseek)
                {
                    client = new DeepseekApiClient(Settings.llmApiUrl, Settings.deepseekModelName, Settings.llmApiKey);
                    DeepseekApiClient deepseekClient = client as DeepseekApiClient;
                    if (deepseekClient != null)
                    {
                        return await deepseekClient.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                }
                else if (Settings.llmApiType == LlmApiType.Grok)
                {
                    client = new GrokApiClient(Settings.llmApiUrl, Settings.grokModelName, Settings.llmApiKey);
                    GrokApiClient grokClient = client as GrokApiClient;
                    if (grokClient != null)
                    {
                        return await grokClient.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                }
                else if (Settings.llmApiType == LlmApiType.Claude)
                {
                    client = new ClaudeApiClient(Settings.llmApiUrl, Settings.claudeModelName, Settings.llmApiKey);
                    ClaudeApiClient claudeClient = client as ClaudeApiClient;
                    if (claudeClient != null)
                    {
                        return await claudeClient.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                }
                else if (Settings.llmApiType == LlmApiType.Player2)
                {
                    client = new Player2ApiClient(Settings.llmApiUrl, Settings.player2ModelName, Settings.llmApiKey, Settings.player2GameClientId);
                    Player2ApiClient player2Client = client as Player2ApiClient;
                    if (player2Client != null)
                    {
                        return await player2Client.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                }
                else
                {
                    client = new KoboldApiClient(Settings.llmApiUrl, Settings.llmApiKey);
                    KoboldApiClient koboldClient = client as KoboldApiClient;
                    if (koboldClient != null)
                    {
                        return await koboldClient.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                }
            }
            finally
            {
                if (client != null)
                {
                    client.Dispose();
                }
            }
            return null;
        }
        
        /// <summary>
        /// Gets a random selection of the least favorite pawn from the bottom 5 most disliked pawns
        /// Enhanced to consider social power dynamics to promote natural group formation
        /// </summary>
        /// <param name="pawn">The pawn whose least favorite to find</param>
        /// <returns>The randomly selected least favorite pawn based on weighted selection</returns>
        public static Pawn GetWeightedLeastFavoritePawn(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || pawn.Map.mapPawns == null)
            {
                return null;
            }

            // Create a list of pawns with combined scores based on opinion and social factors
            List<KeyValuePair<Pawn, float>> pawnScores = new List<KeyValuePair<Pawn, float>>();

            // Create a snapshot of the pawns list to avoid "collection was modified" errors
            List<Pawn> pawnsSnapshot = new List<Pawn>(pawn.Map.mapPawns.FreeColonistsAndPrisoners);

            // SLog.Message(string.Format("[SocialInteractions] GetWeightedLeastFavoritePawn: {0} considering {1} pawns", 
                // pawn.LabelShort, pawnsSnapshot.Count));

            foreach (Pawn otherPawn in pawnsSnapshot)
            {
                if (otherPawn == pawn)
                {
                    continue; // Skip self
                }

                // Ignore pawns that are in cryosleep or otherwise not spawned
                if (!otherPawn.Spawned || otherPawn.Dead)
                {
                    continue;
                }

                // Get raw opinion value (negative is worse for the target)
                int opinion = pawn.relations != null ? pawn.relations.OpinionOf(otherPawn) : 0;
                
                // Calculate social influence of this pawn (how well-regarded they are by others) -  0-1
                // This replicates the logic from DramaInteractionPatches.cs for use here
                float SocialInfluence = SocialInfluenceUtility.CalculateSocialInfluence(otherPawn, pawnsSnapshot);
                
                // Calculate integration with initiator's social network ( 0-1) - similar to method in DramaInteractionPatches.cs
                float Integration = SocialInfluenceUtility.CalculateSocialIntegration(pawn, otherPawn, pawnsSnapshot);
                
                // Create a base score (lower = more likely to be targeted initially based on negative opinion)
                // Negative opinion = better target for badmouthing
                float baseScore = -opinion; // Invert so that negative opinions (disliked) result in positive scores (higher = more targeted)

                // Calculate vulnerability factors where higher = more vulnerable (more likely to be targeted)
                // Pawns with low social influence are more vulnerable (1.0 - Influence)
                float socialVulnerability = 1.0f - SocialInfluence; // 0.0 = high influence, 1.0 = no influence
                // Pawns with low integration are more vulnerable (1.0 - Integration)
                float integrationVulnerability = 1.0f - Integration; // 0.0 = high integration, 1.0 = no integration
                
                // Calculate combined vulnerability where higher values = more vulnerable
                float combinedVulnerability = (socialVulnerability + integrationVulnerability) / 2f;
                
                // Calculate final score: base score (based on negative opinion) + vulnerability component
                // More negative opinions and higher vulnerability = higher final score = more likely to be targeted
                float finalScore = baseScore + (combinedVulnerability * 100f); // Scale vulnerability to match opinion scale

                // SLog.Message(string.Format("[SocialInteractions] GetWeightedLeastFavoritePawn: {0} -> opinion: {1}, baseScore: {2}, SocialInfluence: {3}, Integration: {4}, socialVulnerability: {5}, integrationVulnerability: {6}, combinedVulnerability: {7}, finalScore: {8}", 
                    // otherPawn.LabelShort, opinion, baseScore, SocialInfluence, Integration, socialVulnerability, integrationVulnerability, combinedVulnerability, finalScore));

                pawnScores.Add(new KeyValuePair<Pawn, float>(otherPawn, finalScore));
            }

            if (pawnScores.Count == 0)
            {
                SLog.Message("[SocialInteractions] GetWeightedLeastFavoritePawn: No pawns found to consider");
                return null;
            }

            // Sort by adjusted score (HIGHEST first - most likely to be targeted)
            pawnScores.Sort((x, y) => y.Value.CompareTo(x.Value)); // Descending order: highest scores first

            // Take the top 5 with the highest scores (most likely to be targeted based on all factors)
            int countToConsider = Math.Min(5, pawnScores.Count);
            List<Pawn> candidates = new List<Pawn>();
            for (int i = 0; i < countToConsider; i++)
            {
                candidates.Add(pawnScores[i].Key);
            }

            // SLog.Message(string.Format("[SocialInteractions] GetWeightedLeastFavoritePawn: Top {0} candidates after sorting:", countToConsider));
            // for (int i = 0; i < candidates.Count; i++)
            // {
                // float score = pawnScores[i].Value;
                // SLog.Message(string.Format("  {0}. {1} (score: {2})", i + 1, candidates[i].LabelShort, score));
            // }

            if (candidates.Count == 0)
            {
                SLog.Message("[SocialInteractions] GetWeightedLeastFavoritePawn: No candidates available");
                return null;
            }

            // Simply pick randomly from the top 5 most deserving candidates to avoid statistical focusing
            int randomIndex = Rand.Range(0, candidates.Count);
            Pawn selected = candidates[randomIndex];
            // SLog.Message(string.Format("[SocialInteractions] GetWeightedLeastFavoritePawn: Selected {0} (index {1}) from {2} candidates", 
                // selected.LabelShort, randomIndex, candidates.Count));

            return selected;
        }

        // Method to open voice selection dialog for a pawn
        public static void OpenVoiceSelectionDialog(Pawn pawn)
        {
            if (pawn == null)
            {
                SLog.Warning("[SocialInteractions] Attempted to open voice selection dialog for a null pawn.");
                return;
            }

            // Check if TTS is enabled
            if (!Settings.enableTTS)
            {
                Messages.Message("TTS must be enabled in settings to assign voices.", MessageTypeDefOf.RejectInput);
                return;
            }

            // Make sure voices are loaded
            var manager = Current.Game.GetComponent<VoiceAssignmentManager>();
            if (manager == null || VoiceAssignmentManager.AvailableVoices == null || VoiceAssignmentManager.AvailableVoices.Count == 0)
            {
                Messages.Message("No voices available. Make sure TTS server is configured and voices are loaded.", MessageTypeDefOf.RejectInput);
                return;
            }

            // Open the voice selection dialog
            Find.WindowStack.Add(new VoiceSelectionDialog(pawn));
        }

        /// <summary>
        /// Builds a formatted dossier string from pawn data. Code-generated (not LLM),
        /// so facts are guaranteed accurate. Generated fresh each time for current state.
        /// </summary>
        public static string BuildDossier(Pawn pawn)
        {
            if (pawn == null) return string.Empty;

            var data = ExtractPawnData(pawn, "p", null);
            var sb = new StringBuilder();

            sb.AppendLine("=== DOSSIER ===");
            sb.AppendLine(string.Format("Name: {0}", data.ContainsKey("p") ? data["p"] : pawn.LabelShort));
            sb.AppendLine(string.Format("Sex: {0}  |  Age: {1}",
                data.ContainsKey("p_sex") ? data["p_sex"] : "Unknown",
                data.ContainsKey("p_age") ? data["p_age"] : "Unknown"));
            sb.AppendLine(string.Format("Role: {0}", data.ContainsKey("p_title") ? data["p_title"] : "Unknown"));
            sb.AppendLine(string.Format("Faction: {0}", data.ContainsKey("p_faction") ? data["p_faction"] : "Unknown"));

            string ideology = data.ContainsKey("p_ideology_desc") ? data["p_ideology_desc"] : "None";
            if (ideology != "None")
                sb.AppendLine(string.Format("Ideology: {0}", ideology));

            sb.AppendLine(string.Format("Traits: {0}", data.ContainsKey("p_traits") ? data["p_traits"] : "None"));
            sb.AppendLine(string.Format("Skills: {0}", data.ContainsKey("p_proficiencies") ? data["p_proficiencies"] : "None"));

            string noskills = data.ContainsKey("p_noskills") ? data["p_noskills"] : "None";
            if (noskills != "None")
                sb.AppendLine(string.Format("Incapable of: {0}", noskills));

            string genes = data.ContainsKey("p_genes") ? data["p_genes"] : "None";
            if (genes != "None")
                sb.AppendLine(string.Format("Xenotype: {0}", genes));

            string tech = data.ContainsKey("p_tech") ? data["p_tech"] : "None";
            if (tech != "None")
                sb.AppendLine(string.Format("Implants: {0}", tech));

            string afflictions = data.ContainsKey("p_afflictions") ? data["p_afflictions"] : "None";
            if (afflictions != "None")
                sb.AppendLine(string.Format("Health: {0}", afflictions));

            string family = data.ContainsKey("p_family") ? data["p_family"] : "None";
            if (family != "None")
                sb.AppendLine(string.Format("Family: {0}", family));

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Parses LLM bio response into personality and quirks sections.
        /// Falls back gracefully if the LLM doesn't follow the expected format.
        /// </summary>
        private static string[] ParseBioResponse(string response)
        {
            string personality = "";
            string quirks = "";

            if (string.IsNullOrEmpty(response))
                return new string[] { "", "" };

            response = response.Trim();

            // Find section markers (case-insensitive)
            int personalityIdx = response.IndexOf("PERSONALITY:", StringComparison.OrdinalIgnoreCase);
            int quirksIdx = response.IndexOf("QUIRKS:", StringComparison.OrdinalIgnoreCase);

            // Try alternate headers the LLM might use
            if (quirksIdx < 0)
                quirksIdx = response.IndexOf("QUIRKS & VALUES:", StringComparison.OrdinalIgnoreCase);
            if (quirksIdx < 0)
                quirksIdx = response.IndexOf("QUIRKS AND VALUES:", StringComparison.OrdinalIgnoreCase);

            if (personalityIdx >= 0 && quirksIdx > personalityIdx)
            {
                // Both found in expected order
                int pStart = response.IndexOf('\n', personalityIdx);
                if (pStart < 0) pStart = personalityIdx + "PERSONALITY:".Length;
                else pStart += 1;

                personality = response.Substring(pStart, quirksIdx - pStart).Trim();

                int qStart = response.IndexOf('\n', quirksIdx);
                if (qStart < 0) qStart = quirksIdx + "QUIRKS:".Length;
                else qStart += 1;

                quirks = response.Substring(qStart).Trim();
            }
            else if (personalityIdx >= 0)
            {
                // Only personality found
                int pStart = response.IndexOf('\n', personalityIdx);
                if (pStart < 0) pStart = personalityIdx + "PERSONALITY:".Length;
                else pStart += 1;
                personality = response.Substring(pStart).Trim();
            }
            else if (quirksIdx >= 0)
            {
                // Only quirks found
                int qStart = response.IndexOf('\n', quirksIdx);
                if (qStart < 0) qStart = quirksIdx + "QUIRKS:".Length;
                else qStart += 1;
                quirks = response.Substring(qStart).Trim();
            }
            else
            {
                // Fallback: no markers found. Treat entire response as personality.
                personality = response;
            }

            // Clean up personality text
            personality = personality.TrimStart('-', '*', '#', ' ', '\n');

            // Clean up quirks: normalize bullet format
            if (!string.IsNullOrEmpty(quirks))
            {
                var lines = quirks.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                var cleanLines = new List<string>();
                foreach (var line in lines)
                {
                    string cleaned = line.Trim();
                    // Remove various bullet styles and normalize to "- "
                    if (cleaned.StartsWith("\u2022 ")) cleaned = cleaned.Substring(2);
                    else if (cleaned.StartsWith("* ")) cleaned = cleaned.Substring(2);
                    else if (cleaned.StartsWith("- ")) cleaned = cleaned.Substring(2);
                    else if (cleaned.Length > 2 && char.IsDigit(cleaned[0]) && (cleaned[1] == '.' || cleaned[1] == ')'))
                        cleaned = cleaned.Substring(2);
                    else if (cleaned.Length > 3 && char.IsDigit(cleaned[0]) && char.IsDigit(cleaned[1]) && (cleaned[2] == '.' || cleaned[2] == ')'))
                        cleaned = cleaned.Substring(3);

                    cleaned = cleaned.Trim();
                    if (!string.IsNullOrEmpty(cleaned))
                        cleanLines.Add("- " + cleaned);
                }
                quirks = string.Join("\n", cleanLines.ToArray());
            }

            return new string[] { personality, quirks };
        }

        /// <summary>
        /// Generates a structured bio for the pawn using the LLM.
        /// Returns personality + quirks text (dossier is generated separately by BuildDossier).
        /// </summary>
        public static async Task<string> GenerateBioAsync(Pawn pawn)
        {
            if (pawn == null) return null;

            if (!Settings.llmInteractionsEnabled)
            {
                SLog.Warning("[SocialInteractions] GenerateBioAsync called but LLM interactions are disabled.");
                return null;
            }

            if (string.IsNullOrEmpty(Settings.llmApiUrl))
            {
                SLog.Warning("[SocialInteractions] GenerateBioAsync called but LLM API URL is not set.");
                return null;
            }

            // Extract pawn data
            var pawnData = ExtractPawnData(pawn, "pawn1", null);

            // Build the structured prompt with all available data points
            var sb = new StringBuilder();
            sb.AppendLine("Generate a character profile for the following RimWorld character. RimWorld is a brutal sci-fi colony survival game on a distant rimworld planet.");
            sb.AppendLine();
            sb.AppendLine("Character data:");
            sb.AppendLine(string.Format("Name: {0}", pawnData.ContainsKey("pawn1") ? pawnData["pawn1"] : pawn.LabelShort));
            sb.AppendLine(string.Format("Sex: {0}", pawnData.ContainsKey("pawn1_sex") ? pawnData["pawn1_sex"] : pawn.gender.ToString()));
            sb.AppendLine(string.Format("Age: {0}", pawnData.ContainsKey("pawn1_age") ? pawnData["pawn1_age"] : pawn.ageTracker.AgeBiologicalYears.ToString()));
            sb.AppendLine(string.Format("Role: {0}", pawnData.ContainsKey("pawn1_title") ? pawnData["pawn1_title"] : "Unknown"));
            sb.AppendLine(string.Format("Faction: {0}", pawnData.ContainsKey("pawn1_faction") ? pawnData["pawn1_faction"] : "Unknown"));
            sb.AppendLine(string.Format("Ideology: {0}", pawnData.ContainsKey("pawn1_ideology_desc") ? pawnData["pawn1_ideology_desc"] : "None"));
            sb.AppendLine(string.Format("Traits: {0}", pawnData.ContainsKey("pawn1_traits") ? pawnData["pawn1_traits"] : "None"));
            sb.AppendLine(string.Format("Xenotype/Genes: {0}", pawnData.ContainsKey("pawn1_genes") ? pawnData["pawn1_genes"] : "None"));
            sb.AppendLine(string.Format("Key Skills: {0}", pawnData.ContainsKey("pawn1_proficiencies") ? pawnData["pawn1_proficiencies"] : "None"));
            sb.AppendLine(string.Format("Incapable of: {0}", pawnData.ContainsKey("pawn1_noskills") ? pawnData["pawn1_noskills"] : "None"));

            // Include additional data points for richer personality generation
            string family = pawnData.ContainsKey("pawn1_family") ? pawnData["pawn1_family"] : "None";
            if (family != "None")
                sb.AppendLine(string.Format("Family: {0}", family));

            string tech = pawnData.ContainsKey("pawn1_tech") ? pawnData["pawn1_tech"] : "None";
            if (tech != "None")
                sb.AppendLine(string.Format("Implants/Bionics: {0}", tech));

            string afflictions = pawnData.ContainsKey("pawn1_afflictions") ? pawnData["pawn1_afflictions"] : "None";
            if (afflictions != "None")
                sb.AppendLine(string.Format("Health Issues: {0}", afflictions));

            string mood = pawnData.ContainsKey("pawn1_mood") ? pawnData["pawn1_mood"] : "N/A";
            if (mood != "N/A")
                sb.AppendLine(string.Format("Current Mood: {0}", mood));

            string likes = pawnData.ContainsKey("pawn1_likes") ? pawnData["pawn1_likes"] : "None";
            if (likes != "None")
                sb.AppendLine(string.Format("What they enjoy: {0}", likes));

            string dislikes = pawnData.ContainsKey("pawn1_dislikes") ? pawnData["pawn1_dislikes"] : "None";
            if (dislikes != "None")
                sb.AppendLine(string.Format("What bothers them: {0}", dislikes));

            // Append section instructions
            bool isLocalApi = Settings.llmApiType == LlmApiType.KoboldCpp ||
                              Settings.llmApiType == LlmApiType.LMStudio ||
                              Settings.llmApiType == LlmApiType.Ollama;
            bool isTextCompletion = isLocalApi && !Settings.forceChatCompletion;

            if (isTextCompletion)
            {
                sb.AppendLine();
                sb.AppendLine("Write two sections: PERSONALITY (a paragraph describing their personality and demeanor) and QUIRKS (3-5 bullet points of specific quirks, values, or habits).");
                sb.Append("\n<start>\nPERSONALITY:\n");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("Write exactly two sections with these exact headers:");
                sb.AppendLine();
                sb.AppendLine("PERSONALITY:");
                sb.AppendLine("Write 3-5 sentences describing this character's personality, demeanor, and how their past shaped who they are. Weave in their traits, skills, and circumstances naturally. Interpret the data -- do not repeat it verbatim.");
                sb.AppendLine();
                sb.AppendLine("QUIRKS:");
                sb.AppendLine("Write 3-5 bullet points (starting with \"- \") describing specific behavioral quirks, values, habits, or tendencies unique to this character.");
                sb.AppendLine();
                sb.AppendLine("Do not include any other sections, headers, or conversational text.");
            }

            string prompt = sb.ToString();

            SLog.Message(string.Format("[SocialInteractions] Requesting Auto-Generate Bio for {0}", pawn.LabelShort));

            try
            {
                string result = await GenerateTextWithApiClient(prompt);
                if (string.IsNullOrEmpty(result))
                    return null;

                // Parse the structured response into personality + quirks
                string[] sections = ParseBioResponse(result);
                string personality = sections[0];
                string quirks = sections[1];

                // Compose the display/storage format
                if (!string.IsNullOrEmpty(quirks))
                {
                    return personality + "\n\nQuirks & Values:\n" + quirks;
                }
                return personality;
            }
            catch (Exception ex)
            {
                SLog.Error(string.Format("[SocialInteractions] Error generating bio for {0}: {1}", pawn.LabelShort, ex.Message));
                return null;
            }
        }
    }
}
