using RimWorld;
using Verse;
using Verse.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SocialInteractions
{
    /// <summary>
    /// Outcome types for a lover's quarrel
    /// </summary>
    public enum QuarrelOutcome
    {
        Reconciliation, // They make up and feel better
        Neutral,        // Standard argument, small debuff
        NearBreakup     // Severe argument, potential breakup
    }

    /// <summary>
    /// Interaction worker for lover's quarrel - triggered when romantic partners would normally insult each other
    /// </summary>
    public class InteractionWorker_LoversQuarrel : InteractionWorker
    {
        private const float EARSHOT_RADIUS = 12f;
        
        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            // Initialize out parameters
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = LookTargets.Invalid;

            // Add null checks
            if (initiator == null || recipient == null)
            {
                SLog.Warning("[SocialInteractions] InteractionWorker_LoversQuarrel: Initiator or recipient is null, skipping interaction.");
                return;
            }

            // Determine the quarrel outcome upfront
            QuarrelOutcome outcome = DetermineQuarrelOutcome(initiator, recipient);
            
            // Generate LLM subject with the outcome included
            string subject = GenerateQuarrelSubject(initiator, recipient, outcome);
            
            // Apply thoughts to both participants based on outcome
            ApplyParticipantThoughts(initiator, recipient, outcome);
            
            // Apply witness thoughts to nearby pawns
            ApplyWitnessThoughts(initiator, recipient);
            
            // Handle potential breakup on NearBreakup outcome
            if (outcome == QuarrelOutcome.NearBreakup)
            {
                TryTriggerBreakup(initiator, recipient);
            }
            
            // Handle the LLM interaction
            SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.LoversQuarrel, subject);
            
            // Call base Interacted method to create normal log entry
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            
            SLog.Message(string.Format("[SocialInteractions] Lover's quarrel: {0} and {1} had a {2} outcome", 
                initiator.LabelShort, recipient.LabelShort, outcome.ToString()));
        }

        /// <summary>
        /// Determines the outcome of the quarrel based on various factors
        /// </summary>
        private QuarrelOutcome DetermineQuarrelOutcome(Pawn initiator, Pawn recipient)
        {
            float reconciliationChance = 0.3f; // Base 30% chance of reconciliation
            float nearBreakupChance = 0.15f;   // Base 15% chance of near-breakup

            // Modify based on initiator's mood
            if (initiator.needs != null && initiator.needs.mood != null)
            {
                float mood = initiator.needs.mood.CurLevelPercentage;
                if (mood > 0.6f)
                {
                    reconciliationChance += 0.15f;
                    nearBreakupChance -= 0.05f;
                }
                else if (mood < 0.3f)
                {
                    reconciliationChance -= 0.1f;
                    nearBreakupChance += 0.1f;
                }
            }

            // Modify based on recipient's mood
            if (recipient.needs != null && recipient.needs.mood != null)
            {
                float mood = recipient.needs.mood.CurLevelPercentage;
                if (mood > 0.6f)
                {
                    reconciliationChance += 0.1f;
                }
                else if (mood < 0.3f)
                {
                    nearBreakupChance += 0.05f;
                }
            }

            // Modify based on mutual opinion
            if (initiator.relations != null && recipient.relations != null)
            {
                int initiatorOpinion = initiator.relations.OpinionOf(recipient);
                int recipientOpinion = recipient.relations.OpinionOf(initiator);
                int averageOpinion = (initiatorOpinion + recipientOpinion) / 2;

                if (averageOpinion > 50)
                {
                    reconciliationChance += 0.2f;
                    nearBreakupChance -= 0.1f;
                }
                else if (averageOpinion < 0)
                {
                    reconciliationChance -= 0.15f;
                    nearBreakupChance += 0.15f;
                }
            }

            // Kind pawns more likely to reconcile
            if (HasKindTrait(initiator) || HasKindTrait(recipient))
            {
                reconciliationChance += 0.2f;
            }

            // Abrasive pawns more likely to make things worse
            if (HasAbrasiveTrait(initiator))
            {
                nearBreakupChance += 0.15f;
                reconciliationChance -= 0.1f;
            }

            // Clamp chances
            reconciliationChance = Math.Max(0.05f, Math.Min(0.7f, reconciliationChance));
            nearBreakupChance = Math.Max(0.05f, Math.Min(0.35f, nearBreakupChance));

            // Roll for outcome
            float roll = Rand.Value;
            if (roll < reconciliationChance)
            {
                return QuarrelOutcome.Reconciliation;
            }
            else if (roll > (1f - nearBreakupChance))
            {
                return QuarrelOutcome.NearBreakup;
            }
            else
            {
                return QuarrelOutcome.Neutral;
            }
        }

        /// <summary>
        /// Generates the subject for LLM prompt with outcome information
        /// </summary>
        private string GenerateQuarrelSubject(Pawn initiator, Pawn recipient, QuarrelOutcome outcome)
        {
            string relationshipType = GetRelationshipType(initiator, recipient);
            
            switch (outcome)
            {
                case QuarrelOutcome.Reconciliation:
                    return string.Format("{0} and {1} ({2}) are having a heated argument, but they find common ground and reconcile. The tension melts away as they remember why they care about each other.",
                        initiator.LabelShort, recipient.LabelShort, relationshipType);
                        
                case QuarrelOutcome.NearBreakup:
                    return string.Format("{0} and {1} ({2}) are having a fierce, emotionally charged argument. Harsh words are exchanged, and the relationship feels like it's on the edge of breaking apart. The fight is intense and deeply hurtful.",
                        initiator.LabelShort, recipient.LabelShort, relationshipType);
                        
                case QuarrelOutcome.Neutral:
                default:
                    return string.Format("{0} and {1} ({2}) are having a typical couple's quarrel. Voices are raised and frustrations are aired, but neither side gains ground. They'll need time to cool off.",
                        initiator.LabelShort, recipient.LabelShort, relationshipType);
            }
        }

        /// <summary>
        /// Gets a human-readable relationship type string
        /// </summary>
        private string GetRelationshipType(Pawn pawn1, Pawn pawn2)
        {
            if (pawn1.relations == null) return "partners";
            
            if (pawn1.relations.DirectRelationExists(PawnRelationDefOf.Spouse, pawn2))
                return "spouses";
            if (pawn1.relations.DirectRelationExists(PawnRelationDefOf.Fiance, pawn2))
                return "engaged";
            if (pawn1.relations.DirectRelationExists(PawnRelationDefOf.Lover, pawn2))
                return "lovers";
                
            return "partners";
        }

        /// <summary>
        /// Applies mood thoughts to both participants based on the quarrel outcome
        /// </summary>
        private void ApplyParticipantThoughts(Pawn initiator, Pawn recipient, QuarrelOutcome outcome)
        {
            ThoughtDef thoughtDef = null;
            
            switch (outcome)
            {
                case QuarrelOutcome.Reconciliation:
                    thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("LoversQuarrel_Reconciled");
                    break;
                case QuarrelOutcome.NearBreakup:
                    thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("LoversQuarrel_NearBreakup");
                    break;
                case QuarrelOutcome.Neutral:
                default:
                    thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("LoversQuarrel_Participant");
                    break;
            }

            // Apply to both pawns
            if (thoughtDef != null)
            {
                if (initiator.needs != null && initiator.needs.mood != null && initiator.needs.mood.thoughts != null && initiator.needs.mood.thoughts.memories != null)
                {
                    initiator.needs.mood.thoughts.memories.TryGainMemory(thoughtDef, recipient);
                }
                if (recipient.needs != null && recipient.needs.mood != null && recipient.needs.mood.thoughts != null && recipient.needs.mood.thoughts.memories != null)
                {
                    recipient.needs.mood.thoughts.memories.TryGainMemory(thoughtDef, initiator);
                }
            }
        }

        /// <summary>
        /// Applies mood debuffs to nearby pawns who witnessed the quarrel
        /// </summary>
        private void ApplyWitnessThoughts(Pawn initiator, Pawn recipient)
        {
            if (initiator.Map == null) return;

            ThoughtDef witnessThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("WitnessedLoversQuarrel");
            ThoughtDef parentWitnessThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("WitnessedParentQuarrel");
            
            if (witnessThought == null) return;

            IntVec3 centerPos = initiator.Position;
            
            foreach (Pawn witness in initiator.Map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (witness == null || witness == initiator || witness == recipient)
                    continue;
                    
                if (!witness.Spawned || witness.Dead || witness.Downed)
                    continue;

                // Check if within earshot radius
                float distance = witness.Position.DistanceTo(centerPos);
                if (distance > EARSHOT_RADIUS)
                    continue;

                // Check if this witness is a child of either participant
                bool isChildOfParticipant = IsChildOf(witness, initiator) || IsChildOf(witness, recipient);
                
                ThoughtDef thoughtToApply = isChildOfParticipant && parentWitnessThought != null 
                    ? parentWitnessThought 
                    : witnessThought;
                
                if (witness.needs != null && witness.needs.mood != null && witness.needs.mood.thoughts != null && witness.needs.mood.thoughts.memories != null)
                {
                    witness.needs.mood.thoughts.memories.TryGainMemory(thoughtToApply);
                    
                    SLog.Message(string.Format("[SocialInteractions] {0} witnessed lover's quarrel between {1} and {2}{3}",
                        witness.LabelShort, initiator.LabelShort, recipient.LabelShort,
                        isChildOfParticipant ? " (their parent)" : ""));
                }
            }
        }

        /// <summary>
        /// Checks if the potential child is a child of the potential parent
        /// </summary>
        private bool IsChildOf(Pawn potentialChild, Pawn potentialParent)
        {
            if (potentialChild.relations == null) return false;
            return potentialChild.relations.DirectRelationExists(PawnRelationDefOf.Parent, potentialParent);
        }

        /// <summary>
        /// Attempts to trigger a breakup on severe quarrel outcomes (small chance)
        /// </summary>
        private void TryTriggerBreakup(Pawn initiator, Pawn recipient)
        {
            // 10% chance of actual breakup on near-breakup outcome
            if (Rand.Value < 0.1f)
            {
                SLog.Message(string.Format("[SocialInteractions] Lover's quarrel resulted in breakup: {0} and {1}",
                    initiator.LabelShort, recipient.LabelShort));
                
                // Try to use vanilla breakup mechanics
                try
                {
                    // Find the relationship to break
                    PawnRelationDef relationDef = null;
                    if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Spouse, recipient))
                        relationDef = PawnRelationDefOf.Spouse;
                    else if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Fiance, recipient))
                        relationDef = PawnRelationDefOf.Fiance;
                    else if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Lover, recipient))
                        relationDef = PawnRelationDefOf.Lover;

                    if (relationDef != null)
                    {
                        // Remove the direct relation
                        initiator.relations.RemoveDirectRelation(relationDef, recipient);
                        
                        // Add ex-relation
                        if (relationDef == PawnRelationDefOf.Spouse)
                            initiator.relations.AddDirectRelation(PawnRelationDefOf.ExSpouse, recipient);
                        else
                            initiator.relations.AddDirectRelation(PawnRelationDefOf.ExLover, recipient);
                        
                        // Send notification
                        if (PawnUtility.ShouldSendNotificationAbout(initiator) || PawnUtility.ShouldSendNotificationAbout(recipient))
                        {
                            Messages.Message("MessageBreakup".Translate(initiator.LabelShort, recipient.LabelShort,
                                initiator.Named("PAWN1"), recipient.Named("PAWN2")),
                                new LookTargets(new Pawn[] { initiator, recipient }),
                                MessageTypeDefOf.NegativeEvent);
                        }
                    }
                }
                catch (Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] Failed to process breakup: {0}", ex.Message));
                }
            }
        }

        /// <summary>
        /// Checks if pawn has the Kind trait
        /// </summary>
        private bool HasKindTrait(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null) return false;
            return pawn.story.traits.GetTrait(TraitDefOf.Kind) != null;
        }

        /// <summary>
        /// Checks if pawn has abrasive-type traits
        /// </summary>
        private bool HasAbrasiveTrait(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null) return false;
            
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait == null || trait.def == null) continue;
                string traitName = trait.def.defName.ToLower();
                if (traitName.Contains("abrasive") || traitName.Contains("psychopath"))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if two pawns are in a romantic relationship
        /// </summary>
        public static bool AreRomanticPartners(Pawn pawn1, Pawn pawn2)
        {
            if (pawn1 == null || pawn1.relations == null || pawn2 == null) return false;
            
            return pawn1.relations.DirectRelationExists(PawnRelationDefOf.Spouse, pawn2) ||
                   pawn1.relations.DirectRelationExists(PawnRelationDefOf.Fiance, pawn2) ||
                   pawn1.relations.DirectRelationExists(PawnRelationDefOf.Lover, pawn2);
        }
    }
}
