using RimWorld;
using Verse;
using Verse.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SocialInteractions
{
    /// <summary>
    /// Interaction worker for planned backstabbing after successful badmouthing where the instigator targets the original target's allies
    /// </summary>
    public class InteractionWorker_Backstabbing : InteractionWorker
    {
        // Property to store the target pawn when it's known from job scheduling
        private Pawn scheduledTargetPawn = null;
        public Pawn ScheduledTargetPawn 
        { 
            get { return scheduledTargetPawn; } 
            set { scheduledTargetPawn = value; } 
        }
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
                SLog.Warning("[SocialInteractions] InteractionWorker_Backstabbing: Initiator or recipient is null, skipping interaction.");
                // Initialize output parameters and return early
                letterText = null;
                letterLabel = null;
                letterDef = null;
                lookTargets = LookTargets.Invalid;
                return;
            }
            
            // Check if the same pawn is both initiator and recipient (self-interaction not allowed)
            if (initiator == recipient)
            {
                SLog.Warning("[SocialInteractions] InteractionWorker_Backstabbing: Initiator and recipient are the same pawn, skipping interaction.");
                letterText = null;
                letterLabel = null;
                letterDef = null;
                lookTargets = LookTargets.Invalid;
                return;
            }
            
            // Check if backstabbing is enabled in settings
            if (!SocialInteractions.Settings.enableBackstabbing)
            {
                // If backstabbing is disabled, initialize output parameters and return early
                letterText = null;
                letterLabel = null;
                letterDef = null;
                lookTargets = LookTargets.Invalid;
                return;
            }
            
            // For the first implementation, we'll use a simpler approach where the interaction
            // is either an information gathering attempt or a backstabbing attempt
            // Use social skill and manipulation traits to determine approach
            
            int initiatorSocialSkill = initiator.skills != null ? initiator.skills.GetSkill(SkillDefOf.Social).Level : 0;
            bool hasManipulationTrait = HasTraitThatEncouragesManipulation(initiator);
            
            // If the initiator has high social skill and manipulation traits, attempt information gathering
            if (hasManipulationTrait && initiatorSocialSkill >= 8)
            {
                // Execute information gathering phase - interact with the target to learn about their relationships
                ExecuteInfoGatheringPhase(initiator, recipient, extraSentencePacks);
                    
                // Call the base method for the info gathering interaction
                base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            }
            else
            {
                // Execute the actual backstabbing - use gathered information to approach a target's ally
                // For this case, we'll just do a direct backstabbing attempt without prior info gathering
                ExecuteDirectBackstabbing(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            }
        }

        /// <summary>
        /// Execute the information gathering phase where the instigator tries to learn 
        /// about the recipient's relationships through conversation
        /// </summary>
        private void ExecuteInfoGatheringPhase(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks)
        {
            // The "recipient" in this phase is actually the target whose relationships we're investigating
            // Try to get information about who the recipient values most
            Pawn bestFriend = TryExtractBestFriendInfo(initiator, recipient);
            
            if (bestFriend != null)
            {
                // Successfully gathered information - now we can plan the backstabbing
                // Handle the LLM interaction for the information gathering
                string subject = string.Format("A subtle conversation where {0} skillfully extracts information from {1} about their closest relationships, learning that {1} values and trusts {2}.", 
                    initiator.LabelShort, recipient.LabelShort, bestFriend.LabelShort);
                    
                // Skip spam protection for backstabbing as these are rare, important events that should be witnessed
                SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.Backstabbing, subject, true, false);
                
                // Create a custom log entry for the information gathering
                try
                {
                    PlayLogEntry_Backstabbing infoGatherLogEntry = new PlayLogEntry_Backstabbing(SI_InteractionDefOf.Backstabbing, initiator, recipient, extraSentencePacks, bestFriend, true);
                    
                    // Add the entry to the play log to update the social history
                    if (Find.PlayLog != null)
                    {
                        Find.PlayLog.Add(infoGatherLogEntry);
                    }
                }
                catch (System.Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] InteractionWorker_Backstabbing: Failed to add info gathering to play log: {0}", ex.Message));
                }
            }
            else
            {
                // Failed to extract information
                string subject = string.Format("An unsuccessful attempt by {0} to extract information from {1} about their relationships or who they trust most.",
                    initiator.LabelShort, recipient.LabelShort);

                // Skip spam protection for backstabbing as these are rare, important events that should be witnessed
                SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.Backstabbing, subject, true, false);

                // Create a custom log entry for the failed information gathering
                try
                {
                    // For failed info gathering, we'll still pass the recipient as the target to maintain log consistency
                    PlayLogEntry_Backstabbing infoGatherLogEntry = new PlayLogEntry_Backstabbing(SI_InteractionDefOf.Backstabbing, initiator, recipient, extraSentencePacks, recipient, false);

                    // Add the entry to the play log to update the social history
                    if (Find.PlayLog != null)
                    {
                        Find.PlayLog.Add(infoGatherLogEntry);
                    }
                }
                catch (System.Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] InteractionWorker_Backstabbing: Failed to add failed info gathering to play log: {0}", ex.Message));
                }
            }
        }

        /// <summary>
        /// Execute direct backstabbing where the instigator approaches 
        /// one of the target's allies to turn them against the target
        /// </summary>
        private void ExecuteDirectBackstabbing(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            // Initialize out parameters
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = LookTargets.Invalid;
            
            // For the backstabbing phase, 'recipient' should be the ally of the original target
            // We need to identify the target pawn for the backstabbing attempt
            Pawn targetPawn = null;
            
            // First, try to get the target from the ScheduledTargetPawn property if this is coming from a scheduled backstabbing attempt
            if (ScheduledTargetPawn != null)
            {
                targetPawn = ScheduledTargetPawn;
            }
            else
            {
                // If we don't have a scheduled target from a previous badmouthing interaction,
                // we need to select a target. Since we're in the "direct backstabbing" flow,
                // the pawn is attempting backstabbing without prior information gathering.
                // This means they don't know the relationships and should target randomly.
                targetPawn = SelectRandomTargetForBackstabbing(initiator, recipient);
            }
            
            if (targetPawn == null)
            {
                SLog.Warning("[SocialInteractions] InteractionWorker_Backstabbing: Could not determine target pawn for backstabbing, skipping.");
                return;
            }
            
            // Validate that we don't have the same pawn in multiple roles
            if (targetPawn == initiator || targetPawn == recipient)
            {
                SLog.Warning(string.Format("[SocialInteractions] InteractionWorker_Backstabbing: Target pawn is same as initiator or recipient. Initiator: {0}, Recipient: {1}, Target: {2}, skipping.",
                    initiator.LabelShort, recipient.LabelShort, targetPawn.LabelShort));
                return;
            }
            
            // Send a warning notification to the player about the backstabbing attempt
            string backstabMessage = string.Format("{0} is attempting to manipulate {1} against {2}.", 
                initiator.LabelShort, recipient.LabelShort, targetPawn.LabelShort);
            Messages.Message(backstabMessage, new LookTargets(initiator, recipient, targetPawn), MessageTypeDefOf.ThreatBig);
            
            // Determine if the backstab attempt succeeds based on social skill comparison
            bool backstabSuccessful = AttemptBackstab(initiator, recipient, targetPawn);
            
            // Apply effects based on success/failure
            string subject = GenerateBackstabSubject(initiator, recipient, targetPawn, backstabSuccessful);
            
            if (backstabSuccessful)
            {
                // Apply massive opinion reversal to recipient's opinion of target
                ApplySuccessfulBackstabEffects(initiator, recipient, targetPawn);
            }
            else
            {
                // The attempt failed, maybe recipient is suspicious or sees through the deception
                ApplyFailedBackstabEffects(initiator, recipient, targetPawn);
            }

            // Handle the LLM interaction with the generated subject
            // Skip spam protection for backstabbing as these are rare, important events that should be witnessed
            SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.Backstabbing, subject, true, false);
            
            // Create a custom log entry for the backstabbing interaction to ensure it's properly recorded in social history
            try
            {
                PlayLogEntry_Backstabbing backstabLogEntry = new PlayLogEntry_Backstabbing(SI_InteractionDefOf.Backstabbing, initiator, recipient, extraSentencePacks, targetPawn, backstabSuccessful);
                
                // Add the entry to the play log to update the social history
                if (Find.PlayLog != null)
                {
                    Find.PlayLog.Add(backstabLogEntry);
                }
            }
            catch (System.Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] InteractionWorker_Backstabbing: Failed to add backstabbing to play log: {0}", ex.Message));
            }
            
            // Call the base Interacted method to create the normal log entry using XML rules
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
        }
        
        /// <summary>
        /// Try to extract information about the recipient's best friend through conversation
        /// </summary>
        private Pawn TryExtractBestFriendInfo(Pawn initiator, Pawn recipient)
        {
            // Calculate success chance based on social skill and traits
            int initiatorSocialSkill = initiator.skills != null ? initiator.skills.GetSkill(SkillDefOf.Social).Level : 0;
            int recipientSocialSkill = recipient.skills != null ? recipient.skills.GetSkill(SkillDefOf.Social).Level : 0;
            
            // Base chance of success - use the settings value instead of hardcoded 0.3
            float baseChance = SocialInteractions.Settings.baseBackstabbingChance; // Base chance from settings
            
            // Adjust for skill difference
            float skillDifference = (initiatorSocialSkill - recipientSocialSkill) * 0.05f; // 5% per skill difference
            baseChance += skillDifference;
            
            // Adjust for manipulation traits
            bool hasDeceptionTrait = HasTraitThatEnhancesDeception(initiator);
            if (hasDeceptionTrait)
            {
                baseChance += 0.2f;
            }
            
            bool hasPerceptiveTrait = HasTraitThatPreventsDeception(recipient);
            if (hasPerceptiveTrait)
            {
                baseChance -= 0.2f;
            }
            
            // Ensure chance is within bounds
            baseChance = Math.Max(0.1f, Math.Min(0.8f, baseChance));
            
            // Roll for success
            float roll = Rand.Value;
            bool success = roll < baseChance;
            
            if (success)
            {
                // Success! Find the recipient's most valued pawn to return as their "best friend"
                Pawn bestFriend = FindMostTrustedTargetForRecipient(null, recipient);
                
                if (bestFriend != null)
                {
                    return bestFriend;
                }
                else
                {
                    return null; // No good targets to backstab
                }
            }
            
            return null; // Failed to extract information
        }
        
        /// <summary>
        /// Identify the target pawn for backstabbing based on social relationships
        /// </summary>
        private Pawn IdentifyTargetForBackstabbing(Pawn initiator, Pawn potentialAlly)
        {
            // The "potentialAlly" is the pawn who we're trying to turn against their friend
            // We need to find who that pawn has the highest opinion of (their friend to be backstabbed)
            
            if (potentialAlly.Map == null || potentialAlly.Map.mapPawns.FreeColonistsAndPrisoners.Count == 0)
            {
                return null;
            }
            
            Pawn highestOpinionTarget = null;
            int highestOpinion = int.MinValue;
            
            foreach (Pawn potentialTarget in potentialAlly.Map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (potentialTarget == initiator || potentialTarget == potentialAlly)
                    continue; // Skip the instigator and the ally themselves
                
                if (potentialAlly.relations != null)
                {
                    int opinion = potentialAlly.relations.OpinionOf(potentialTarget);
                    if (opinion > highestOpinion)
                    {
                        highestOpinion = opinion;
                        highestOpinionTarget = potentialTarget;
                    }
                }
            }
            
            // Only return if the opinion is significantly positive
            return highestOpinion >= 30 ? highestOpinionTarget : null; // Threshold for "truly trusted"
        }
        
        /// <summary>
        /// Select a random target pawn for backstabbing when the instigator doesn't know relationships
        /// </summary>
        private Pawn SelectRandomTargetForBackstabbing(Pawn initiator, Pawn recipient)
        {
            if (recipient.Map == null || recipient.Map.mapPawns.FreeColonistsAndPrisoners.Count == 0)
            {
                return null;
            }
            
            // Create a list of potential targets (excluding the initiator and recipient themselves)
            List<Pawn> potentialTargets = new List<Pawn>();
            
            foreach (Pawn potentialTarget in recipient.Map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (potentialTarget == initiator || potentialTarget == recipient)
                    continue; // Skip the instigator and the recipient themselves
                
                // Only consider conscious pawns who are not in mental states
                if (potentialTarget.Dead || potentialTarget.Downed || potentialTarget.InMentalState)
                    continue;
                    
                potentialTargets.Add(potentialTarget);
            }
            
            // If we have no valid targets, return null
            if (potentialTargets.Count == 0)
            {
                SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Backstabbing: No valid targets available for random backstabbing selection"));
                return null;
            }
            
            // Randomly select a target from the list
            Pawn randomTarget = potentialTargets[Rand.Range(0, potentialTargets.Count)];
            
            SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Backstabbing: Randomly selected target {0} from {1} possible targets", 
                randomTarget.LabelShort, potentialTargets.Count));
                
            return randomTarget;
        }
        
        /// <summary>
        /// Find the pawn that recipient has the highest opinion of (the one being targeted in the backstab)
        /// </summary>
        private Pawn FindMostTrustedTargetForRecipient(Pawn initiator, Pawn recipient)
        {
            if (recipient.Map == null || recipient.Map.mapPawns.FreeColonistsAndPrisoners.Count == 0)
            {
                return null;
            }
            
            Pawn highestOpinionTarget = null;
            int highestOpinion = int.MinValue;
            
            foreach (Pawn potentialTarget in recipient.Map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (potentialTarget == initiator || potentialTarget == recipient)
                    continue; // Skip the instigator and recipient themselves
                
                if (recipient.relations != null)
                {
                    int opinion = recipient.relations.OpinionOf(potentialTarget);
                    if (opinion > highestOpinion)
                    {
                        highestOpinion = opinion;
                        highestOpinionTarget = potentialTarget;
                    }
                }
            }
            
            return highestOpinionTarget;
        }

        /// <summary>
        /// Attempt to successfully backstab by comparing social skills and trust levels
        /// </summary>
        private bool AttemptBackstab(Pawn initiator, Pawn recipient, Pawn targetPawn)
        {
            // Get original trust level between recipient and target
            int originalTrust = recipient.relations != null ? recipient.relations.OpinionOf(targetPawn) : 0;

            // Get social skills
            int initiatorSocialSkill = initiator.skills != null ? initiator.skills.GetSkill(SkillDefOf.Social).Level : 0;
            int recipientSocialSkill = recipient.skills != null ? recipient.skills.GetSkill(SkillDefOf.Social).Level : 0;

            // Calculate base chance based on trust level and social skill difference
            float baseChance = 0.3f; // 30% base chance

            // Adjust for trust level (higher trust is harder to break)
            if (originalTrust > 0)
            {
                // Higher trust requires more skill to overcome
                float trustDifficulty = Math.Min(1.0f, originalTrust / 100.0f); // Cap at 1.0 for 100+ trust
                baseChance -= trustDifficulty * 0.3f; // Reduce chance based on trust
            }

            // Adjust for social skill difference
            float skillDifference = (initiatorSocialSkill - recipientSocialSkill) * 0.05f; // 5% per skill level difference
            baseChance += skillDifference;

            // Adjust for traits that affect deception
            if (HasTraitThatEnhancesDeception(initiator))
            {
                baseChance += 0.2f; // 20% bonus for good deceivers
            }

            if (HasTraitThatPreventsDeception(recipient))
            {
                baseChance -= 0.2f; // 20% penalty for perceptive pawns
            }

            // Ensure chance is within bounds
            baseChance = Math.Max(0.05f, Math.Min(0.95f, baseChance));

            return Rand.Value < baseChance;
        }
        
        private string GenerateBackstabSubject(Pawn initiator, Pawn recipient, Pawn targetPawn, bool success)
        {
            // Get detailed target description for LLM context
            string targetDescription = SocialInteractions.GetPawnDescription(targetPawn);
            
            if (success)
            {
                // Get original trust level to customize the subject
                int originalTrust = recipient.relations != null ? recipient.relations.OpinionOf(targetPawn) : 0;

                if (originalTrust > 50)
                {
                    // Catastrophic betrayal for high-trust relationships
                    return string.Format("{0} uses deception to turn {1} against {2} ({3}). The manipulation is successful and devastating, causing {1} to completely reverse their opinion of {2}, now seeing {2} negatively.",
                        initiator.LabelShort, recipient.LabelShort, targetPawn.LabelShort, targetDescription);
                }
                else
                {
                    // Generic manipulation for low/medium-trust relationships
                    return string.Format("{0} uses deception to turn {1} against {2} ({3}). The manipulation is successful, causing {1} to now think worse of {2}.",
                        initiator.LabelShort, recipient.LabelShort, targetPawn.LabelShort, targetDescription);
                }
            }
            else
            {
                return string.Format("{0} tries to turn {1} against {2} ({3}) through deceptive manipulation. However, {1} sees through the deception and the attempt fails.",
                    initiator.LabelShort, recipient.LabelShort, targetPawn.LabelShort, targetDescription);
            }
        }
        
        /// <summary>
        /// Apply massive opinion reversal effects when backstabbing succeeds
        /// </summary>
        private void ApplySuccessfulBackstabEffects(Pawn initiator, Pawn recipient, Pawn targetPawn)
        {
            // Get original trust level between recipient and target
            int originalTrust = recipient.relations != null ? recipient.relations.OpinionOf(targetPawn) : 0;
            
            // Apply the opinion change based on original trust level
            if (recipient.needs != null && recipient.needs.mood != null)
            {
                // For high trust levels (over 50), apply catastrophic betrayal with massive opinion reversal
                // For moderate or low trust levels (50 or below), apply generic "heard bad things" thought with -5 opinion offset
                if (originalTrust > 50)
                {
                    // Catastrophic betrayal: massive opinion change based on original trust level
                    ThoughtDef manipulatedThought = SI_ThoughtDefOf.WasManipulatedAgainstSomeone;
                    if (manipulatedThought != null)
                    {
                        recipient.needs.mood.thoughts.memories.TryGainMemory(manipulatedThought, targetPawn);
                    }
                }
                else
                {
                    // Generic "heard bad things" thought with moderate -5 opinion offset
                    ThoughtDef heardBadThingsThought = DefDatabase<ThoughtDef>.GetNamed("WasToldNegativeThings");
                    if (heardBadThingsThought != null)
                    {
                        recipient.needs.mood.thoughts.memories.TryGainMemory(heardBadThingsThought, targetPawn);
                    }
                    else
                    {
                        SLog.Warning(string.Format("[SocialInteractions] Backstabbing: Could not find WasToldNegativeThings thought definition, skipping thought application"));
                    }
                }
            }
            
            // Apply thoughts to all parties
            // Initiator gets positive thoughts for successful manipulation
            if (initiator.needs != null && initiator.needs.mood != null)
            {
                ThoughtDef successfulManipulationThought = SI_ThoughtDefOf.SuccessfullyBackstabbedSomeone;
                if (successfulManipulationThought != null)
                {
                    initiator.needs.mood.thoughts.memories.TryGainMemory(successfulManipulationThought, recipient);
                }
            }
            
            // Target pawn does not immediately realize they were backstabbed
            // The revelation should happen later when they interact with the friend who now hates them
            // This creates more realistic and dramatic social dynamics
        }
        
        /// <summary>
        /// Apply effects when backstabbing fails
        /// </summary>
        private void ApplyFailedBackstabEffects(Pawn initiator, Pawn recipient, Pawn targetPawn)
        {
            // The recipient sees through the deception
            // Apply negative thoughts to recipient about the instigator (for trying to deceive them)
            if (recipient.needs != null && recipient.needs.mood != null)
            {
                ThoughtDef failedManipulationThought = SI_ThoughtDefOf.WasTargetOfFailedManipulation;
                if (failedManipulationThought != null)
                {
                    recipient.needs.mood.thoughts.memories.TryGainMemory(failedManipulationThought, initiator);
                }
            }
            
            // Apply negative thoughts to instigator for failing their manipulation attempt
            if (initiator.needs != null && initiator.needs.mood != null)
            {
                ThoughtDef failedAttemptThought = SI_ThoughtDefOf.FailedBackstabAttempt;
                if (failedAttemptThought != null)
                {
                    initiator.needs.mood.thoughts.memories.TryGainMemory(failedAttemptThought, recipient);
                }
            }
            
            // Target gets slight positive thoughts about recipient (for seeing through the deception)
            if (targetPawn.needs != null && targetPawn.needs.mood != null)
            {
                // Slight positive boost to opinion of target (for seeing through the deception)
                ThoughtDef sawThroughDeceptionThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Chitchat");
                if (sawThroughDeceptionThought != null)
                {
                    targetPawn.needs.mood.thoughts.memories.TryGainMemory(sawThroughDeceptionThought, recipient);
                }
            }
        }
        
        /// <summary>
        /// Calculate the massive negative opinion based on original trust level
        /// </summary>
        private int CalculateBetrayalOpinion(int originalTrust)
        {
            // The more trusted someone was, the more devastating the betrayal
            // Formula: -(originalTrust * multiplier) with limits
            float multiplier = 1.8f; // Adjust this to control severity
            int betrayalValue = (int)(-originalTrust * multiplier);
            
            // Set reasonable limits
            betrayalValue = Math.Max(-100, Math.Min(-10, betrayalValue)); // Between -100 and -10
            
            return betrayalValue;
        }
        
        private bool HasTraitThatEnhancesDeception(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }
            
            // Check for traits that enhance social manipulation
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait != null && trait.def != null)
                {
                    string traitLabel = trait.def.defName.ToLower();
                    string traitLabelDisplay = trait.Label.ToLower();
                    
                    // Check for traits that enhance deception
                    if (traitLabel.Contains("deceptive") || 
                        traitLabel.Contains("charming") || 
                        traitLabel.Contains("liar") ||
                        traitLabel.Contains("manipulative") ||
                        traitLabel.Contains("smooth") ||
                        traitLabelDisplay.Contains("deceptive") || 
                        traitLabelDisplay.Contains("charming") || 
                        traitLabelDisplay.Contains("liar") ||
                        traitLabelDisplay.Contains("manipulative") ||
                        traitLabelDisplay.Contains("smooth talker"))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        private bool HasTraitThatPreventsDeception(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }
            
            // Check for traits that make one perceptive to deception
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait != null && trait.def != null)
                {
                    string traitLabel = trait.def.defName.ToLower();
                    string traitLabelDisplay = trait.Label.ToLower();
                    
                    // Check for traits that enhance perception of deception
                    if (traitLabel.Contains("perceptive") || 
                        traitLabel.Contains("observant") || 
                        traitLabel.Contains("insightful") ||
                        traitLabelDisplay.Contains("perceptive") || 
                        traitLabelDisplay.Contains("observant") || 
                        traitLabelDisplay.Contains("insightful"))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Checks if a pawn has traits that encourage manipulation and strategic backstabbing
        /// </summary>
        private bool HasTraitThatEncouragesManipulation(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }
            
            // Check for traits that make backstabbing more likely
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait != null && trait.def != null)
                {
                    string traitLabel = trait.def.defName.ToLower();
                    string traitLabelDisplay = trait.Label.ToLower();
                    
                    // Check for manipulative, strategic, or deceptive traits
                    if (traitLabel.Contains("manipulative") || 
                        traitLabel.Contains("deceptive") || 
                        traitLabel.Contains("calculating") ||
                        traitLabel.Contains("strategic") ||
                        traitLabel.Contains("psychopath") ||
                        traitLabel.Contains("liar") ||
                        traitLabel.Contains("smooth") ||
                        traitLabelDisplay.Contains("manipulative") || 
                        traitLabelDisplay.Contains("deceptive") || 
                        traitLabelDisplay.Contains("calculating") ||
                        traitLabelDisplay.Contains("strategic") ||
                        traitLabelDisplay.Contains("psychopath") ||
                        traitLabelDisplay.Contains("liar") ||
                        traitLabelDisplay.Contains("smooth talker"))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        private bool HasTraitThatEnjoysManipulation(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }
            
            // Check for traits that would make someone enjoy manipulation
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait != null && trait.def != null)
                {
                    string traitLabel = trait.def.defName.ToLower();
                    string traitLabelDisplay = trait.Label.ToLower();
                    
                    // Check for traits that enjoy negative interactions
                    if (traitLabel.Contains("sadist") || 
                        traitLabel.Contains("manipulative") || 
                        traitLabel.Contains("psychopath") ||
                        traitLabel.Contains("bully") ||
                        traitLabelDisplay.Contains("sadist") || 
                        traitLabelDisplay.Contains("manipulative") || 
                        traitLabelDisplay.Contains("psychopath") ||
                        traitLabelDisplay.Contains("bully"))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
    }
}