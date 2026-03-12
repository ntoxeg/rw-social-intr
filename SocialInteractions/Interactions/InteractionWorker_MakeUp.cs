using RimWorld;
using Verse;
using Verse.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SocialInteractions
{
    /// <summary>
    /// Interaction worker for make-up/apologizing interactions that allow pawns to clear up misunderstandings
    /// and remove negative modifiers from backstabbing or other negative interactions
    /// </summary>
    public class InteractionWorker_MakeUp : InteractionWorker
    {
        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            // Initialize out parameters
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = LookTargets.Invalid;

            // Add null checks to prevent exceptions
            if (initiator == null || recipient == null)
            {
                SLog.Warning("[SocialInteractions] InteractionWorker_MakeUp: Initiator or recipient is null, skipping interaction.");
                // Initialize output parameters and return early
                letterText = null;
                letterLabel = null;
                letterDef = null;
                lookTargets = LookTargets.Invalid;
                return;
            }

            // Check if the recipient has negative thoughts about the initiator (from backstabbing or other negative interactions)
            bool hasNegativeModifier = HasNegativeModifierFromBackstab(initiator, recipient);

            if (!hasNegativeModifier)
            {
                // If no significant negative modifier exists, just call the base method
                base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
                return;
            }

            // Attempt to reconcile based on social skill
            bool reconciliationSuccessful = AttemptReconciliation(initiator, recipient);

            // Apply appropriate thoughts based on the outcome
            ApplyReconciliationThoughts(initiator, recipient, reconciliationSuccessful);

            // Generate an appropriate subject based on the outcome
            string subject = GenerateMakeUpSubject(initiator, recipient, reconciliationSuccessful);

            // Handle the LLM interaction with the generated subject
            SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.MakeUp, subject, true);

            // Add topic-specific sentence rulepacks based on the outcome
            if (extraSentencePacks == null)
            {
                extraSentencePacks = new List<RulePackDef>();
            }

            if (reconciliationSuccessful)
            {
                // Add rulepack for successful reconciliation
                RulePackDef topicRulePack = DefDatabase<RulePackDef>.GetNamedSilentFail("MakeUpSuccessfulReconciliation");
                if (topicRulePack != null)
                {
                    extraSentencePacks.Add(topicRulePack);
                }
                // If rulepack doesn't exist, we just won't add a specific topic (which is fine)
            }
            else
            {
                // Add rulepack for failed reconciliation
                RulePackDef topicRulePack = DefDatabase<RulePackDef>.GetNamedSilentFail("MakeUpFailedReconciliation");
                if (topicRulePack != null)
                {
                    extraSentencePacks.Add(topicRulePack);
                }
                // If rulepack doesn't exist, we just won't add a specific topic (which is fine)
            }

            // Call the base Interacted method to create the normal log entry using XML rules
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);

            // Create a custom log entry for the make-up interaction to ensure it's properly recorded in social history
            try
            {
                PlayLogEntry_MakeUp makeupLogEntry = new PlayLogEntry_MakeUp(SI_InteractionDefOf.MakeUp, initiator, recipient, extraSentencePacks, reconciliationSuccessful);

                // Add the entry to the play log to update the social history
                if (Find.PlayLog != null)
                {
                    Find.PlayLog.Add(makeupLogEntry);
                }
            }
            catch (System.Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] InteractionWorker_MakeUp: Failed to add make-up to play log: {0}", ex.Message));
            }

            // Log the interaction
            SLog.Message(string.Format("[SocialInteractions] Make-up interaction: {0} attempted reconciliation with {1}, success: {2}",
                initiator.LabelShort, recipient.LabelShort, reconciliationSuccessful));
        }

        /// <summary>
        /// Checks if the recipient has negative thoughts about the initiator from backstabbing or other significant negative interactions
        /// </summary>
        private bool HasNegativeModifierFromBackstab(Pawn initiator, Pawn recipient)
        {
            if (recipient.needs == null || recipient.needs.mood == null || recipient.needs.mood.thoughts == null)
            {
                return false;
            }

            // Check for specific backstab-related thoughts or other negative thoughts that originated from the initiator
            List<Thought_Memory> thoughtsList = recipient.needs.mood.thoughts.memories.Memories;
            foreach (Thought_Memory thought in thoughtsList)
            {
                if (thought.otherPawn == initiator)
                {
                    // Check if thought is negative and significant enough to warrant reconciliation
                    if (thought.def.stages != null && thought.def.stages.Count > 0)
                    {
                        int opinionOffset = thought.CurStageIndex < thought.def.stages.Count ? (int)thought.def.stages[thought.CurStageIndex].baseOpinionOffset : 0;
                        if (opinionOffset < -10) // If the thought creates a significant negative opinion offset
                        {
                            return true;
                        }
                    }
                    else
                    {
                        // For thoughts without stages, check the base mood effect
                        // Actually, most thoughts with opinion effects have stages, so if they don't have stages
                        // they might just be mood thoughts rather than opinion thoughts
                        // We'll just return false for these since they don't modify opinion
                        return false;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to reconcile based on the initiator's social skill level
        /// </summary>
        private bool AttemptReconciliation(Pawn initiator, Pawn recipient)
        {
            // Get the initiator's social skill level
            int socialSkillLevel = 0;
            if (initiator.skills != null)
            {
                socialSkillLevel = initiator.skills.GetSkill(SkillDefOf.Social).Level;
            }

            // Calculate success chance based on social skill (higher skill = higher chance)
            // Base chance of 30% + 5% per social skill level (so at level 20, 130% which caps at 95%)
            float baseChance = 0.3f;
            float skillBonus = socialSkillLevel * 0.05f;
            float successChance = baseChance + skillBonus;
            successChance = Math.Min(0.95f, successChance); // Cap at 95% to avoid guaranteed success

            // Check if recipient has traits that make forgiveness easier or harder
            successChance *= GetTraitBasedModifier(recipient);

            // Roll for success
            return Rand.Value < successChance;
        }

        /// <summary>
        /// Gets trait-based modifiers that affect how receptive the recipient is to apologies
        /// </summary>
        private float GetTraitBasedModifier(Pawn recipient)
        {
            if (recipient.story == null || recipient.story.traits == null)
            {
                return 1.0f;
            }

            // Check for traits that make the recipient more forgiving
            foreach (Trait trait in recipient.story.traits.allTraits)
            {
                if (trait != null && trait.def != null)
                {
                    string traitLabel = trait.def.defName.ToLower();
                    string traitLabelDisplay = trait.Label.ToLower();

                    // Forgiving traits increase success chance
                    if (traitLabel.Contains("kind") ||
                        traitLabel.Contains("forgiving") ||
                        traitLabelDisplay.Contains("kind") ||
                        traitLabelDisplay.Contains("forgiving"))
                    {
                        return 1.5f; // 50% more likely to succeed with forgiving recipients
                    }
                    // Vengeful traits decrease success chance
                    else if (traitLabel.Contains("jealous") ||
                             traitLabel.Contains("psychopath") ||
                             traitLabelDisplay.Contains("jealous") ||
                             traitLabelDisplay.Contains("psychopath"))
                    {
                        return 0.5f; // 50% less likely to succeed with vengeful recipients
                    }
                }
            }

            return 1.0f;
        }

        /// <summary>
        /// Applies appropriate thoughts based on whether reconciliation was successful
        /// </summary>
        private void ApplyReconciliationThoughts(Pawn initiator, Pawn recipient, bool successful)
        {
            if (successful)
            {
                // Success! Remove or reduce negative thoughts about the initiator
                RemoveOrReduceNegativeThoughts(initiator, recipient);

                // Apply positive thoughts for making amends
                if (recipient.needs != null && recipient.needs.mood != null)
                {
                    ThoughtDef madeUpThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("ReconciledWith");
                    if (madeUpThought != null)
                    {
                        recipient.needs.mood.thoughts.memories.TryGainMemory(madeUpThought, initiator);
                    }
                    else
                    {
                        // Fallback to a general positive thought
                        ThoughtDef acceptedApology = DefDatabase<ThoughtDef>.GetNamedSilentFail("AppreciatedReconciliation");
                        if (acceptedApology != null)
                        {
                            recipient.needs.mood.thoughts.memories.TryGainMemory(acceptedApology, initiator);
                        }
                        else
                        {
                            // Further fallback to a similar thought
                            ThoughtDef fallbackThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("SocialRecreationPartner");
                            if (fallbackThought != null)
                            {
                                recipient.needs.mood.thoughts.memories.TryGainMemory(fallbackThought, initiator);
                            }
                            else
                            {
                                // Even further fallback to a positive social thought that is more likely to exist
                                ThoughtDef socialThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("KindWords");
                                if (socialThought != null)
                                {
                                    recipient.needs.mood.thoughts.memories.TryGainMemory(socialThought, initiator);
                                }
                                else
                                {
                                    // Ultimate fallback - just use a simple chitchat thought if KindWords isn't available either
                                    ThoughtDef chitchatThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Chitchat");
                                    if (chitchatThought != null)
                                    {
                                        recipient.needs.mood.thoughts.memories.TryGainMemory(chitchatThought, initiator);
                                    }
                                }
                            }
                        }
                    }
                }

                // Initiator also gets a positive thought for successfully making amends
                if (initiator.needs != null && initiator.needs.mood != null)
                {
                    ThoughtDef successfulApology = DefDatabase<ThoughtDef>.GetNamedSilentFail("SuccessfulReconciliation");
                    if (successfulApology != null)
                    {
                        initiator.needs.mood.thoughts.memories.TryGainMemory(successfulApology, recipient);
                    }
                }
            }
            else
            {
                // Failed reconciliation - may worsen relationship or have no effect
                // Could add a failed apology thought here if desired
                // For now, just keep the existing negative thoughts
            }
        }

        /// <summary>
        /// Removes or reduces negative thoughts the recipient has about the initiator
        /// </summary>
        private void RemoveOrReduceNegativeThoughts(Pawn initiator, Pawn recipient)
        {
            if (recipient.needs == null || recipient.needs.mood == null || recipient.needs.mood.thoughts == null)
            {
                return;
            }

            // Create a list of negative thoughts to process
            List<Thought_Memory> negativeThoughts = new List<Thought_Memory>();

            List<Thought_Memory> thoughtsList = recipient.needs.mood.thoughts.memories.Memories;
            foreach (Thought_Memory thought in thoughtsList)
            {
                if (thought.otherPawn == initiator)
                {
                    int opinionOffset = 0;
                    if (thought.def.stages != null && thought.def.stages.Count > 0 && thought.CurStageIndex < thought.def.stages.Count)
                    {
                        opinionOffset = (int)thought.def.stages[thought.CurStageIndex].baseOpinionOffset;
                    }
                    if (opinionOffset < 0) // Negative thoughts
                    {
                        negativeThoughts.Add(thought);
                    }
                }
            }

            // Process each negative thought - either reduce its effect or remove it
            foreach (Thought_Memory thought in negativeThoughts)
            {
                // For now, we'll remove the thought completely on successful reconciliation
                // In the future, we could implement reducing the intensity instead
                recipient.needs.mood.thoughts.memories.RemoveMemory(thought);
            }
        }

        /// <summary>
        /// Generate an appropriate subject for the LLM based on the outcome
        /// </summary>
        private string GenerateMakeUpSubject(Pawn initiator, Pawn recipient, bool successful)
        {
            if (successful)
            {
                return string.Format("{0} is trying to apologize to {1} and clear up misunderstandings. {1} accepts {0}'s apology",
                    initiator.LabelShort, recipient.LabelShort);
            }
            else
            {
                return string.Format("{0} is trying to apologize to {1} but is struggling to clear up misunderstandings.",
                    initiator.LabelShort, recipient.LabelShort);
            }
        }
    }
}