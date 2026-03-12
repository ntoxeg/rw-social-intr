using RimWorld;
using Verse;
using Verse.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SocialInteractions
{
    public class InteractionWorker_Badmouthing : InteractionWorker
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
                SLog.Warning("[SocialInteractions] InteractionWorker_Badmouthing: Initiator or recipient is null, skipping interaction.");
                // Initialize output parameters and return early
                letterText = null;
                letterLabel = null;
                letterDef = null;
                lookTargets = LookTargets.Invalid;
                return;
            }

            // Check if the initiator has traits that would make them avoid this interaction
            bool preventsBadmouthing = HasTraitThatPreventsBadmouthing(initiator);
            if (preventsBadmouthing)
            {
                base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
                return;
            }

            // Find the least favorite pawn in the colony for the initiator
            Pawn targetPawn = GetLeastFavoritePawn(initiator);
                
            if (targetPawn == null)
            {
                base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
                return;
            }

            // Check that the target pawn is not the same as the recipient
            if (targetPawn == recipient)
            {
                base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
                return;
            }

            // Check recipient's opinions of both the target and the initiator
            int recipientOpinionOfTarget = recipient.relations != null ? recipient.relations.OpinionOf(targetPawn) : 0;
            int recipientOpinionOfInitiator = recipient.relations != null ? recipient.relations.OpinionOf(initiator) : 0;
            
            // Get initiator's opinion of the target
            int initiatorOpinionOfTarget = initiator.relations != null ? initiator.relations.OpinionOf(targetPawn) : 0;
            
            // Check if both initiator and recipient share a negative opinion of the target (gossip scenario)
            bool sharedNegativeOpinion = initiatorOpinionOfTarget <= SocialInteractions.Settings.badmouthingLowOpinionThreshold && 
                                        recipientOpinionOfTarget <= SocialInteractions.Settings.badmouthingLowOpinionThreshold;

            if (sharedNegativeOpinion)
            {
                // Gossip scenario: Both pawns share negative opinions about the target
                // This should strengthen their bond and confirm their shared views
                ApplyGossipThoughts(initiator, recipient, targetPawn);
                
                // Generate appropriate subject text for LLM with more detailed information
                string targetDescription = SocialInteractions.GetPawnDescription(targetPawn);
                string subject = string.Format("A gossip interaction where {0} and {1} share negative opinions about {2} ({3}). This strengthens their relationship and confirms their mutual dislike.",
                    initiator.LabelShort, recipient.LabelShort, targetPawn.LabelShort, targetDescription);
                
                // Handle the LLM interaction
                SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.Badmouthing, subject);
            }
            else if (recipientOpinionOfTarget <= recipientOpinionOfInitiator)
            {
                // Original badmouthing scenario: recipient was told negative things about someone they already don't like much
                ApplyBadmouthingThoughtsToTarget(initiator, recipient, targetPawn);
                
                // Generate appropriate subject text for LLM with more detailed information
                string targetDescription = SocialInteractions.GetPawnDescription(targetPawn);
                string subject = string.Format("A badmouthing interaction where {0} speaks negatively about {1} ({2}) to {3}. {3} values {1} less than {0}, causing {3} to believe the badmouthing and think worse of {1}.",
                    initiator.LabelShort, targetPawn.LabelShort, targetDescription, recipient.LabelShort);
                
                // Handle the LLM interaction
                SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.Badmouthing, subject);
            }
            else
            {
                // Original badmouthing scenario: recipient was told negative things about someone they respect
                ApplyBadmouthingThoughtsToInitiator(initiator, recipient, targetPawn);
                
                // Generate appropriate subject text for LLM with more detailed information
                string targetDescription = SocialInteractions.GetPawnDescription(targetPawn);
                string subject = string.Format("A badmouthing interaction where {0} speaks negatively about {1} ({2}) to {3}. However, {3} respects {1} more than {0}, causing {3} to lose respect for {0} instead.",
                    initiator.LabelShort, targetPawn.LabelShort, targetDescription, recipient.LabelShort);
                
                // Handle the LLM interaction
                SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.Badmouthing, subject);
            }

            // We removed the target-specific sentence rulepacks because they caused grammar resolution issues
            // The target information is now properly handled by the PlayLogEntry_Badmouthing class
            // which uses direct string formatting to include the target pawn's name

            // Call the base Interacted method to create the normal log entry using XML rules
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            
            // Create a custom log entry that includes the target pawn information to ensure consistency
            // This ensures the target shown in the play log matches the one used in the interaction
            try
            {
                // Use the existing targetPawn variable that was already determined in the method
                // This ensures perfect consistency between the interaction and the log entry
                if (targetPawn != null && targetPawn != recipient)
                {
                    // Create a custom log entry for the badmouthing interaction that includes the target pawn
                    PlayLogEntry_Badmouthing badmouthingLogEntry = new PlayLogEntry_Badmouthing(SI_InteractionDefOf.Badmouthing, initiator, recipient, extraSentencePacks, targetPawn);
                    
                    // Add the entry to the play log to update the social history
                    if (Find.PlayLog != null)
                    {
                        Find.PlayLog.Add(badmouthingLogEntry);
                    }
                    
                    // Check if this successful badmouthing creates an opportunity for backstabbing
                    // This would happen when the instigator has sufficient motivation and opportunity
                    TryTriggerBackstabbingOpportunity(initiator, recipient, targetPawn);
                }
            }
            catch (System.Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] InteractionWorker_Badmouthing: Failed to add badmouthing to play log: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Applies thoughts for gossip scenario where both pawns share negative opinions about the target
        /// </summary>
        private void ApplyGossipThoughts(Pawn initiator, Pawn recipient, Pawn targetPawn)
        {
            // Both pawns get positive thoughts for bonding with someone who shares their negative opinion
            // This encourages future interactions between them (gossip partnerships)
            
            // Use our newly defined thoughts that promote bonding over shared negative opinions
            if (SI_ThoughtDefOf.BondedOverSharedDislike != null)
            {
                // Initiator bonds with recipient over shared dislike
                if (initiator.needs != null && initiator.needs.mood != null && initiator.needs.mood.thoughts != null && initiator.needs.mood.thoughts.memories != null)
                {
                    initiator.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.BondedOverSharedDislike, recipient);
                }
                
                // Recipient bonds with initiator over shared dislike  
                if (recipient.needs != null && recipient.needs.mood != null && recipient.needs.mood.thoughts != null && recipient.needs.mood.thoughts.memories != null)
                {
                    recipient.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.BondedOverSharedDislike, initiator);
                }
            }
            else if (SI_ThoughtDefOf.FoundCommonGround != null)
            {
                // Fallback to FoundCommonGround thought if BondedOverSharedDislike is not available
                if (initiator.needs != null && initiator.needs.mood != null && initiator.needs.mood.thoughts != null && initiator.needs.mood.thoughts.memories != null)
                {
                    initiator.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.FoundCommonGround, recipient);
                }
                if (recipient.needs != null && recipient.needs.mood != null && recipient.needs.mood.thoughts != null && recipient.needs.mood.thoughts.memories != null)
                {
                    recipient.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.FoundCommonGround, initiator);
                }
            }
            else
            {
                // Fallback to game's general social thoughts if custom thoughts aren't loaded
                
                // Use existing RimWorld thoughts that promote social bonding
                ThoughtDef socialConnectionThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("IncreasedChemistry");
                if (socialConnectionThought != null)
                {
                    // Give a positive thought to both for having increased chemistry
                    if (initiator.needs != null && initiator.needs.mood != null && initiator.needs.mood.thoughts != null && initiator.needs.mood.thoughts.memories != null)
                    {
                        initiator.needs.mood.thoughts.memories.TryGainMemory(socialConnectionThought, recipient);
                    }
                    if (recipient.needs != null && recipient.needs.mood != null && recipient.needs.mood.thoughts != null && recipient.needs.mood.thoughts.memories != null)
                    {
                        recipient.needs.mood.thoughts.memories.TryGainMemory(socialConnectionThought, initiator);
                    }
                }
                else
                {
                    // Last fallback - use SocialRecreationPartner as a generic positive social interaction
                    ThoughtDef positiveSocialThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("SocialRecreationPartner");
                    if (positiveSocialThought != null)
                    {
                    if (positiveSocialThought != null)
                    {
                        if (initiator.needs != null && initiator.needs.mood != null && initiator.needs.mood.thoughts != null && initiator.needs.mood.thoughts.memories != null)
                        {
                            initiator.needs.mood.thoughts.memories.TryGainMemory(positiveSocialThought, recipient);
                        }
                        if (recipient.needs != null && recipient.needs.mood != null && recipient.needs.mood.thoughts != null && recipient.needs.mood.thoughts.memories != null)
                        {
                            recipient.needs.mood.thoughts.memories.TryGainMemory(positiveSocialThought, initiator);
                        }
                    }
                    }
                }
            }
            
            // Optionally, both gain slight negative opinion of the target (reinforcing their shared dislike)
            // Only if the target is not one of the interacting pawns
            if (targetPawn != initiator && targetPawn != recipient)
            {
                // Reinforce negative opinion of the target through the interaction
                // This helps solidify the "us vs them" dynamic that promotes clique formation
                if (recipient.relations != null && recipient.needs != null && recipient.needs.mood != null && recipient.needs.mood.thoughts != null && recipient.needs.mood.thoughts.memories != null)
                {
                    // Apply a thought to recipient about the target to reinforce negative opinion
                    ThoughtDef wasToldNegativeThingsThought = DefDatabase<ThoughtDef>.GetNamed("WasToldNegativeThings");
                    if (wasToldNegativeThingsThought != null)
                    {
                        recipient.needs.mood.thoughts.memories.TryGainMemory(wasToldNegativeThingsThought, targetPawn);
                    }
                    else
                    {
                        // No appropriate fallback thought, so skip reinforcement of target's negative opinion
                        // The primary effect is the bonding between the two interacting pawns
                    }
                }
                
                if (initiator.relations != null && initiator.needs != null && initiator.needs.mood != null && initiator.needs.mood.thoughts != null && initiator.needs.mood.thoughts.memories != null)
                {
                    // Apply a thought to initiator about the target to reinforce negative opinion  
                    ThoughtDef wasToldNegativeThingsThought = DefDatabase<ThoughtDef>.GetNamed("WasToldNegativeThings");
                    if (wasToldNegativeThingsThought != null)
                    {
                        initiator.needs.mood.thoughts.memories.TryGainMemory(wasToldNegativeThingsThought, targetPawn);
                    }
                    else
                    {
                        // No appropriate fallback thought, so skip reinforcement of target's negative opinion
                        // The primary effect is the bonding between the two interacting pawns
                    }
                }
            }
        }
        
        /// <summary>
        /// Applies thoughts for original badmouthing scenario where target receives negative thoughts
        /// </summary>
        private void ApplyBadmouthingThoughtsToTarget(Pawn initiator, Pawn recipient, Pawn targetPawn)
        {
            // In this scenario, the recipient was told negative things about someone they already don't like much,
            // so they form an even worse opinion of that target
            // Apply the WasToldNegativeThings thought to the recipient about the target
            // Apply the WasToldNegativeThings thought to the recipient about the target
            if (recipient.needs != null && recipient.needs.mood != null && recipient.needs.mood.thoughts != null && recipient.needs.mood.thoughts.memories != null)
            {
                ThoughtDef wasToldNegativeThingsThought = DefDatabase<ThoughtDef>.GetNamed("WasToldNegativeThings");
                if (wasToldNegativeThingsThought != null)
                {
                    recipient.needs.mood.thoughts.memories.TryGainMemory(wasToldNegativeThingsThought, targetPawn);
                }
                else
                {
                    // Fallback to general insult thought if custom thought is not available
                    ThoughtDef insultedThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Insulted");
                    if (insultedThought != null)
                    {
                        recipient.needs.mood.thoughts.memories.TryGainMemory(insultedThought, targetPawn);
                    }
                }
            }
        }
        
        /// <summary>
        /// Applies thoughts for original badmouthing scenario where initiator receives negative thoughts
        /// </summary>
        private void ApplyBadmouthingThoughtsToInitiator(Pawn initiator, Pawn recipient, Pawn targetPawn)
        {
            // In this scenario, the recipient was told negative things about someone they respect by someone they trust less
            // This should damage the relationship with the initiator
            // Apply the HeardBadmouthing thought to the recipient about the initiator
            // Apply the HeardBadmouthing thought to the recipient about the initiator
            if (recipient.needs != null && recipient.needs.mood != null && recipient.needs.mood.thoughts != null && recipient.needs.mood.thoughts.memories != null)
            {
                ThoughtDef heardBadmouthingThought = DefDatabase<ThoughtDef>.GetNamed("HeardBadmouthing");
                if (heardBadmouthingThought != null)
                {
                    recipient.needs.mood.thoughts.memories.TryGainMemory(heardBadmouthingThought, initiator);
                }
                else
                {
                    // Fallback to general insult thought if custom thought is not available
                    ThoughtDef insultedThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Insulted");
                    if (insultedThought != null)
                    {
                        recipient.needs.mood.thoughts.memories.TryGainMemory(insultedThought, initiator);
                    }
                }
            }
        }

        private Pawn GetLeastFavoritePawn(Pawn pawn)
        {
            if (pawn.Map == null || pawn.Map.mapPawns.FreeColonistsAndPrisoners.Count == 0)
            {
                return null;
            }

            return SocialInteractions.GetWeightedLeastFavoritePawn(pawn);
        }


        
        private bool HasTraitThatPreventsBadmouthing(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }
            
            // Check for Kind trait - Kind pawns would never engage in badmouthing
            Trait kindTrait = pawn.story.traits.GetTrait(TraitDefOf.Kind);
            if (kindTrait != null)
            {
                return true;
            }
            
            // Add other traits that would prevent badmouthing here
            // For example, traits like "Good Listener" or similar pro-social traits
            
            return false;
        }
        
        private bool HasTraitThatEncouragesBadmouthing(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }
            
            // Check for traits that make badmouthing more likely
            // Jealous trait (if it exists)
            TraitDef jealousDef = DefDatabase<TraitDef>.GetNamedSilentFail("Jealous");
            if (jealousDef != null)
            {
                Trait jealousTrait = pawn.story.traits.GetTrait(jealousDef);
                if (jealousTrait != null)
                {
                    return true;
                }
            }
            
            // Abrasive trait
            TraitDef abrasiveDef = DefDatabase<TraitDef>.GetNamedSilentFail("Abrasive");
            if (abrasiveDef != null)
            {
                Trait abrasiveTrait = pawn.story.traits.GetTrait(abrasiveDef);
                if (abrasiveTrait != null)
                {
                    return true;
                }
            }
            
            // Psychopath trait (if it exists in the game/other mods)
            TraitDef psychopathDef = DefDatabase<TraitDef>.GetNamedSilentFail("Psychopath");
            if (psychopathDef != null)
            {
                Trait psychopathTrait = pawn.story.traits.GetTrait(psychopathDef);
                if (psychopathTrait != null)
                {
                    return true;
                }
            }
            
            // Add other traits like "Bullying", "Rigid", etc. if they exist
            // For now, we'll include any trait that tends to make a pawn antisocial
            
            // Check for any trait that has "Dislike" or negative social interaction effects
            // We can also check for traits that increase the pawn's tendency toward negative social behavior
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait != null && trait.def != null)
                {
                    // Check if this trait affects social interactions negatively or makes them more likely to speak negatively
                    if (trait.Label.ToLower().Contains("abrasive") || 
                        trait.Label.ToLower().Contains("psychopath") || 
                        trait.Label.ToLower().Contains("jealous") ||
                        trait.Label.ToLower().Contains("mean") ||
                        trait.Label.ToLower().Contains("cold"))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Checks if the successful badmouthing creates an opportunity for strategic backstabbing
        /// where the instigator might approach the target's allies to turn them against the target
        /// </summary>
        private void TryTriggerBackstabbingOpportunity(Pawn initiator, Pawn recipient, Pawn targetPawn)
        {
            // Check if backstabbing is enabled in settings
            if (!SocialInteractions.Settings.enableBackstabbing)
            {
                return;
            }

            if (initiator == null || recipient == null || targetPawn == null)
            {
                return;
            }

            // Check if the target has highly trusted allies worth targeting
            Pawn bestTargetForBackstab = FindMostTrustedAllyOfTarget(targetPawn, recipient);
            if (bestTargetForBackstab == null || bestTargetForBackstab == initiator || bestTargetForBackstab == recipient || bestTargetForBackstab == targetPawn)
            {
                // No suitable target for backstabbing found or the target would be one of the participants
                return;
            }

            // Calculate the backstabbing opportunity chance based on various factors
            float backstabChance = CalculateBackstabbingChance(initiator, bestTargetForBackstab, targetPawn);

            // Determine if this pawn would do info gathering first or go direct (without proper info)
            bool willDoInfoGathering = ShouldDoInfoGatheringFirst(initiator, bestTargetForBackstab, targetPawn);

            // If the pawn will not do info gathering (will try direct approach without proper information), reduce the chance significantly
            if (!willDoInfoGathering)
            {
                backstabChance *= 0.1f; // Reduce to 10% of normal chance when attempting backstab without proper info gathering
            }

            // Roll for the backstabbing opportunity
            float roll = Rand.Value;
            SLog.Message(string.Format("[SocialInteractions] Badmouthing: Backstabbing roll was {0:F3} (needed < {1:F3}) - {2}",
                roll, backstabChance, roll < backstabChance ? "SUCCESS" : "FAILED"));

            if (roll < backstabChance)
            {
                // Schedule a strategic backstabbing interaction
                ScheduleBackstabbingAttempt(initiator, bestTargetForBackstab, targetPawn);
            }
        }
        
        /// <summary>
        /// Find the pawn that the target has the highest opinion of (the best target for backstabbing)
        /// </summary>
        private Pawn FindMostTrustedAllyOfTarget(Pawn targetPawn, Pawn excludedPawn = null)
        {
            if (targetPawn.Map == null || targetPawn.Map.mapPawns.FreeColonistsAndPrisoners.Count == 0)
            {
                return null;
            }
            
            Pawn highestOpinionOwner = null;
            int highestOpinion = int.MinValue;
            
            foreach (Pawn possibleAlly in targetPawn.Map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (possibleAlly == targetPawn || possibleAlly == excludedPawn)
                    continue; // Skip the target themselves and any excluded pawn
                
                if (targetPawn.relations != null)
                {
                    int opinion = targetPawn.relations.OpinionOf(possibleAlly);
                    if (opinion > highestOpinion)
                    {
                        highestOpinion = opinion;
                        highestOpinionOwner = possibleAlly;
                    }
                }
            }
            
            // Only return if the opinion is significantly positive
            return highestOpinion >= 30 ? highestOpinionOwner : null; // Threshold for "truly trusted"
        }
        
        /// <summary>
        /// Calculate the chance for a backstabbing opportunity based on various factors
        /// </summary>
        private float CalculateBackstabbingChance(Pawn initiator, Pawn targetAlly, Pawn originalTarget)
        {
            float baseChance = SocialInteractions.Settings.baseBackstabbingChance; // Base chance from settings

            // Increase chance if the target ally has very high opinion of the original target
            int allyOpinionOfTarget = targetAlly.relations != null ? targetAlly.relations.OpinionOf(originalTarget) : 0;
            if (allyOpinionOfTarget > 50)
            {
                baseChance += 0.2f; // 20% bonus for high-trust relationships
            }
            else if (allyOpinionOfTarget > 30)
            {
                baseChance += 0.1f; // 10% bonus for moderately high trust
            }

            // Increase chance if the instigator has high social skill
            int socialSkill = initiator.skills != null ? initiator.skills.GetSkill(SkillDefOf.Social).Level : 0;
            if (socialSkill >= 10)
            {
                baseChance += 0.2f; // 20% bonus for high social skill
            }
            else if (socialSkill >= 7)
            {
                baseChance += 0.1f; // 10% bonus for decent social skill
            }

            // Increase chance if the instigator has traits that encourage manipulation
            if (HasTraitThatEncouragesManipulation(initiator))
            {
                baseChance += 0.3f; // 30% bonus for manipulative traits
            }

            // Apply age-based modifiers (since backstabbing is triggered after badmouthing)
            baseChance *= CalculateAgeModifier(initiator, originalTarget);

            // Cap at a reasonable maximum
            baseChance = Math.Min(0.8f, baseChance);

            return baseChance;
        }
        
        /// <summary>
        /// Schedule a backstabbing attempt at a future time
        /// </summary>
        private void ScheduleBackstabbingAttempt(Pawn initiator, Pawn targetAlly, Pawn originalTarget)
        {
            // Decide whether to do information gathering first
            if (ShouldDoInfoGatheringFirst(initiator, targetAlly, originalTarget))
            {
                // Schedule an information gathering attempt first
                Job infoGatherJob = new Job(SI_JobDefOf.BackstabbingGatherInfo, originalTarget);
                infoGatherJob.count = 1; // Just execute once
                
                if (initiator.jobs != null)
                {
                    initiator.jobs.TryTakeOrderedJob(infoGatherJob);
                }
            }
            else
            {
                // Create a job for the backstabbing approach
                // This will make the pawn physically move to the target and perform the interaction
                Job backstabJob = new Job(SI_JobDefOf.BackstabbingApproachTarget, targetAlly);
                // Also pass the original target (the person being backstabbed/about whom negative things are being said)
                backstabJob.SetTarget(TargetIndex.B, originalTarget);
                backstabJob.count = 1; // Just execute once
                
                // Add the job to the initiator's queue
                if (initiator.jobs != null)
                {
                    initiator.jobs.TryTakeOrderedJob(backstabJob);
                }
            }
        }
        
        /// <summary>
        /// Determines if the initiator should gather information first before attempting backstabbing
        /// </summary>
        private bool ShouldDoInfoGatheringFirst(Pawn initiator, Pawn targetAlly, Pawn originalTarget)
        {
            // Check if the initiator has high social skill and manipulation traits that would make info gathering worthwhile
            int initiatorSocialSkill = initiator.skills != null ? initiator.skills.GetSkill(SkillDefOf.Social).Level : 0;
            bool hasManipulationTrait = HasTraitThatEncouragesManipulation(initiator);
            
            // Only do info gathering if the pawn has the right traits and skills
            return hasManipulationTrait && initiatorSocialSkill >= 8;
        }
        
        /// <summary>
        /// Check if the conditions are right for attempting backstabbing
        /// </summary>
        private bool CanAttemptBackstabbing(Pawn initiator, Pawn targetAlly)
        {
            // Check if both pawns are conscious and in a good mental state
            if (initiator.Downed || targetAlly.Downed || initiator.InMentalState || targetAlly.InMentalState)
            {
                return false;
            }
            
            // Check if they're in a private enough location for a conversation
            // (This is a simplification - we could add more complex social location logic)
            
            return true;
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

        /// <summary>
        /// Calculates an age-based modifier for drama interactions
        /// Adults (age > 17) have decreased chance of targeting children (age < 13) - up to 80% decrease based on recipient age
        /// Children (age < 18) have increased chance when both are under 18 - 50% increase
        /// </summary>
        private float CalculateAgeModifier(Pawn initiator, Pawn recipient)
        {
            int initiatorAge = initiator.ageTracker.AgeBiologicalYears;
            int recipientAge = recipient.ageTracker.AgeBiologicalYears;

            // If initiator is adult (>17) and recipient is child (<13), decrease chance based on recipient age
            if (initiatorAge > 17 && recipientAge < 13)
            {
                // The younger the child, the more we decrease the chance (up to 80% at age 0)
                float ageBasedReduction = (float)recipientAge / 13f;  // From 0 (age 0) to 1 (age 13)
                float reductionFactor = 1.0f - (0.8f * (1.0f - ageBasedReduction));  // From 0.2 (age 0) to 1.0 (age 13)
                return Math.Max(0.2f, reductionFactor);  // At least 20% of original chance
            }
            // If both are under 18 (children), increase chance by 50%
            else if (initiatorAge < 18 && recipientAge < 18)
            {
                return 1.5f;  // 50% increase
            }

            // Default no modifier
            return 1.0f;
        }
    }
}