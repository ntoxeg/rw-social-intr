using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;
using Verse.Sound;
using Verse.AI.Group;

namespace SocialInteractions
{
    /// <summary>
    /// Manages the negotiation flow between two pawns with LLM integration.
    /// Handles prompt generation, LLM requests, response parsing, and outcome determination.
    /// </summary>
    public class NegotiationManager
    {
        private Pawn initiator;
        private Pawn target;
        private Dialog_PawnNegotiation dialog;
        
        private StringBuilder conversationHistory = new StringBuilder();
        private List<string> currentChoices = new List<string>(); // Store current choices
        private string lastSelectedChoice = null;
        private int turnCount = 0;
        private const int MaxTurns = 10; // Safety limit
        
        private bool isActive = false;
        public bool IsInteractionLimitReached { get { return turnCount >= MaxTurns; } }
        private bool outcomeApplied = false; // Tracks if FinalizeNegotiation has run
        private NegotiationOutcome? lastNotifiedOutcome = null; // Prevent duplicate messages
        private NegotiationOutcome? pendingOutcome = null;
        
        // Store last dialogue lines for final display
        private List<DialogueLine> lastDialogueLines = new List<DialogueLine>();
        private int conversationId = -1;
        private bool waitingForLLM = false; // Add tracking here too
        
        // Raid negotiation context (null if not negotiating with a raid)
        private Lord raidContext = null;
        private bool isTradeContext = false;
        private bool isVisitorContext = false;
        private bool isSocialFightContext = false;
        private bool isMentalStateContext = false;
        private Pawn otherFighter = null;

        public string InteractionTypeLabel
        {
            get
            {
                if (raidContext != null) return "Raid Negotiation";
                if (isTradeContext) return "Trade Negotiation";
                if (isVisitorContext) return "Visitor Negotiation";
                if (isSocialFightContext) return "Social Fight Intervention";
                if (isMentalStateContext) return "Mental Break Intervention";
                
                if (target.Faction == Faction.OfPlayer)
                    return "Negotiation with Colonist";
                
                return "Negotiation with " + target.KindLabel;
            }
        }
        
        public NegotiationManager(Pawn initiator, Pawn target, Dialog_PawnNegotiation dialog)
        {
            this.initiator = initiator;
            this.target = target;
            this.dialog = dialog;
            
            // Check if this is a raid negotiation
            this.raidContext = RaidNegotiationContext.GetActiveRaid(initiator);
            if (raidContext != null)
            {
                SLog.Message("[Negotiation] Raid context detected for " + initiator.LabelShort);
            }
            
            // Check if this is a trade negotiation
            this.isTradeContext = target.TraderKind != null;
            if (!this.isTradeContext)
            {
                Lord lord = target.GetLord();
                if (lord != null && lord.LordJob is LordJob_TradeWithColony)
                {
                    this.isTradeContext = true;
                }
            }
            if (isTradeContext)
            {
                SLog.Message("[Negotiation] Trade context detected for " + target.LabelShort);
            }
            
            // Check if this is a visitor/refugee negotiation
            if (!this.isTradeContext && this.raidContext == null)
            {
                Lord lord = target.GetLord();
                if (lord != null && lord.LordJob != null)
                {
                    string jobName = lord.LordJob.GetType().Name;
                    if (jobName.Contains("Visit") || jobName.Contains("Refugee") || jobName.Contains("Guest") || jobName.Contains("Traveler"))
                    {
                        this.isVisitorContext = true;
                        SLog.Message("[Negotiation] Visitor context detected for " + target.LabelShort + " (Job: " + jobName + ")");
                    }
                }
            }
            
            // Check if this is a social fight negotiation
            if (target.MentalStateDef == MentalStateDefOf.SocialFighting)
            {
                this.isSocialFightContext = true;
                MentalState_SocialFighting socialFight = target.MentalState as MentalState_SocialFighting;
                if (socialFight != null)
                {
                    this.otherFighter = socialFight.otherPawn;
                }
                
                if (this.otherFighter != null)
                {
                    SLog.Message("[Negotiation] Social fight context detected between " + target.LabelShort + " and " + otherFighter.LabelShort);
                }
            }
            
            // Check if this is a general mental state negotiation (but not social fighting)
            if (!this.isSocialFightContext && target.InMentalState)
            {
                this.isMentalStateContext = true;
                SLog.Message("[Negotiation] Mental state context detected for " + target.LabelShort + " (State: " + target.MentalStateDef.defName + ")");
            }
        }
        
        public void StartNegotiation()
        {
            isActive = true;
            outcomeApplied = false;
            lastNotifiedOutcome = null;
            pendingOutcome = null;
            turnCount = 0;
            conversationHistory.Clear();
            
            SLog.Message("[Negotiation] Starting negotiation between " + initiator.LabelShort + " and " + target.LabelShort);
            
            // Apply negotiating hediff
            ApplyNegotiatingHediff();
            
            // Send initial LLM request
            SendLLMRequest(null);
        }
        
        public void OnChoiceSelected(int choiceIndex)
        {
            if (!isActive) return;
            
            SLog.Message("[Negotiation] OnChoiceSelected called with index: " + choiceIndex + ", currentChoices.Count: " + currentChoices.Count);
            
            if (choiceIndex >= 0 && choiceIndex < currentChoices.Count)
            {
                lastSelectedChoice = currentChoices[choiceIndex];
                SLog.Message("[Negotiation] Choice selected: " + lastSelectedChoice);
                SendLLMRequest(lastSelectedChoice);
            }
            else
            {
                SLog.Warning("[Negotiation] Invalid choice index: " + choiceIndex);
            }
        }
        
        public void OnCustomInput(string customText)
        {
            if (!isActive) return;
            
            lastSelectedChoice = customText;
            SLog.Message("[Negotiation] Custom input: " + customText);
            SendLLMRequest(customText);
        }
        
        public void EndNegotiationEarly()
        {
            SLog.Message("[Negotiation] EndNegotiationEarly called by user. Waiting: " + waitingForLLM);
            if (!waitingForLLM)
            {
                isActive = false; // Mark as done immediately to skip delay feedback
                EndNegotiation();
            }
            else
            {
                SLog.Message("[Negotiation] User closed window while waiting for LLM. Background task will finalize later.");
                isActive = false; // Ensure background task knows window is closed
            }
        }
        
        private void SendLLMRequest(string selectedChoice)
        {
            dialog.SetWaiting(true);
            turnCount++;
            
            if (turnCount > MaxTurns)
            {
                SLog.Warning("[Negotiation] Max turns reached, forcing conclusion");
                EndNegotiation();
                return;
            }
            
            // Build prompt
            string prompt = BuildPrompt(selectedChoice);
            SLog.Message("[Negotiation] Sending prompt (turn " + turnCount + "):\n" + prompt.Substring(0, Math.Min(500, prompt.Length)) + "...");
            
            // Send async LLM request
            waitingForLLM = true;
            Task.Run(async () =>
            {
                try
                {
                    string response = await GetLLMResponse(prompt);
                    
                    // Process on main thread using Coroutine
                    ExecuteOnMainThread(() =>
                    {
                        waitingForLLM = false;
                        // Always process the response to ensure outcomes are applied, 
                        // even if the dialog was closed (isActive == false)
                        ProcessLLMResponse(response);
                    });
                }
                catch (Exception ex)
                {
                    SLog.Error("[Negotiation] LLM error: " + ex.Message);
                    ExecuteOnMainThread(() =>
                    {
                        waitingForLLM = false;
                        if (isActive)
                        {
                            HandleLLMFailure();
                        }
                        else
                        {
                            // If the window was closed but we had an error,
                            // we must still ensure the negotiation is finalized
                            // to clear the LLM busy state (isLlmBusy/activeConversations).
                            EndNegotiation();
                        }
                    });
                }
            });
        }
        
        public static void ExecuteOnMainThread(Action action)
        {
            if (Current.Root != null)
            {
                ((MonoBehaviour)Current.Root).StartCoroutine(ExecuteOnMainThreadRoutine(action));
            }
            else
            {
                // Fallback (unlikely during gameplay)
                LongEventHandler.ExecuteWhenFinished(action);
            }
        }

        private static IEnumerator ExecuteOnMainThreadRoutine(Action action)
        {
            yield return null; // Wait one frame
            if (action != null)
            {
                try 
                {
                    action(); 
                }
                catch (Exception ex)
                {
                    SLog.Error("[Negotiation] Error executing on main thread: " + ex);
                }
            }
        }
        
        /// <summary>
        /// Build the base negotiation prompt.
        /// </summary>
        private string BuildPrompt(string selectedChoice)
        {
            StringBuilder sb = new StringBuilder();
            
            // System context - different for raid vs trade vs normal negotiation
            if (raidContext != null)
            {
                return BuildRaidPrompt(selectedChoice);
            }
            if (isTradeContext)
            {
                return BuildTradePrompt(selectedChoice);
            }
            if (isVisitorContext)
            {
                return BuildVisitorPrompt(selectedChoice);
            }
            if (isSocialFightContext)
            {
                return BuildSocialFightPrompt(selectedChoice);
            }
            if (isMentalStateContext)
            {
                return BuildMentalStatePrompt(selectedChoice);
            }
            
            sb.AppendLine("You are writing a negotiation dialogue between two pawns in the colony survival game RimWorld.");
            sb.AppendLine();
            
            // Pawn 1 context
            var pawn1Data = SocialInteractions.ExtractPawnData(initiator, "pawn1", target);
            sb.AppendLine("[Initiator - " + initiator.LabelShort + "]");
            AppendPawnContext(sb, pawn1Data, "pawn1", initiator);
            sb.AppendLine();
            
            // Pawn 2 context
            var pawn2Data = SocialInteractions.ExtractPawnData(target, "pawn2", initiator);
            sb.AppendLine("[Target - " + target.LabelShort + "]");
            AppendPawnContext(sb, pawn2Data, "pawn2", target);
            sb.AppendLine();
            
            // Relationship
            sb.AppendLine("[Relationship]");
            sb.AppendLine(SocialInteractions.GetRelationship(initiator, target));
            sb.AppendLine();
            
            // World context
            AppendWorldContext(sb);
            sb.AppendLine();
            
            // Conversation history
            if (conversationHistory.Length > 0)
            {
                sb.AppendLine("[Conversation so far]");
                sb.AppendLine(conversationHistory.ToString());
                sb.AppendLine();
            }
            
            // Selected choice
            if (!string.IsNullOrEmpty(selectedChoice))
            {
                sb.AppendLine("[" + initiator.LabelShort + " chooses: \"" + selectedChoice + "\"]");
                sb.AppendLine();
            }
            
            // Instructions
            sb.AppendLine("Continue the dialogue. Write what " + initiator.LabelShort + " says (based on the choice if given), then " + target.LabelShort + "'s response.");
            sb.AppendLine("If the conversation has reached a natural conclusion (agreement, disagreement, or impasse), provide only the appropriate outcome:");
            sb.AppendLine("Otherwise, provide exactly 3 new action choices for " + initiator.LabelShort + ".");
            sb.AppendLine();
            AppendFormatPrompt(sb);
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Build a raid-specific negotiation prompt.
        /// </summary>
        private string BuildRaidPrompt(string selectedChoice)
        {
            StringBuilder sb = new StringBuilder();
            
            // System context for raid negotiation
            sb.AppendLine("You are writing a tense negotiation dialogue between a colonist and an enemy raider in a colony survival game.");
            sb.AppendLine("The colonist is attempting to negotiate with hostile raiders to avoid combat.");
            sb.AppendLine();
            
            // Negotiator context
            var pawn1Data = SocialInteractions.ExtractPawnData(initiator, "pawn1", target);
            sb.AppendLine("[Negotiator - " + initiator.LabelShort + "]");
            AppendPawnContext(sb, pawn1Data, "pawn1", initiator);
            sb.AppendLine();
            
            // Raider leader context
            var pawn2Data = SocialInteractions.ExtractPawnData(target, "pawn2", initiator);
            sb.AppendLine("[Raider Leader - " + target.LabelShort + "]");
            AppendPawnContext(sb, pawn2Data, "pawn2", target);
            sb.AppendLine();
            
            // Relationship
            sb.AppendLine("[Relationship]");
            sb.AppendLine(SocialInteractions.GetRelationship(initiator, target));
            sb.AppendLine();
            
            // Raid context
            sb.AppendLine("[Raid Context]");
            sb.AppendLine("- Faction: " + (raidContext.faction != null ? raidContext.faction.Name : "Unknown"));
            sb.AppendLine("- Raider count: " + raidContext.ownedPawns.Count(p => p != null && !p.Dead));
            if (raidContext.faction != null)
            {
                int goodwill = raidContext.faction.PlayerGoodwill;
                string relationDesc = goodwill < -80 ? "bitter enemies" : 
                                      goodwill < -40 ? "hostile" : 
                                      goodwill < 0 ? "unfriendly" : "neutral";
                sb.AppendLine("- Faction relations: " + relationDesc + " (goodwill: " + goodwill + ")");
            }
            sb.AppendLine();
            
            // World context
            AppendWorldContext(sb);
            sb.AppendLine();
            
            // Conversation history
            if (conversationHistory.Length > 0)
            {
                sb.AppendLine("[Conversation so far]");
                sb.AppendLine(conversationHistory.ToString());
                sb.AppendLine();
            }
            
            // Selected choice
            if (!string.IsNullOrEmpty(selectedChoice))
            {
                sb.AppendLine("[" + initiator.LabelShort + " says: \"" + selectedChoice + "\"]");
                sb.AppendLine();
            }
            
            // Instructions for raid negotiation
            sb.AppendLine("Continue the negotiation. Write what " + initiator.LabelShort + " says, then " + target.LabelShort + "'s response.");
            sb.AppendLine();
            sb.AppendLine("Possible outcomes:");
            sb.AppendLine("- CRITICAL_SUCCESS: Raiders agree to leave peacefully without taking anything");
            sb.AppendLine("- POSITIVE: Raiders agree to not harm the colonists, but will loiter for a while and may take some valuables");
            sb.AppendLine("- NEUTRAL: No agreement, raiders remain hostile but haven't attacked yet");
            sb.AppendLine("- NEGATIVE: Negotiation fails disastrously, raiders attack immediately");
            sb.AppendLine();
            sb.AppendLine("If the conversation has reached a conclusion, provide the final outcome.");
            sb.AppendLine("Otherwise, provide exactly 3 new dialogue choices for " + initiator.LabelShort + ".");
            sb.AppendLine();
            AppendFormatPrompt(sb);
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Build a trade-specific negotiation prompt.
        /// </summary>
        private string BuildTradePrompt(string selectedChoice)
        {
            StringBuilder sb = new StringBuilder();
            
            // System context for trade negotiation
            sb.AppendLine("You are writing a dialogue between a colonist and a traveling merchant in the colony survival game RimWorld.");
            sb.AppendLine("The colonist is attempting to haggle or build rapport to get better prices or find rare goods.");
            sb.AppendLine();
            
            // Negotiator context
            var pawn1Data = SocialInteractions.ExtractPawnData(initiator, "pawn1", target);
            sb.AppendLine("[Negotiator - " + initiator.LabelShort + "]");
            AppendPawnContext(sb, pawn1Data, "pawn1", initiator);
            sb.AppendLine();
            
            // Merchant context
            var pawn2Data = SocialInteractions.ExtractPawnData(target, "pawn2", initiator);
            sb.AppendLine("[Merchant - " + target.LabelShort + "]");
            AppendPawnContext(sb, pawn2Data, "pawn2", target);
            if (target.TraderKind != null)
            {
                sb.AppendLine("- Merchant Type: " + target.TraderKind.label);
            }
            sb.AppendLine();
            
            // Relationship
            sb.AppendLine("[Relationship]");
            sb.AppendLine(SocialInteractions.GetRelationship(initiator, target));
            sb.AppendLine();
            
            // World context
            AppendWorldContext(sb);
            sb.AppendLine();
            
            // Conversation history
            if (conversationHistory.Length > 0)
            {
                sb.AppendLine("[Conversation so far]");
                sb.AppendLine(conversationHistory.ToString());
                sb.AppendLine();
            }
            
            // Selected choice
            if (!string.IsNullOrEmpty(selectedChoice))
            {
                sb.AppendLine("[" + initiator.LabelShort + " chooses: \"" + selectedChoice + "\"]");
                sb.AppendLine();
            }
            
            // Instructions
            sb.AppendLine("Continue the dialogue. Write what " + initiator.LabelShort + " says, then " + target.LabelShort + "'s response.");
            sb.AppendLine("If the merchant is impressed, provide a POSITIVE outcome.");
            sb.AppendLine("If the merchant is extremely impressed and inspired by the interaction, provide a CRITICAL_SUCCESS outcome.");
            sb.AppendLine("If the merchant is not impressed and wants to leave, provide a NEGATIVE outcome.");
            sb.AppendLine("If the negociation can continue, provide a NEUTRAL outcome.");
            sb.AppendLine("Otherwise, provide exactly 3 new action choices for " + initiator.LabelShort + ".");
            sb.AppendLine();
            AppendFormatPrompt(sb);
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Build a visitor-specific negotiation prompt.
        /// </summary>
        private string BuildVisitorPrompt(string selectedChoice)
        {
            StringBuilder sb = new StringBuilder();
            
            // System context for visitor negotiation
            sb.AppendLine("You are writing a dialogue between a colonist and a visitor/refugee/traveler in the colony survival game RimWorld.");
            sb.AppendLine("The colonist is attempting to build rapport, share news, or make a good impression on behalf of the colony.");
            sb.AppendLine();
            
            // Negotiator context
            var pawn1Data = SocialInteractions.ExtractPawnData(initiator, "pawn1", target);
            sb.AppendLine("[Negotiator - " + initiator.LabelShort + "]");
            AppendPawnContext(sb, pawn1Data, "pawn1", initiator);
            sb.AppendLine();
            
            // Visitor context
            var pawn2Data = SocialInteractions.ExtractPawnData(target, "pawn2", initiator);
            sb.AppendLine("[Visitor - " + target.LabelShort + "]");
            AppendPawnContext(sb, pawn2Data, "pawn2", target);
            
            Lord lord = target.GetLord();
            if (lord != null && lord.LordJob != null)
            {
                sb.AppendLine("- Activity: " + lord.LordJob.GetType().Name.Replace("LordJob_", ""));
            }
            sb.AppendLine();
            
            // Relationship
            sb.AppendLine("[Relationship]");
            sb.AppendLine(SocialInteractions.GetRelationship(initiator, target));
            sb.AppendLine();
            
            // World context
            AppendWorldContext(sb);
            sb.AppendLine();
            
            // Conversation history
            if (conversationHistory.Length > 0)
            {
                sb.AppendLine("[Conversation so far]");
                sb.AppendLine(conversationHistory.ToString());
                sb.AppendLine();
            }
            
            // Selected choice
            if (!string.IsNullOrEmpty(selectedChoice))
            {
                sb.AppendLine("[" + initiator.LabelShort + " chooses: \"" + selectedChoice + "\"]");
                sb.AppendLine();
            }
            
            // Instructions
            sb.AppendLine("Continue the dialogue. Write what " + initiator.LabelShort + " says, then " + target.LabelShort + "'s response.");
            sb.AppendLine("If the visitor is impressed or grateful, provide a POSITIVE outcome.");
            sb.AppendLine("If the visitor is PROFOUNDLY impressed and expresses a desire to stay and join the colony, provide a CRITICAL_SUCCESS outcome.");
            sb.AppendLine("If the visitor is dissatisfied or angry, provide a NEGATIVE outcome.");
            sb.AppendLine("If the negotiation can continue, provide a NEUTRAL outcome.");
            sb.AppendLine("Otherwise, provide exactly 3 new action choices for " + initiator.LabelShort + ".");
            sb.AppendLine();
            AppendFormatPrompt(sb);
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Build a social fight specific negotiation prompt.
        /// </summary>
        private string BuildSocialFightPrompt(string selectedChoice)
        {
            StringBuilder sb = new StringBuilder();
            
            sb.AppendLine("You are writing a dialogue in the colony survival game RimWorld.");
            sb.AppendLine("A physical brawl (social fight) has broken out between two colonists. " + initiator.LabelShort + " is intervening to try and stop the fight.");
            sb.AppendLine();
            
            // Negotiator context
            var pawn1Data = SocialInteractions.ExtractPawnData(initiator, "pawn1", target);
            sb.AppendLine("[Intervenor - " + initiator.LabelShort + "]");
            AppendPawnContext(sb, pawn1Data, "pawn1", initiator);
            sb.AppendLine();
            
            // Target context (one of the fighters)
            var pawn2Data = SocialInteractions.ExtractPawnData(target, "pawn2", initiator);
            sb.AppendLine("[Fighter 1 - " + target.LabelShort + "]");
            AppendPawnContext(sb, pawn2Data, "pawn2", target);
            sb.AppendLine();
            
            // Other fighter context
            if (otherFighter != null)
            {
                var pawn3Data = SocialInteractions.ExtractPawnData(otherFighter, "pawn3", initiator);
                sb.AppendLine("[Fighter 2 - " + otherFighter.LabelShort + "]");
                AppendPawnContext(sb, pawn3Data, "pawn3", otherFighter);
                sb.AppendLine();
            }
            
            // Relationship
            sb.AppendLine("[Relationships]");
            sb.AppendLine(initiator.LabelShort + " and " + target.LabelShort + ": " + SocialInteractions.GetRelationship(initiator, target));
            if (otherFighter != null)
            {
                sb.AppendLine(initiator.LabelShort + " and " + otherFighter.LabelShort + ": " + SocialInteractions.GetRelationship(initiator, otherFighter));
                sb.AppendLine(target.LabelShort + " and " + otherFighter.LabelShort + ": " + SocialInteractions.GetRelationship(target, otherFighter));
            }
            sb.AppendLine();
            
            // World context
            AppendWorldContext(sb);
            sb.AppendLine();
            
            // Conversation history
            if (conversationHistory.Length > 0)
            {
                sb.AppendLine("[Conversation so far]");
                sb.AppendLine(conversationHistory.ToString());
                sb.AppendLine();
            }
            
            // Selected choice
            if (!string.IsNullOrEmpty(selectedChoice))
            {
                sb.AppendLine("[" + initiator.LabelShort + " says: \"" + selectedChoice + "\"]");
                sb.AppendLine();
            }
            
            string punchTarget = (otherFighter != null) ? otherFighter.LabelShort : "someone";
            sb.AppendLine("Continue the dialogue. Write what " + initiator.LabelShort + " says, then " + target.LabelShort + "'s response (who is currently punching " + punchTarget + ").");
            sb.AppendLine("If the fighters are convinced to stop, provide a POSITIVE outcome.");
            sb.AppendLine("If they are not only stopped but also reconciled and regret their actions, provide a CRITICAL_SUCCESS outcome.");
            sb.AppendLine("If the fighters are not convinced to stop, provide a NEGATIVE outcome (where they keep fighting or turn on the intervenor).");
            sb.AppendLine("If the negotiation can continue, provide a NEUTRAL outcome.");
            sb.AppendLine("Otherwise, provide exactly 3 new action choices for " + initiator.LabelShort + ".");
            sb.AppendLine();
            AppendFormatPrompt(sb);
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Build a general mental state negotiation prompt.
        /// </summary>
        private string BuildMentalStatePrompt(string selectedChoice)
        {
            StringBuilder sb = new StringBuilder();
            
            sb.AppendLine("You are writing a dialogue in the colony survival game RimWorld.");
            sb.AppendLine(target.LabelShort + " is in a mental break state: " + target.MentalStateDef.LabelCap + ". " + initiator.LabelShort + " is attempting to talk them down and help them recover.");
            sb.AppendLine();
            
            // Negotiator context
            var pawn1Data = SocialInteractions.ExtractPawnData(initiator, "pawn1", target);
            sb.AppendLine("[Intervenor - " + initiator.LabelShort + "]");
            AppendPawnContext(sb, pawn1Data, "pawn1", initiator);
            sb.AppendLine();
            
            // Target context (pawn in mental break)
            var pawn2Data = SocialInteractions.ExtractPawnData(target, "pawn2", initiator);
            sb.AppendLine("[Target - " + target.LabelShort + "]");
            AppendPawnContext(sb, pawn2Data, "pawn2", target);
            sb.AppendLine();
            
            // World context
            AppendWorldContext(sb);
            sb.AppendLine();
            
            // Conversation history
            if (conversationHistory.Length > 0)
            {
                sb.AppendLine("[Conversation so far]");
                sb.AppendLine(conversationHistory.ToString());
                sb.AppendLine();
            }
            
            // Selected choice
            if (!string.IsNullOrEmpty(selectedChoice))
            {
                sb.AppendLine("[" + initiator.LabelShort + " says: \"" + selectedChoice + "\"]");
                sb.AppendLine();
            }
            
            // Instructions
            sb.AppendLine("Continue the dialogue. Write what " + initiator.LabelShort + " says, then " + target.LabelShort + "'s response (who is currently in a " + target.MentalStateDef.LabelCap + " state).");
            sb.AppendLine("If " + target.LabelShort + " is calmed down or shown reason, provide a POSITIVE or CRITICAL_SUCCESS outcome.");
            sb.AppendLine("If " + target.LabelShort + " is angered or enraged, provide a NEGATIVE outcome (where they snap and become BERSERK).");
            sb.AppendLine("Otherwise, provide a NEUTRAL outcome");
            sb.AppendLine();
            AppendFormatPrompt(sb);
            
            return sb.ToString();
        }

        private void AppendFormatPrompt(StringBuilder sb)
        {
            sb.AppendLine("FORMAT: Respect the following format exactly for response parsing.");
            sb.AppendLine();
            sb.AppendLine(initiator.LabelShort + ": bla bla...");
            sb.AppendLine(target.LabelShort + ": bla bla...");
            sb.AppendLine();
            sb.AppendLine("OUTCOME: NEUTRAL | NEGATIVE | POSITIVE | CRITICAL_SUCCESS");
            sb.AppendLine();
            sb.AppendLine("CHOICES:");
            sb.AppendLine("1. action/statement");
            sb.AppendLine("2. action/statement");
            sb.AppendLine("3. action/statement");
            sb.AppendLine("END_CHOICES");
        }
        
        private void AppendPawnContext(StringBuilder sb, Dictionary<string, string> data, string prefix, Pawn pawn)
        {
            string name = data.ContainsKey(prefix) ? data[prefix] : "Unknown";
            string sex = data.ContainsKey(prefix + "_sex") ? data[prefix + "_sex"] : "unknown";
            string age = data.ContainsKey(prefix + "_age") ? data[prefix + "_age"] : "unknown";
            string title = data.ContainsKey(prefix + "_title") ? data[prefix + "_title"] : "unknown";
            string faction = data.ContainsKey(prefix + "_faction") ? data[prefix + "_faction"] : "unknown";
            string ideology = data.ContainsKey(prefix + "_ideology") ? data[prefix + "_ideology"] : "unknown";
            string traits = data.ContainsKey(prefix + "_traits") ? data[prefix + "_traits"] : "unknown";
            string genes = data.ContainsKey(prefix + "_genes") ? data[prefix + "_genes"] : "unknown";
            string proficiencies = data.ContainsKey(prefix + "_proficiencies") ? data[prefix + "_proficiencies"] : "unknown";
            string noskills = data.ContainsKey(prefix + "_noskills") ? data[prefix + "_noskills"] : "unknown";
            string mood = data.ContainsKey(prefix + "_mood") ? data[prefix + "_mood"] : "unknown";
            string likes = data.ContainsKey(prefix + "_likes") ? data[prefix + "_likes"] : "unknown";
            string dislikes = data.ContainsKey(prefix + "_dislikes") ? data[prefix + "_dislikes"] : "unknown";
            string afflictions = data.ContainsKey(prefix + "_afflictions") ? data[prefix + "_afflictions"] : "unknown";
            string family = data.ContainsKey(prefix + "_family") ? data[prefix + "_family"] : "unknown";
            string bio = data.ContainsKey(prefix + "_bio") ? data[prefix + "_bio"] : "";
            string action = data.ContainsKey(prefix + "_action") ? data[prefix + "_action"] : "unknown";

            string description = string.Format(
                "{0} is a {1}, age {2}, a {3} of the {4} faction, following the {5} ideology, has the following traits: {6}; Xenotype: {7}; {0} is proficient in: {8}; {0} is incapable of: {9}; {0}'s mood is {10}, positives: {11} / negatives: {12}; Medical status: {13}. {0}'s family: {14}. {15}",
                name, sex, age, title, faction, ideology, traits, genes, proficiencies, noskills, mood, likes, dislikes, afflictions, family, bio);
            
            sb.AppendLine(description);

            // Add Social Skill context
            if (pawn.skills != null)
            {
                var socialSkill = pawn.skills.GetSkill(SkillDefOf.Social);
                if (socialSkill != null)
                {
                    sb.AppendLine(string.Format("IMPORTANT: {0}'s Social skill level is {1} (0-20 scale). Use this to determine their negotiation capability and eloquence.", name, socialSkill.Level));
                }
            }

            sb.AppendLine(string.Format("{0} is currently {1}", name, action));
        }
        
        private void AppendWorldContext(StringBuilder sb)
        {
            if (initiator.Map == null) return;
            
            sb.AppendLine("[World Context]");
            
            long absTicks = Find.TickManager.TicksAbs;
            float longitude = Find.WorldGrid.LongLatOf(initiator.Tile).x;
            int day = GenDate.DayOfQuadrum(absTicks, longitude);
            Quadrum quadrum = GenDate.Quadrum(absTicks, longitude);
            int year = GenDate.Year(absTicks, longitude);
            int hour = (int)(GenDate.DayPercent(absTicks, longitude) * 24f);
            
            sb.AppendLine("- Date: " + day + " of " + quadrum.Label() + ", " + year);
            sb.AppendLine("- Time: " + hour.ToString("D2") + ":00");
            sb.AppendLine("- Weather: " + initiator.Map.weatherManager.curWeather.label);
            sb.AppendLine("- Location: " + SocialInteractions.GetBiomeInfo(initiator.Map));
        }
        
        private async Task<string> GetLLMResponse(string prompt)
        {
            // Use existing API client infrastructure
            var settings = SocialInteractions.Settings;
            string apiKey = settings.llmApiKey;

            // Prepare sampling parameters once
            int? topK = settings.llmTopK > 0 ? (int?)settings.llmTopK : null;
            float? topP = settings.llmTopP < 1.0f ? (float?)settings.llmTopP : null;
            float? minP = settings.llmMinP > 0.0f ? (float?)settings.llmMinP : null;
            float? repPen = settings.llmRepetitionPenalty != 1.0f ? (float?)settings.llmRepetitionPenalty : null;
            
            switch (settings.llmApiType)
            {
                case LlmApiType.KoboldCpp:
                    using (var client = new KoboldApiClient(settings.llmApiUrl, apiKey))
                    {
                        return await client.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                case LlmApiType.Ollama:
                    using (var client = new OllamaApiClient(settings.llmApiUrl, settings.ollamaModelName))
                    {
                        return await client.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                case LlmApiType.LMStudio:
                    using (var client = new LMStudioApiClient(settings.llmApiUrl, settings.lmStudioModelName))
                    {
                        return await client.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                case LlmApiType.OpenAI:
                    using (var client = new OpenAiApiClient(settings.llmApiUrl, settings.openAiModelName, apiKey))
                    {
                        return await client.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                case LlmApiType.Gemini:
                    using (var client = new GeminiApiClient(settings.llmApiUrl, apiKey))
                    {
                        return await client.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                case LlmApiType.Qwen:
                    using (var client = new QwenApiClient(settings.llmApiUrl, settings.qwenModelName, apiKey))
                    {
                        return await client.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                case LlmApiType.Deepseek:
                    using (var client = new DeepseekApiClient(settings.llmApiUrl, settings.deepseekModelName, apiKey))
                    {
                        return await client.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                case LlmApiType.Grok:
                    using (var client = new GrokApiClient(settings.llmApiUrl, settings.grokModelName, apiKey))
                    {
                        return await client.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                case LlmApiType.Claude:
                    using (var client = new ClaudeApiClient(settings.llmApiUrl, settings.claudeModelName, apiKey))
                    {
                        return await client.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                case LlmApiType.Player2:
                    using (var client = new Player2ApiClient(settings.llmApiUrl, settings.player2ModelName, apiKey, settings.player2GameClientId))
                    {
                        return await client.GenerateText(prompt, null, null, null, null, topK, topP, minP, repPen);
                    }
                default:
                    throw new Exception("Unknown API type: " + settings.llmApiType);
            }
        }
        
        private void ProcessLLMResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                SLog.Warning("[Negotiation] Received null or empty response from LLM.");
                if (isActive)
                {
                    HandleLLMFailure();
                }
                else
                {
                    EndNegotiation();
                }
                return;
            }

            try
            {
                SLog.Message("[Negotiation] Received response (" + (isActive ? "Active" : "Background") + "):\n" + response.Substring(0, Math.Min(500, response.Length)) + "...");
                
                // Check for outcome
                var outcomeMatch = Regex.Match(response, @"OUTCOME:\s*(CRITICAL_SUCCESS|POSITIVE|NEUTRAL|NEGATIVE)", RegexOptions.IgnoreCase);
                bool hasOutcome = outcomeMatch.Success;

                // User request: Only process outcome AFTER the first interaction.
                // This gives the player at least one chance to influence the result.
                if (hasOutcome && turnCount == 1)
                {
                    SLog.Message("[Negotiation] Outcome provided on turn 1 (" + outcomeMatch.Groups[1].Value + "). Suppressing terminal processing to allow at least one more interaction.");
                    hasOutcome = false;
                }

                NegotiationOutcome outcome = NegotiationOutcome.Neutral;

                if (hasOutcome)
                {
                    string outcomeStr = outcomeMatch.Groups[1].Value.ToUpper();
                    if (outcomeStr == "POSITIVE") outcome = NegotiationOutcome.Positive;
                    else if (outcomeStr == "NEGATIVE") outcome = NegotiationOutcome.Negative;
                    else if (outcomeStr == "CRITICAL_SUCCESS") outcome = NegotiationOutcome.CriticalSuccess;
                    else outcome = NegotiationOutcome.Neutral;
                    
                    // Extract dialogue
                    ExtractAndDisplayDialogue(response);

                    // Notify immediately for non-neutral outcomes
                    SendOutcomeNotification(outcome);

                    if (outcome == NegotiationOutcome.Negative)
                    {
                        MessageTypeDefOf.NegativeEvent.sound.PlayOneShotOnCamera(null);
                        
                        if (isActive)
                        {
                            dialog.AddConversationEntry("System", "<color=#FF4444>Negotiation Failed.</color>", false);
                            currentChoices.Clear();
                            dialog.SetChoices(currentChoices);
                        }

                        EndNegotiation(outcome);
                        return;
                    }
                    else if (outcome == NegotiationOutcome.Positive || outcome == NegotiationOutcome.CriticalSuccess)
                    {
                        MessageTypeDefOf.PositiveEvent.sound.PlayOneShotOnCamera(null);
                        
                        if (isActive)
                        {
                            string colorTag = "<color=#44FF44>";
                            string statusMsg = (outcome == NegotiationOutcome.CriticalSuccess) ? "Negotiation Critical Success!" : "Negotiation Successful!";
                            dialog.AddConversationEntry("System", colorTag + statusMsg + "</color> You may continue chatting.", false);
                        }
                        
                        pendingOutcome = outcome;
                        SendOutcomeNotification(outcome);
                    }
                }
                
                if (!hasOutcome)
                {
                    ExtractAndDisplayDialogue(response);
                }
                
                // If the window is still active, update choices
                if (isActive)
                {
                    if (IsInteractionLimitReached)
                    {
                        SLog.Message("[Negotiation] Interaction limit reached. Suppressing choices.");
                        currentChoices.Clear();
                        dialog.AddConversationEntry("System", "Conversation concluded. (Max turns reached)", false);
                    }
                    else
                    {
                        currentChoices = ExtractChoices(response);
                        if (currentChoices.Count == 0)
                        {
                            SLog.Warning("[Negotiation] No choices parsed, providing defaults");
                            currentChoices = GetDefaultChoices();
                        }
                    }
                    
                    SLog.Message("[Negotiation] Setting " + currentChoices.Count + " choices");
                    dialog.SetChoices(currentChoices);
                }
                else if (!isActive)
                {
                    // If window was closed (isActive == false), ALWAYS finalize
                    // Pass the current turn outcome if one was found, otherwise EndNegotiation 
                    // will prefer pendingOutcome or Neutral.
                    EndNegotiation(hasOutcome ? (NegotiationOutcome?)outcome : null);
                }
            }
            catch (Exception ex)
            {
                SLog.Error("[Negotiation] Error in ProcessLLMResponse: " + ex.Message);
                if (!isActive)
                {
                    EndNegotiation();
                }
                else
                {
                    HandleLLMFailure();
                }
            }
        }
        
        private void ExtractAndDisplayDialogue(string response)
        {
            // Parse all dialogue lines in order of appearance
            // Match lines like "Name: text" for both pawns
            string initiatorName = initiator.LabelShort;
            string targetName = target.LabelShort;
            
            // Use a single regex to find all dialogue lines, then determine speaker
            string pattern = @"(?:^|\n)([\w]+):\s*(.+?)(?=\n|$)";
            var allMatches = Regex.Matches(response, pattern, RegexOptions.IgnoreCase);
            
            var ttsBatch = new List<DialogueLine>();
            
            foreach (Match match in allMatches)
            {
                string speaker = match.Groups[1].Value.Trim();
                string text = match.Groups[2].Value.Trim();
                
                if (string.IsNullOrEmpty(text)) continue;
                
                // Skip if this looks like a keyword/instruction rather than dialogue
                if (speaker.ToUpper() == "CHOICES" || speaker.ToUpper() == "OUTCOME" ||
                    speaker.ToUpper() == "END_CHOICES" || speaker.ToUpper() == "FORMAT")
                {
                    continue;
                }
                
                // Determine if this is initiator or target
                bool isInitiator = speaker.Equals(initiatorName, StringComparison.OrdinalIgnoreCase);
                bool isTarget = speaker.Equals(targetName, StringComparison.OrdinalIgnoreCase);
                
                if (isInitiator || isTarget)
                {
                    Pawn speakerPawn = isInitiator ? initiator : target;
                    Pawn recipientPawn = isInitiator ? target : initiator;
                    
                    if (isActive)
                    {
                        dialog.AddConversationEntry(speaker, text, isInitiator);
                    }
                    conversationHistory.AppendLine(speaker + ": " + text);
                    
                    // Store for final display AND for TTS batching
                    var lineEntry = new DialogueLine(speakerPawn, recipientPawn, text);
                    lastDialogueLines.Add(lineEntry);
                    ttsBatch.Add(lineEntry);
                    
                    // Log to ChatLogManager
                    if (conversationId < 0)
                    {
                        conversationId = SpeechBubbleManager.StartConversation();
                    }
                    string fallbackText = string.Format("{0} negotiates with {1}.", speakerPawn.Name.ToStringShort, recipientPawn.Name.ToStringShort);
                    string loggedText = speaker + ": " + text;
                    ChatLogManager.AddMessage(new ChatMessage(speakerPawn, recipientPawn, loggedText, MessageType.LLMChat, conversationId, Color.white, fallbackText, loggedText));
                    
                    SLog.Message("[Negotiation] Added dialogue: " + speaker + ": " + text.Substring(0, Math.Min(50, text.Length)));
                }
            }

            // Start staggered TTS dispatch
            if (ttsBatch.Count > 0 && SocialInteractions.Settings.enableTTS && Current.Root != null)
            {
                ((MonoBehaviour)Current.Root).StartCoroutine(ProcessTTSBatch(ttsBatch));
            }
        }

        private IEnumerator ProcessTTSBatch(List<DialogueLine> batch)
        {
            foreach (var line in batch)
            {
                TTSManager.Speak(line.Text, line.Speaker, SocialInteractions.Settings.ttsSpeed, (int)SocialInteractions.Settings.ttsVolume);
                // Stagger requests by 200ms (realtime) to force FIFO processing on server/network
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }
        
        private List<string> ExtractChoices(string response)
        {
            var choices = new List<string>();
            
            // Look for CHOICES: ... END_CHOICES block
            var choicesMatch = Regex.Match(response, @"CHOICES:\s*\n([\s\S]*?)(?:END_CHOICES|OUTCOME:|$)", RegexOptions.IgnoreCase);
            if (choicesMatch.Success)
            {
                string choicesBlock = choicesMatch.Groups[1].Value;
                
                // Extract numbered choices
                var choiceMatches = Regex.Matches(choicesBlock, @"^\s*\d+\.\s*(.+?)$", RegexOptions.Multiline);
                foreach (Match match in choiceMatches)
                {
                    string choice = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(choice) && choices.Count < 3)
                    {
                        choices.Add(choice);
                    }
                }
            }
            
            return choices;
        }
        
        private List<string> GetDefaultChoices()
        {
            return new List<string>
            {
                "Continue the discussion",
                "Change the subject",
                "End the conversation"
            };
        }
        
        public static NegotiationOutcome RollSkillBasedOutcome(Pawn initiator)
        {
            if (initiator.skills == null) return NegotiationOutcome.Neutral;

            float socialLevel = initiator.skills.GetSkill(SkillDefOf.Social).Level;
            float normalizedLevel = Mathf.Clamp01(socialLevel / 20f);

            // Weights at Level 20: CriticalSuccess 5%, Positive 15%, Negative 5% (Baseline risk), Neutral 75%
            // Weights at Level 0: CriticalSuccess 0%, Positive 0%, Negative 50%, Neutral 50%
            float critChance = Mathf.Lerp(0f, 0.05f, normalizedLevel);
            float posChance = Mathf.Lerp(0f, 0.15f, normalizedLevel);
            float negChance = Mathf.Lerp(0.5f, 0.05f, normalizedLevel);

            float roll = Rand.Value;

            if (roll < critChance) return NegotiationOutcome.CriticalSuccess;
            if (roll < critChance + posChance) return NegotiationOutcome.Positive;
            if (roll > 1f - negChance) return NegotiationOutcome.Negative;
            
            return NegotiationOutcome.Neutral;
        }

        public static void ApplyUniversalOutcome(Pawn initiator, Pawn target, NegotiationOutcome outcome, Lord raidContext = null, bool isTradeContext = false, bool isVisitorContext = false)
        {
            // Apply generic mood/thought effects
            ApplyMoodOutcome(initiator, outcome);

            // Apply context-specific outcomes
            if (raidContext != null) ApplyRaidOutcomeStatic(raidContext, initiator, outcome);
            else if (isTradeContext) ApplyTradeOutcomeStatic(initiator, target, outcome);
            else if (isVisitorContext) ApplyVisitorOutcomeStatic(target, outcome, initiator);
            
            // Detect social fight context (can happen alongside colonist-to-colonist)
            if (target.MentalStateDef == MentalStateDefOf.SocialFighting)
            {
                MentalState_SocialFighting socialFight = target.MentalState as MentalState_SocialFighting;
                if (socialFight != null)
                {
                    Pawn otherPawn = socialFight.otherPawn;
                    if (otherPawn != null)
                    {
                        ApplySocialFightOutcomeStatic(target, otherPawn, outcome);
                    }
                }
            }

            // Detect general mental state context
            if (target.InMentalState && target.MentalStateDef != MentalStateDefOf.SocialFighting)
            {
                ApplyMentalStateOutcomeStatic(target, outcome);
            }

            // Set cooldown
            SetCooldownStatic(initiator, target, raidContext, isTradeContext);
        }

        private void HandleLLMFailure()
        {
            SLog.Warning("[Negotiation] LLM failed, using skill-based fallback");
            
            NegotiationOutcome outcome = RollSkillBasedOutcome(initiator);
            
            string description;
            switch (outcome)
            {
                case NegotiationOutcome.CriticalSuccess:
                    description = "The conversation was masterfully handled! An incredible breakthrough was achieved.";
                    break;
                case NegotiationOutcome.Positive:
                    description = "The conversation concluded on a positive note, reaching a favorable agreement.";
                    break;
                case NegotiationOutcome.Negative:
                    description = "The conversation went poorly and ended in a bitter disagreement.";
                    break;
                default:
                    description = "The conversation concluded awkwardly without any real resolution.";
                    break;
            }

            if (isActive)
            {
                dialog.AddConversationEntry("System", description, false);
            }
            
            EndNegotiation(outcome);
        }
        
        private void EndNegotiation(NegotiationOutcome? outcomeOverride = null)
        {
            // Determine final outcome, preferring explicit overrides or successful pending outcomes over Neutral
            NegotiationOutcome finalOutcome = NegotiationOutcome.Neutral;
            
            if (outcomeOverride.HasValue && (outcomeOverride.Value != NegotiationOutcome.Neutral || !pendingOutcome.HasValue))
            {
                finalOutcome = outcomeOverride.Value;
            }
            else if (pendingOutcome.HasValue)
            {
                finalOutcome = pendingOutcome.Value;
            }
            
            // Negative outcome always takes priority as it signifies a break in communication/faction hostility
            if (outcomeOverride == NegotiationOutcome.Negative || pendingOutcome == NegotiationOutcome.Negative)
            {
                finalOutcome = NegotiationOutcome.Negative;
            }
            
            SLog.Message("[Negotiation] EndNegotiation called. Override: " + outcomeOverride + ", Pending: " + pendingOutcome + " -> Final: " + finalOutcome);
            
            FinalizeNegotiation(finalOutcome);
            
            // Note: CloseDialog() is now called by Dialog_PawnNegotiation after the delay 
            // set by InitiateDelayedClose inside FinalizeNegotiation.
        }
        
        public void Cleanup()
        {
            // Called when window is closed
            
            // Immediately stop the "standing around" hediff so pawns are free
            // Move this out of if(isActive) to ensure it's always called when window closes
            RemoveNegotiatingHediff();

            if (isActive)
            {
                SLog.Message("[Negotiation] Cleanup called (Manual Close). Pending: " + pendingOutcome);
                isActive = false; // Stop UI updates
                
                // If NOT waiting for an LLM response, finalize with what we have
                if (!waitingForLLM)
                {
                    EndNegotiation();
                }
                else
                {
                    SLog.Message("[Negotiation] Still waiting for LLM response, background task will finalize later.");
                }
            }
        }
        
        private void FinalizeNegotiation(NegotiationOutcome outcome)
        {
            if (outcomeApplied)
            {
                SLog.Warning("[Negotiation] FinalizeNegotiation called but outcome was already applied. Ignored: " + outcome);
                return;
            }
            outcomeApplied = true;
            
            // Set isActive to false if it wasn't already (e.g. if ending normally)
            // But we might want it to stay true for the delayed close UI.
            // Let's rely on Cleanup to set it to false for manual close.
            
            SLog.Message("[Negotiation] Finalizing with outcome: " + outcome + " (Initiator: " + initiator.LabelShort + ")");
            
            // Send global notification message
            SendOutcomeNotification(outcome);
            
            // Apply outcome
            ApplyUniversalOutcome(initiator, target, outcome, raidContext, isTradeContext, isVisitorContext);

            if (isActive)
            {
                // Calculate total reading time for the final messages shown in the dialog
                float totalDuration = 0f;
                int startIndex = Math.Max(0, lastDialogueLines.Count - 2);
                for (int i = startIndex; i < lastDialogueLines.Count; i++)
                {
                    DialogueLine line = lastDialogueLines[i];
                    totalDuration += SpeechBubbleManager.EstimateReadingTime(line.Text);
                }
                
                // Initiate delayed close
                dialog.InitiateDelayedClose(totalDuration);
            }

            // End the conversation to clear the LLM busy state
            if (conversationId != -1)
            {
                SLog.Message("[Negotiation] Ending conversation ID: " + conversationId);
                SpeechBubbleManager.EndConversation(conversationId);
            }
        }
        
        private void ApplyNegotiatingHediff()
        {
            if (initiator.health != null && SI_HediffDefOf.SI_Negotiating != null)
            {
                Hediff hediff = HediffMaker.MakeHediff(SI_HediffDefOf.SI_Negotiating, initiator);
                initiator.health.AddHediff(hediff);
                SLog.Message("[Negotiation] Applied SI_Negotiating hediff to " + initiator.LabelShort);
            }
        }
        
        private void RemoveNegotiatingHediff()
        {
            if (initiator.health != null)
            {
                Hediff hediff = initiator.health.hediffSet.GetFirstHediffOfDef(SI_HediffDefOf.SI_Negotiating);
                if (hediff != null)
                {
                    initiator.health.RemoveHediff(hediff);
                    SLog.Message("[Negotiation] Removed SI_Negotiating hediff from " + initiator.LabelShort);
                }
            }
        }
        
        private void ApplyOutcome(NegotiationOutcome outcome)
        {
            ApplyMoodOutcome(initiator, outcome);
        }

        public static void ApplyMoodOutcome(Pawn initiator, NegotiationOutcome outcome)
        {
            if (initiator.needs == null) { SLog.Warning("[Negotiation] Cannot apply outcome: initiator.needs is null"); return; }
            if (initiator.needs.mood == null) { SLog.Warning("[Negotiation] Cannot apply outcome: initiator.needs.mood is null"); return; }
            if (initiator.needs.mood.thoughts == null) { SLog.Warning("[Negotiation] Cannot apply outcome: initiator.needs.mood.thoughts is null"); return; }
            if (initiator.needs.mood.thoughts.memories == null) { SLog.Warning("[Negotiation] Cannot apply outcome: initiator.needs.mood.thoughts.memories is null"); return; }
            
            SLog.Message("[Negotiation] Applying mood effects for outcome: " + outcome);
            
            switch (outcome)
            {
                case NegotiationOutcome.Positive:
                case NegotiationOutcome.CriticalSuccess:
                    if (SI_ThoughtDefOf.SI_NegotiationPositive != null)
                    {
                        initiator.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.SI_NegotiationPositive);
                        SLog.Message("[Negotiation] Applied positive thought to " + initiator.LabelShort);
                    }
                    break;
                case NegotiationOutcome.Negative:
                    if (SI_ThoughtDefOf.SI_NegotiationNegative != null)
                    {
                        initiator.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.SI_NegotiationNegative);
                        SLog.Message("[Negotiation] Applied negative thought to " + initiator.LabelShort);
                    }
                    break;
            }
        }
        
        private void SetCooldown()
        {
            SetCooldownStatic(initiator, target, raidContext, isTradeContext);
        }

        public static void SetCooldownStatic(Pawn initiator, Pawn target, Lord raidContext = null, bool isTradeContext = false)
        {
            if (Current.Game == null) return;
            var comp = Current.Game.GetComponent<NegotiationCooldown_GameComponent>();
            if (comp == null) return;

            float hours = SocialInteractions.Settings.negotiationCooldownHours;
            if (hours <= 0) return;

            if (raidContext != null)
            {
                // Raid: Set cooldown for the entire faction
                comp.SetCooldown(raidContext.faction, hours);
            }
            else if (isTradeContext || initiator.Faction != target.Faction)
            {
                // Trade or different faction visitor: Set cooldown for both the specific pawn and the faction
                comp.SetCooldown(target, hours);
                if (target.Faction != null)
                {
                    comp.SetCooldown(target.Faction, hours);
                }
            }
            else
            {
                // Internal colony negotiation: Set cooldown for the target pawn
                comp.SetCooldown(target, hours);
            }
        }
        
        /// <summary>
        /// Apply raid-specific outcome.
        /// </summary>
        private void ApplyRaidOutcome(NegotiationOutcome outcome)
        {
            ApplyRaidOutcomeStatic(raidContext, initiator, outcome);
        }

        public static void ApplyRaidOutcomeStatic(Lord raidContext, Pawn initiator, NegotiationOutcome outcome)
        {
            if (raidContext == null) return;
            
            // Map NegotiationOutcome to NegotiatedRaidOutcome
            NegotiatedRaidOutcome raidOutcome;
            switch (outcome)
            {
                case NegotiationOutcome.CriticalSuccess:
                    raidOutcome = NegotiatedRaidOutcome.CriticalSuccess;
                    break;
                case NegotiationOutcome.Positive:
                    raidOutcome = NegotiatedRaidOutcome.Positive;
                    break;
                case NegotiationOutcome.Negative:
                    raidOutcome = NegotiatedRaidOutcome.Failure;
                    break;
                default:
                    raidOutcome = NegotiatedRaidOutcome.Neutral;
                    break;
            }
            
            // Apply the raid outcome
            RaidOutcomeUtility.ApplyRaidOutcome(raidContext, raidOutcome);
            
            // Clear the raid context
            RaidNegotiationContext.ClearActiveRaid(initiator);
        }
        
        /// <summary>
        /// Apply trade-specific outcome.
        /// </summary>
        private void ApplyTradeOutcome(NegotiationOutcome outcome)
        {
            ApplyTradeOutcomeStatic(initiator, target, outcome);
        }

        public static void ApplyTradeOutcomeStatic(Pawn initiator, Pawn target, NegotiationOutcome outcome)
        {
            switch (outcome)
            {
                case NegotiationOutcome.Positive:
                case NegotiationOutcome.CriticalSuccess:
                    if (initiator.mindState == null || initiator.mindState.inspirationHandler == null) break;
                    
                    // Grant Trade Inspiration
                    if (InspirationDefOf.Inspired_Trade != null)
                    {
                        if (initiator.mindState.inspirationHandler.TryStartInspiration(InspirationDefOf.Inspired_Trade))
                        {
                            Messages.Message(initiator.LabelShort + " has gained a terminal case of trading inspiration from the discussion!", initiator, MessageTypeDefOf.PositiveEvent);
                            SLog.Message("[Negotiation] Applied trade inspiration to " + initiator.LabelShort);
                        }
                    }
                    break;
                    
                case NegotiationOutcome.Negative:
                    // Find the lord and make them leave
                    Lord lord = target.GetLord();
                    if (lord != null)
                    {
                        Messages.Message("SI_TraderLeavingNegative".Translate(target.LabelShort), target, MessageTypeDefOf.NegativeEvent);
                        
                        // Set the dismissal flag on the main trader of the caravan
                        // This triggers the vanilla dismissal logic in LordJob_TradeWithColony
                        Pawn trader = TraderCaravanUtility.FindTrader(lord);
                        if (trader != null && trader.mindState != null)
                        {
                            trader.mindState.traderDismissed = true;
                            SLog.Message("[Negotiation] Set traderDismissed = true for " + trader.LabelShort + " (Caravan Leader).");
                        }
                        else
                        {
                            // Fallback for non-caravan traders (e.g. single travelers)
                            lord.ReceiveMemo("TravelerJoyDone");
                            SLog.Message("[Negotiation] No specific career trader found, sent TravelerJoyDone memo.");
                        }
                        
                        SLog.Message("[Negotiation] Trader " + target.LabelShort + " group is leaving due to negative outcome.");
                    }
                    break;
            }
        }

        /// <summary>
        /// Apply visitor-specific outcome.
        /// </summary>
        private void ApplyVisitorOutcome(NegotiationOutcome outcome)
        {
            ApplyVisitorOutcomeStatic(target, outcome, initiator);
        }

        public static void ApplyVisitorOutcomeStatic(Pawn target, NegotiationOutcome outcome, Pawn initiator = null)
        {
            switch (outcome)
            {
                case NegotiationOutcome.CriticalSuccess:
                    if (initiator != null)
                    {
                        // Trigger join request
                        TriggerJoinRequestStatic(target, initiator);
                    }
                    else
                    {
                        SLog.Warning("[Negotiation] Cannot trigger join request: initiator is null");
                    }
                    goto case NegotiationOutcome.Positive;

                case NegotiationOutcome.Positive:
                    if (target.Faction != null && !target.Faction.IsPlayer && !target.Faction.HostileTo(Faction.OfPlayer))
                    {
                        int amount = (outcome == NegotiationOutcome.CriticalSuccess) ? 12 : 6;
                        target.Faction.TryAffectGoodwillWith(Faction.OfPlayer, amount, true, true, null);
                        SLog.Message("[Negotiation] Faction " + target.Faction.Name + " goodwill improved by " + amount);
                    }
                    break;
                    
                case NegotiationOutcome.Negative:
                    if (target.Faction != null && !target.Faction.IsPlayer && !target.Faction.HostileTo(Faction.OfPlayer))
                    {
                        int amount = 6;
                        target.Faction.TryAffectGoodwillWith(Faction.OfPlayer, -amount, true, true, null);
                        Messages.Message("SI_VisitorGoodwillReduced".Translate(target.Faction.Name, amount), target, MessageTypeDefOf.NegativeEvent);
                        SLog.Message("[Negotiation] Faction " + target.Faction.Name + " goodwill reduced by " + amount + " due to negative outcome with " + target.LabelShort);
                    }
                    break;
                    
                default:
                    break;
            }
        }

        public static void TriggerJoinRequestStatic(Pawn target, Pawn solicitor)
        {
            try
            {
                LetterDef joinLetterDef = DefDatabase<LetterDef>.GetNamed("SI_JoinRequest", false);
                if (joinLetterDef == null)
                {
                    SLog.Warning("[Negotiation] Could not find LetterDef SI_JoinRequest. Fallback to message.");
                    Messages.Message("SI_JoinRequestLabel".Translate(target.LabelShort), target, MessageTypeDefOf.PositiveEvent);
                    return;
                }

                SI_JoinRequestLetter letter = (SI_JoinRequestLetter)LetterMaker.MakeLetter(joinLetterDef);
                letter.joiner = target;
                letter.Label = "SI_JoinRequestLabel".Translate(target.LabelShort);
                letter.Text = "SI_JoinRequestText".Translate(target.LabelShort, solicitor.LabelShort);
                letter.lookTargets = target;

                Find.LetterStack.ReceiveLetter(letter);
                SLog.Message("[Negotiation] Sent join request letter for " + target.LabelShort);
            }
            catch (Exception ex)
            {
                SLog.Error("[Negotiation] Failed to trigger join request: " + ex);
            }
        }

        public static void ApplyMentalStateOutcomeStatic(Pawn target, NegotiationOutcome outcome)
        {
            if (outcome == NegotiationOutcome.Positive || outcome == NegotiationOutcome.CriticalSuccess)
            {
                if (target.InMentalState)
                {
                    string mentalStateName = target.MentalStateDef != null ? target.MentalStateDef.defName : "unknown state";
                    target.mindState.mentalStateHandler.CurState.RecoverFromState();
                    SLog.Message("[Negotiation] " + target.LabelShort + " recovered from " + mentalStateName + " through negotiation.");
                }

                // Apply mood buff for critical success
                if (outcome == NegotiationOutcome.CriticalSuccess && SI_ThoughtDefOf.SI_NegotiationPositive != null)
                {
                    if (target.needs != null && target.needs.mood != null && target.needs.mood.thoughts != null && target.needs.mood.thoughts.memories != null)
                    {
                        target.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.SI_NegotiationPositive);
                        SLog.Message("[Negotiation] Applied SI_NegotiationPositive critical success mood buff to " + target.LabelShort);
                    }
                }
            }
            else if (outcome == NegotiationOutcome.Negative)
            {
                if (target.InMentalState && target.MentalStateDef != MentalStateDefOf.Berserk)
                {
                    target.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, "Negotiation failed spectacularly.", forced: true);
                    SLog.Message("[Negotiation] " + target.LabelShort + " became BERSERK due to failed negotiation.");
                }
            }
        }

        public static void ApplySocialFightOutcomeStatic(Pawn target, Pawn otherFighter, NegotiationOutcome outcome)
        {
            if (outcome == NegotiationOutcome.Positive || outcome == NegotiationOutcome.CriticalSuccess)
            {
                // Stop the fight
                if (target.MentalStateDef == MentalStateDefOf.SocialFighting)
                {
                    target.mindState.mentalStateHandler.CurState.RecoverFromState();
                    SLog.Message("[Negotiation] Stopped social fight for " + target.LabelShort);
                }
                if (otherFighter.MentalStateDef == MentalStateDefOf.SocialFighting)
                {
                    otherFighter.mindState.mentalStateHandler.CurState.RecoverFromState();
                    SLog.Message("[Negotiation] Stopped social fight for " + otherFighter.LabelShort);
                }

                if (outcome == NegotiationOutcome.CriticalSuccess)
                {
                    // Reconcile relationship
                    if (SI_ThoughtDefOf.FoundCommonGround != null)
                    {
                        if (target.needs != null && target.needs.mood != null && target.needs.mood.thoughts != null && target.needs.mood.thoughts.memories != null)
                        {
                            target.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.FoundCommonGround, otherFighter);
                        }
                        if (otherFighter.needs != null && otherFighter.needs.mood != null && otherFighter.needs.mood.thoughts != null && otherFighter.needs.mood.thoughts.memories != null)
                        {
                            otherFighter.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.FoundCommonGround, target);
                        }
                        SLog.Message("[Negotiation] Applied FoundCommonGround reconciliation between " + target.LabelShort + " and " + otherFighter.LabelShort);
                    }
                }
            }
            else if (outcome == NegotiationOutcome.Negative)
            {
                // On negative outcome for brawl breakup, target goes berserk
                if (target.InMentalState && target.MentalStateDef != MentalStateDefOf.Berserk)
                {
                    target.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, "Negotiation failed spectacularly.", forced: true);
                    SLog.Message("[Negotiation] " + target.LabelShort + " became BERSERK due to failed brawl breakup negotiation.");
                }
            }
        }

        private void SendOutcomeNotification(NegotiationOutcome outcome)
        {
            if (outcome == NegotiationOutcome.Neutral) return;
            if (lastNotifiedOutcome == outcome) return;
            lastNotifiedOutcome = outcome;

            string outcomeLabel = GetOutcomeLabel(outcome);
            string message;
            MessageTypeDef messageType = (outcome == NegotiationOutcome.Negative) ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.PositiveEvent;
            if (outcome == NegotiationOutcome.Neutral) messageType = MessageTypeDefOf.NeutralEvent;

            if (raidContext != null)
            {
                string factionName = (raidContext.faction != null) ? raidContext.faction.Name : "Unknown Faction";
                message = string.Format("Negotiation with {0} leader {1}: {2}", factionName, target.LabelShort, outcomeLabel);
            }
            else if (isTradeContext)
            {
                message = string.Format("Trade negotiation with {0}: {1}", target.LabelShort, outcomeLabel);
            }
            else if (isVisitorContext)
            {
                string factionName = (target.Faction != null) ? target.Faction.Name : "Unknown Faction";
                message = string.Format("Negotiation with {0} of {1}: {2}", target.LabelShort, factionName, outcomeLabel);
            }
            else if (isSocialFightContext)
            {
                string otherName = (otherFighter != null) ? otherFighter.LabelShort : "someone";
                message = string.Format("Negotiation to stop the fight between {0} and {1}: {2}", target.LabelShort, otherName, outcomeLabel);
            }
            else if (isMentalStateContext)
            {
                string stateLabel = (target.MentalStateDef != null) ? (string)target.MentalStateDef.LabelCap : "mental break";
                message = string.Format("Negotiation to snap {0} out of {1}: {2}", target.LabelShort, stateLabel, outcomeLabel);
            }
            else
            {
                message = string.Format("Negotiation between {0} and {1}: {2}", initiator.LabelShort, target.LabelShort, outcomeLabel);
            }

            Messages.Message(message, target, messageType);
            SLog.Message("[Negotiation] Notification sent: " + message);
        }

        private string GetOutcomeLabel(NegotiationOutcome outcome)
        {
            switch (outcome)
            {
                case NegotiationOutcome.Positive: return "Successful";
                case NegotiationOutcome.CriticalSuccess: return "Critical Success";
                case NegotiationOutcome.Negative: return "Failed";
                case NegotiationOutcome.Neutral: return "Neutral";
                default: return outcome.ToString();
            }
        }
    }
    
    public enum NegotiationOutcome
    {
        Positive,
        CriticalSuccess,
        Neutral,
        Negative
    }

    /// <summary>
    /// Helper class to store dialogue lines for final display
    /// </summary>
    public class DialogueLine
    {
        public Pawn Speaker { get; set; }
        public Pawn Recipient { get; set; }
        public string Text { get; set; }
        
        public DialogueLine(Pawn speaker, Pawn recipient, string text)
        {
            Speaker = speaker;
            Recipient = recipient;
            Text = text;
        }
    }
}
