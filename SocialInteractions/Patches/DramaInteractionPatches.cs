using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace SocialInteractions
{
    /// <summary>
    /// Patch to handle all drama interactions during social interactions like Chitchat
    /// Provides a unified system for different types of drama mechanics with priority management.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), "TryInteractWith")]
    public static class DramaInteractionHandlerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn_InteractionsTracker __instance, Pawn recipient, InteractionDef intDef, ref bool __result)
        {
            // The __instance is the pawn whose interactions tracker is being called (the initiator)
            Pawn initiator = (Pawn)AccessTools.Field(typeof(Pawn_InteractionsTracker), "pawn").GetValue(__instance);

            // Early check: if drama feature is not enabled, skip everything else
            if (!SocialInteractions.Settings.enableDrama)
            {
                return;
            }

            // If the basic interaction didn't succeed, skip
            if (!__result)
                return;

            // Only consider social interactions that might be good contexts for drama
            if (intDef != InteractionDefOf.Chitchat &&
                intDef != InteractionDefOf.DisturbingChat &&
                intDef != InteractionDefOf.Insult)
                return;

            // Process drama interactions in priority order
            // First: Check for lover's quarrel (highest priority for insults between partners)
            if (TryProcessLoversQuarrel(initiator, recipient, intDef))
            {
                // Lover's quarrel was triggered, exit to prevent other drama interactions
                return;
            }

            // Check for highest priority drama interaction that fits the context
            if (TryProcessBadmouthingGossip(initiator, recipient, intDef))
            {
                // Badmouthing/gossip was triggered, exit to prevent other drama interactions
                return;
            }

            // Add other drama interaction checks here in priority order
            // For example, enhanced chitchat insults would be checked next
            if (TryProcessEnhancedChitchatInsult(initiator, recipient, intDef))
            {
                // Enhanced chitchat insult was triggered, exit to prevent other drama interactions
                return;
            }

            // Finally, check for admiration interactions (lowest priority)
            // This is for pawns with low social influence to admire/push their favorite influencers
            if (TryProcessAdmiration(initiator, recipient, intDef))
            {
                // Admiration was triggered, exit to prevent other drama interactions
                return;
            }

            // Check for make-up/apologizing interactions (higher priority than admiration but lower than badmouthing/insults)
            // This allows pawns to attempt reconciliation after conflicts
            if (TryProcessMakeUp(initiator, recipient, intDef))
            {
                // Make-up interaction was triggered, exit to prevent other drama interactions
                return;
            }

            // Additional drama interaction checks would go here as needed
        }

        /// <summary>
        /// Attempts to process admiration interaction where pawns with low social influence
        /// praise or promote those they view as leaders based on shared interests/traits
        /// Lowest priority interaction
        /// </summary>
        private static bool TryProcessAdmiration(Pawn initiator, Pawn recipient, InteractionDef intDef)
        {
            // Check if we should potentially initiate an admiration interaction
            // based on the initiator's low social influence and the recipient's appeal
            bool shouldInitiate = ShouldInitiateAdmiration(initiator, recipient);

            if (shouldInitiate)
            {
                // Check if this interaction type is appropriate for admiration
                if (intDef == InteractionDefOf.Chitchat || intDef == InteractionDefOf.DisturbingChat)
                {
                    // Always allow the admiration interaction to occur regardless of LLM settings
                    // The InteractionWorker_Admiration will handle both LLM and non-LLM cases internally
                    InteractionDef admirationDef = DefDatabase<InteractionDef>.GetNamedSilentFail("Admiration");
                    if (admirationDef != null)
                    {
                        InteractionWorker_Admiration admirationWorker = new InteractionWorker_Admiration();

                        string letterText, letterLabel;
                        LetterDef letterDef;
                        LookTargets lookTargets;

                        // Call the interaction worker's Interacted method directly
                        admirationWorker.Interacted(initiator, recipient, null, out letterText, out letterLabel, out letterDef, out lookTargets);

                        // The interaction worker will handle logging the interaction properly
                        return true; // Indicate that we processed this interaction
                    }
                    else
                    {
                        // If the interaction def is not available, fall back to simple interaction
                        string subject = string.Format("{0} expressed admiration for {1}", initiator.LabelShort, recipient.LabelShort);
                        SocialInteractions.HandleInteraction(initiator, recipient, intDef, subject);

                        return true; // Indicate that we processed this interaction
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if an admiration interaction should be initiated based on shared interests/traits
        /// and social influence levels
        /// </summary>
        private static bool ShouldInitiateAdmiration(Pawn initiator, Pawn recipient)
        {
            if (initiator == null || recipient == null)
            {
                return false;
            }

            // Check if the initiator has traits that prevent positive admiration (unlikely, but possible)
            if (HasTraitThatPreventsAdmiration(initiator))
            {
                return false;
            }

            // Calculate the social influence of the initiator
            float initiatorInfluence = CalculateSocialInfluence(initiator);

            // Only initiators with low social influence should seek to admire others
            if (initiatorInfluence > 10f) // Adjust this threshold as needed
            {
                return false; // High-influence pawns don't need to suck up to others
            }

            // Check if the recipient has traits that the initiator shares or admires
            bool hasAttractionToRecipient = HasAttractionToTarget(initiator, recipient);

            // Base chance for admiration from settings
            float admirationChance = SocialInteractions.Settings.baseAdmirationChance;

            // Increase chance if there's a strong attraction (shared traits, valued skills, etc.)
            if (hasAttractionToRecipient)
            {
                admirationChance *= SocialInteractions.Settings.admirationAttractionMultiplier;
            }

            // Consider the relationship between initiator and recipient
            if (initiator.relations != null)
            {
                int opinionOfRecipient = initiator.relations.OpinionOf(recipient);

                // Higher opinion increases chance of admiration
                if (opinionOfRecipient > 20) // Positive opinion above threshold
                {
                    admirationChance *= SocialInteractions.Settings.admirationPositiveOpinionMultiplier;
                }
            }

            // Apply social skill modifier - higher social skill of recipient increases admiration chance
            if (recipient.skills != null)
            {
                var recipientSocialSkill = recipient.skills.GetSkill(SkillDefOf.Social);
                if (recipientSocialSkill != null && recipientSocialSkill.Level > 8)
                {
                    // For every skill level above 8, increase chance by 10%
                    float socialSkillMultiplier = 1.0f + ((recipientSocialSkill.Level - 8) * 0.1f);
                    admirationChance *= socialSkillMultiplier;
                }
            }

            // Apply age-based modifiers
            admirationChance *= CalculateAgeModifier(initiator, recipient);

            float randValue = Rand.Value;
            return randValue < admirationChance;
        }

        /// <summary>
        /// Checks if the initiator has traits that would prevent admiration
        /// </summary>
        private static bool HasTraitThatPreventsAdmiration(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }

            // Check for traits that would prevent admiration (e.g., arrogant, narcissistic, etc.)
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait != null && trait.def != null)
                {
                    string traitLabel = trait.def.defName.ToLower();
                    string traitLabelDisplay = trait.Label.ToLower();

                    // Check both defName and display label to catch various trait formats
                    if (traitLabel.Contains("arrogant") ||
                        traitLabel.Contains("narcissist") ||
                        traitLabel.Contains("egotist") ||
                        traitLabel.Contains("selfish") ||
                        // Also check the display label in case defName doesn't match
                        traitLabelDisplay.Contains("arrogant") ||
                        traitLabelDisplay.Contains("narcissist") ||
                        traitLabelDisplay.Contains("egotist") ||
                        traitLabelDisplay.Contains("selfish"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the initiator has attraction to the recipient based on
        /// shared traits, skills, or roles
        /// </summary>
        private static bool HasAttractionToTarget(Pawn initiator, Pawn recipient)
        {
            // Check for shared traits
            bool hasSharedTrait = HasSharedTrait(initiator, recipient);

            // Check if initiator values recipient's skills
            bool valuesRecipientsSkills = ValuesSkillsOfRecipient(initiator, recipient);

            // Check if recipient has a role/initiator admires
            bool recipientIsAdmirable = IsAdmirableToInitiator(initiator, recipient);

            return hasSharedTrait || valuesRecipientsSkills || recipientIsAdmirable;
        }

        /// <summary>
        /// Checks if two pawns share significant traits
        /// </summary>
        private static bool HasSharedTrait(Pawn initiator, Pawn recipient)
        {
            if (initiator.story == null || initiator.story.traits == null ||
                recipient.story == null || recipient.story.traits == null)
            {
                return false;
            }

            var initiatorTraits = initiator.story.traits.allTraits;
            var recipientTraits = recipient.story.traits.allTraits;

            foreach (var initTrait in initiatorTraits)
            {
                if (initTrait == null || initTrait.def == null) continue;

                foreach (var recTrait in recipientTraits)
                {
                    if (recTrait == null || recTrait.def == null) continue;

                    // Check for matching or compatible traits
                    if (initTrait.def.defName == recTrait.def.defName)
                    {
                        return true; // Same trait
                    }

                    // Check for compatible traits based on social groupings
                    string initLabel = initTrait.def.defName.ToLower();
                    string recLabel = recTrait.def.defName.ToLower();

                    // Examples of compatible pairs (expand as needed)
                    if ((initLabel.Contains("optimist") && recLabel.Contains("optimist")) ||
                        (initLabel.Contains("pessimist") && recLabel.Contains("pessimist")) ||
                        (initLabel.Contains("kind") && recLabel.Contains("kind")) ||
                        (initLabel.Contains("abrasive") && recLabel.Contains("abrasive")))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the initiator values the recipient's skills
        /// (initiator has low skill in area where recipient excels)
        /// </summary>
        private static bool ValuesSkillsOfRecipient(Pawn initiator, Pawn recipient)
        {
            if (initiator.skills == null || recipient.skills == null)
            {
                return false;
            }

            // Check if the initiator has low skill in an area where the recipient excels
            // This would make the initiator more likely to admire the recipient's skill
            foreach (var skill in recipient.skills.skills)
            {
                if (skill.Level >= 8) // High skill level
                {
                    var initiatorSkill = initiator.skills.GetSkill(skill.def);
                    if (initiatorSkill != null && initiatorSkill.Level < 5) // Low skill in same area
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the recipient has qualities that make them admirable to the initiator
        /// </summary>
        private static bool IsAdmirableToInitiator(Pawn initiator, Pawn recipient)
        {
            // Check for specific roles or statuses that the initiator might admire
            // For example, if initiator lacks a skill that recipient has in abundance

            // Check if recipient has high social skill (potential social leader)
            if (recipient.skills != null)
            {
                var socialSkill = recipient.skills.GetSkill(SkillDefOf.Social);
                if (socialSkill != null && socialSkill.Level >= 8)
                {
                    // Even if the initiator doesn't have low social skill, they might admire someone with high social skill
                    // This is important for the "social leaders" aspect of admiration
                    return true;
                }
            }

            // Check for other admirable roles like medical, combat, etc.
            if (recipient.skills != null)
            {
                // Check medical skill
                var medicalSkill = recipient.skills.GetSkill(SkillDefOf.Medicine);
                if (medicalSkill != null && medicalSkill.Level >= 12) // Very high medical skill
                {
                    // If initiator has low medical skill or has medical needs, they might admire this
                    var initiatorMedicalSkill = initiator.skills != null ? initiator.skills.GetSkill(SkillDefOf.Medicine) : null;
                    if (initiatorMedicalSkill != null && initiatorMedicalSkill.Level < 5)
                    {
                        return true;
                    }
                }

                // Check shooting or melee skills (combat leaders)
                var shootingSkill = recipient.skills.GetSkill(SkillDefOf.Shooting);
                var meleeSkill = recipient.skills.GetSkill(SkillDefOf.Melee);
                if ((shootingSkill != null && shootingSkill.Level >= 10) ||
                    (meleeSkill != null && meleeSkill.Level >= 10)) // High combat skill
                {
                    // If initiator has low combat skill, they might admire this
                    var initiatorShootingSkill = initiator.skills != null ? initiator.skills.GetSkill(SkillDefOf.Shooting) : null;
                    var initiatorMeleeSkill = initiator.skills != null ? initiator.skills.GetSkill(SkillDefOf.Melee) : null;
                    if ((initiatorShootingSkill != null && initiatorShootingSkill.Level < 5) ||
                        (initiatorMeleeSkill != null && initiatorMeleeSkill.Level < 5))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Calculate social influence using the utility class
        /// </summary>
        private static float CalculateSocialInfluence(Pawn target)
        {
            if (target == null || target.Map == null || target.Map.mapPawns == null) return 0f;

            return SocialInfluenceUtility.CalculateSocialInfluence(target, target.Map.mapPawns.FreeColonistsAndPrisoners);
        }

        private static bool ShouldInitiateBadmouthing(Pawn initiator, Pawn recipient)
        {
            if (initiator == null || recipient == null)
            {
                return false;
            }

            // Check if the initiator has traits that prevent badmouthing
            bool preventsBadmouthing = HasTraitThatPreventsBadmouthing(initiator);
            if (preventsBadmouthing)
            {
                return false; // Kind pawns and similar never do this
            }

            // Check if the initiator has traits that encourage badmouthing
            float badmouthingChance = SocialInteractions.Settings.baseBadmouthingChance; // Base chance from settings
            bool encouragesBadmouthing = HasTraitThatEncouragesBadmouthing(initiator);

            if (encouragesBadmouthing)
            {
                badmouthingChance = SocialInteractions.Settings.traitEncouragedBadmouthingChance; // Chance for trait-encouraged pawns from settings
            }

            // Additional chance based on relationship factors
            // If the initiator has a particularly low opinion of someone else in the colony,
            // they might be more likely to badmouth that person
            Pawn leastFavoritePawn = GetLeastFavoritePawn(initiator);
            if (leastFavoritePawn != null && leastFavoritePawn != recipient)
            {
                // If the initiator has someone they really dislike, they're more likely to badmouth
                int opinionOfLeastFavorite = 0;
                if (initiator.relations != null)
                {
                    opinionOfLeastFavorite = initiator.relations.OpinionOf(leastFavoritePawn);
                }

                if (opinionOfLeastFavorite < SocialInteractions.Settings.badmouthingLowOpinionThreshold) // Significantly negative opinion based on settings
                {
                    badmouthingChance += SocialInteractions.Settings.badOpinionAdditionalChance; // Additional chance from settings
                }
            }

            // Apply age-based modifiers
            badmouthingChance *= CalculateAgeModifier(initiator, recipient);

            float randValue = Rand.Value;
            return randValue < badmouthingChance;
        }

        /// <summary>
        /// Calculates an age-based modifier for drama interactions
        /// Adults (age > 17) have decreased chance of targeting children (age < 13) - up to 80% decrease based on recipient age
        /// Children (age < 18) have increased chance when both are under 18 - 50% increase
        /// </summary>
        private static float CalculateAgeModifier(Pawn initiator, Pawn recipient)
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

        private static bool HasTraitThatPreventsBadmouthing(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }

            // Kind pawns never engage in badmouthing
            Trait kindTrait = pawn.story.traits.GetTrait(TraitDefOf.Kind);
            if (kindTrait != null)
            {
                return true;
            }

            return false;
        }

        private static bool HasTraitThatEncouragesBadmouthing(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }

            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait != null && trait.def != null)
                {
                    string traitLabel = trait.def.defName.ToLower(); // Use defName for more accuracy
                    string traitLabelDisplay = trait.Label.ToLower();

                    // Check both defName and display label to catch various trait formats
                    if (traitLabel.Contains("jealous") ||
                        traitLabel.Contains("abrasive") ||
                        traitLabel.Contains("psychopath") ||
                        traitLabel.Contains("mean") ||
                        traitLabel.Contains("cold") ||
                        traitLabel.Contains("arrogant") ||
                        traitLabel.Contains("bitch") ||  // Some mods may have this trait
                        traitLabel.Contains("bully") ||
                        traitLabel.Contains("selfish") ||
                        // Also check the display label in case defName doesn't match
                        traitLabelDisplay.Contains("jealous") ||
                        traitLabelDisplay.Contains("abrasive") ||
                        traitLabelDisplay.Contains("psychopath") ||
                        traitLabelDisplay.Contains("mean") ||
                        traitLabelDisplay.Contains("cold") ||
                        traitLabelDisplay.Contains("arrogant"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Pawn GetLeastFavoritePawn(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || pawn.Map.mapPawns == null)
            {
                return null;
            }

            return SocialInteractions.GetWeightedLeastFavoritePawn(pawn);
        }

        /// <summary>
        /// Attempts to process a lover's quarrel interaction when romantic partners would insult each other
        /// Highest priority - intercepts insults between partners
        /// </summary>
        private static bool TryProcessLoversQuarrel(Pawn initiator, Pawn recipient, InteractionDef intDef)
        {
            // Only intercept Insult interactions
            if (intDef != InteractionDefOf.Insult)
            {
                return false;
            }

            // Check if the two pawns are in a romantic relationship
            if (!InteractionWorker_LoversQuarrel.AreRomanticPartners(initiator, recipient))
            {
                return false;
            }

            // Trigger the lover's quarrel interaction
            SLog.Message(string.Format("[SocialInteractions] Intercepting insult between romantic partners {0} and {1} - triggering lover's quarrel",
                initiator.LabelShort, recipient.LabelShort));

            InteractionWorker_LoversQuarrel quarrelWorker = new InteractionWorker_LoversQuarrel();

            string letterText, letterLabel;
            LetterDef letterDef;
            LookTargets lookTargets;

            // Call the interaction worker's Interacted method directly
            quarrelWorker.Interacted(initiator, recipient, null, out letterText, out letterLabel, out letterDef, out lookTargets);

            return true; // Indicate that we processed this interaction
        }

        /// <summary>
        /// Attempts to process badmouthing or gossip interaction based on opinion dynamics
        /// Higher priority than other drama interactions
        /// </summary>
        private static bool TryProcessBadmouthingGossip(Pawn initiator, Pawn recipient, InteractionDef intDef)
        {
            // Check if we should potentially replace this interaction with badmouthing/gossip
            // based on traits and settings
            bool shouldInitiate = ShouldInitiateBadmouthing(initiator, recipient);

            if (shouldInitiate)
            {
                // The original interaction already succeeded, so we'll trigger the badmouthing directly
                // through the InteractionWorker_Badmouthing system by calling the interaction worker directly

                // Directly call the interaction worker method to trigger the badmouthing/gossip interaction
                InteractionDef badmouthingDef = DefDatabase<InteractionDef>.GetNamedSilentFail("Badmouthing");
                if (badmouthingDef != null)
                {
                    // Create a new instance of the InteractionWorker_Badmouthing and call Interacted directly
                    // The interaction worker will now determine if this is gossip (shared negative opinions)
                    // or badmouthing (one-sided negative opinions) based on the pawns' opinions of the target
                    InteractionWorker_Badmouthing badmouthingWorker = new InteractionWorker_Badmouthing();

                    string letterText, letterLabel;
                    LetterDef letterDef;
                    LookTargets lookTargets;

                    // Call the interaction worker's Interacted method directly
                    badmouthingWorker.Interacted(initiator, recipient, null, out letterText, out letterLabel, out letterDef, out lookTargets);

                    // The interaction worker will handle logging the interaction properly
                    // No need to manually add to play log here since the interaction worker handles it
                    return true; // Indicate that we processed this interaction
                }
            }

            return false; // Indicate that we didn't process this interaction
        }

        /// <summary>
        /// Attempts to process enhanced chitchat insult interaction
        /// Lower priority than badmouthing/gossip
        /// </summary>
        private static bool TryProcessEnhancedChitchatInsult(Pawn initiator, Pawn recipient, InteractionDef intDef)
        {
            // Check if we should potentially enhance this chitchat with an insult
            // based on traits, mood, or relationship dynamics
            bool shouldInitiate = ShouldInitiateEnhancedChitchatInsult(initiator, recipient);

            if (shouldInitiate)
            {
                // Instead of a full badmouthing interaction, we'll trigger our new EnhancedInsult interaction
                // This provides more nuanced insult handling with severity based on opinion
                if (intDef == InteractionDefOf.Chitchat || intDef == InteractionDefOf.DisturbingChat)
                {
                    // Check if LLM interactions are enabled for EnhancedInsult
                    if (SocialInteractions.IsLlmInteractionEnabled(SI_InteractionDefOf.EnhancedInsult))
                    {
                        // Let the EnhancedInsult interaction worker handle the interaction with severity-based subjects
                        InteractionDef enhancedInsultDef = DefDatabase<InteractionDef>.GetNamedSilentFail("EnhancedInsult");
                        if (enhancedInsultDef != null)
                        {
                            InteractionWorker_EnhancedInsult enhancedInsultWorker = new InteractionWorker_EnhancedInsult();

                            string letterText, letterLabel;
                            LetterDef letterDef;
                            LookTargets lookTargets;

                            // Call the interaction worker's Interacted method directly - this handles severity and subject generation
                            enhancedInsultWorker.Interacted(initiator, recipient, null, out letterText, out letterLabel, out letterDef, out lookTargets);

                            // The interaction worker will handle logging the interaction properly
                            return true; // Indicate that we processed this interaction with an enhanced insult
                        }
                    }
                    else
                    {
                        // If LLM is not enabled for EnhancedInsult, show a default bubble with a generic subject
                        string subject = string.Format("{0} made a negative comment to {1}", initiator.LabelShort, recipient.LabelShort);
                        SocialInteractions.HandleInteraction(initiator, recipient, intDef, subject);

                        return true; // Indicate that we processed this interaction
                    }
                }
            }

            return false;
        }

        private static bool ShouldInitiateEnhancedChitchatInsult(Pawn initiator, Pawn recipient)
        {
            if (initiator == null || recipient == null)
            {
                return false;
            }

            // Check if the initiator has traits that prevent negative interactions
            bool preventsNegativeInteractions = HasTraitThatPreventsBadmouthing(initiator);
            if (preventsNegativeInteractions)
            {
                return false; // Kind pawns and similar never do this
            }

            // Base chance for enhanced chitchat insults from settings
            float insultChance = SocialInteractions.Settings.baseEnhancedChitchatInsultChance;

            // Modify chance based on mood using settings
            if (initiator.needs != null && initiator.needs.mood != null)
            {
                float mood = initiator.needs.mood.CurLevelPercentage;
                // Lower mood increases chance of negative comments in conversation
                if (mood < 0.4f) // Below 40% mood
                {
                    insultChance *= SocialInteractions.Settings.enhancedChitchatInsultMoodMultiplierBad;
                }
                else if (mood > 0.8f) // Above 80% mood
                {
                    insultChance *= SocialInteractions.Settings.enhancedChitchatInsultMoodMultiplierGood;
                }
            }

            // Modify chance based on opinion of recipient using settings
            if (initiator.relations != null)
            {
                int opinionOfRecipient = initiator.relations.OpinionOf(recipient);
                // Lower opinion of recipient increases chance of negative comments
                if (opinionOfRecipient < -20) // Significantly negative opinion
                {
                    insultChance *= SocialInteractions.Settings.enhancedChitchatInsultOpinionMultiplierVeryNegative;
                }
                else if (opinionOfRecipient > 30) // Significantly positive opinion
                {
                    insultChance *= SocialInteractions.Settings.enhancedChitchatInsultOpinionMultiplierVeryPositive;
                }
            }

            // Modify chance based on traits that encourage negative interactions using settings
            if (HasTraitThatEncouragesBadmouthing(initiator))
            {
                insultChance *= SocialInteractions.Settings.enhancedChitchatInsultTraitMultiplier;
            }

            // Modify chance based on relationship differences
            // For example, if initiator has very different opinions about others compared to recipient
            float opinionDifferenceFactor = CalculateOpinionDifferenceFactor(initiator, recipient);
            insultChance *= opinionDifferenceFactor;

            // Apply age-based modifiers
            insultChance *= CalculateAgeModifier(initiator, recipient);

            float randValue = Rand.Value;
            return randValue < insultChance;
        }

        /// <summary>
        /// Calculates a factor based on how different the initiator's and recipient's opinions are
        /// Higher differences increase the chance of negative comments
        /// </summary>
        private static float CalculateOpinionDifferenceFactor(Pawn initiator, Pawn recipient)
        {
            if (initiator.Map == null || initiator.Map.mapPawns.FreeColonistsAndPrisoners.Count <= 1)
            {
                return 1.0f; // No difference factor if insufficient pawns
            }

            float totalDifference = 0f;
            int comparisonCount = 0;

            // Compare opinions about other pawns in the colony
            foreach (Pawn otherPawn in initiator.Map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (otherPawn == initiator || otherPawn == recipient)
                {
                    continue; // Skip self and the recipient
                }

                // Get opinions of both initiator and recipient about this other pawn
                int initiatorOpinion = initiator.relations != null ? initiator.relations.OpinionOf(otherPawn) : 0;
                int recipientOpinion = recipient.relations != null ? recipient.relations.OpinionOf(otherPawn) : 0;

                // Calculate the absolute difference in opinions
                float difference = Math.Abs(initiatorOpinion - recipientOpinion);

                // If their opinions are very different (more than 20 points), that contributes to tension
                if (difference > 20)
                {
                    totalDifference += difference / 100f; // Normalize to reasonable values
                    comparisonCount++;
                }
            }

            if (comparisonCount == 0)
            {
                return 1.0f; // No significant differences found
            }

            float averageDifference = totalDifference / comparisonCount;

            // Return a factor greater than 1.0 if there are significant opinion differences
            // This makes it more likely to have negative comments when pawns have very different opinions
            return 1.0f + (averageDifference * SocialInteractions.Settings.enhancedChitchatInsultOpinionDifferenceMultiplier); // Scale the impact using settings
        }

        /// <summary>
        /// Attempts to process make-up/apologizing interaction where pawns attempt to clear up
        /// misunderstandings and reconcile after conflicts
        /// </summary>
        private static bool TryProcessMakeUp(Pawn initiator, Pawn recipient, InteractionDef intDef)
        {
            // Check if we should potentially initiate a make-up/apologizing interaction
            // based on negative feelings between pawns and reconciliation opportunities
            bool shouldInitiate = ShouldInitiateMakeUp(initiator, recipient);

            if (shouldInitiate)
            {
                // Check if this interaction type is appropriate for make-up interactions
                // Make-up interactions can happen during Chitchat, DisturbingChat or even Insult interactions
                if (intDef == InteractionDefOf.Chitchat)
                {
                    // Let the MakeUp interaction worker handle the interaction regardless of LLM setting
                    // The interaction worker will internally handle both LLM and non-LLM cases
                    InteractionDef makeUpDef = DefDatabase<InteractionDef>.GetNamedSilentFail("MakeUp");
                    if (makeUpDef != null)
                    {
                        InteractionWorker_MakeUp makeUpWorker = new InteractionWorker_MakeUp();

                        string letterText, letterLabel;
                        LetterDef letterDef;
                        LookTargets lookTargets;

                        // Call the interaction worker's Interacted method directly
                        makeUpWorker.Interacted(initiator, recipient, null, out letterText, out letterLabel, out letterDef, out lookTargets);

                        // The interaction worker will handle logging the interaction properly
                        return true; // Indicate that we processed this interaction
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if a make-up/apologizing interaction should be initiated based on
        /// negative modifiers from previous conflicts or backstabbing
        /// </summary>
        private static bool ShouldInitiateMakeUp(Pawn initiator, Pawn recipient)
        {
            if (initiator == null || recipient == null)
            {
                return false;
            }

            // Check if recipient has negative thoughts about the initiator from past conflicts
            bool hasNegativeModifier = HasNegativeModifierFromConflict(initiator, recipient);

            if (!hasNegativeModifier)
            {
                return false; // No point in making up if there are no negative feelings
            }

            // Check if the initiator has traits that encourage positive reconciliation
            bool hasKindnessTrait = HasTraitThatEncouragesKindness(initiator);

            // Calculate base chance for make-up attempts
            float makeUpChance = SocialInteractions.Settings.baseMakeUpChance;

            // Increase chance if the initiator has kindness-related traits
            if (hasKindnessTrait)
            {
                makeUpChance *= 1.5f; // Kind pawns are more likely to try to make up
            }

            // Consider the relationship between initiator and recipient
            if (initiator.relations != null)
            {
                int opinionOfRecipient = initiator.relations.OpinionOf(recipient);

                // If initiator has a positive opinion of recipient despite negative modifiers,
                // they might be more motivated to make amends
                if (opinionOfRecipient > 10)
                {
                    makeUpChance *= SocialInteractions.Settings.makeUpPositiveOpinionMultiplier;
                }
                // If initiator has a very negative opinion, chance might be lower
                else if (opinionOfRecipient < -20)
                {
                    makeUpChance *= SocialInteractions.Settings.makeUpNegativeOpinionMultiplier;
                }
            }

            // Consider mood of the initiator
            if (initiator.needs != null && initiator.needs.mood != null)
            {
                float mood = initiator.needs.mood.CurLevelPercentage;
                // Slightly more likely to make up when mood is not too low or too high
                if (mood > 0.3f && mood < 0.7f)
                {
                    makeUpChance *= 1.2f; // 20% increase in reasonable mood range
                }
            }

            // Base chance for make-up attempts from settings
            float randValue = Rand.Value;
            return randValue < makeUpChance;
        }

        /// <summary>
        /// Checks if the pawn has traits that encourage positive reconciliation behaviors
        /// </summary>
        private static bool HasTraitThatEncouragesKindness(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }

            // Kind pawns are more likely to try to make up
            Trait kindTrait = pawn.story.traits.GetTrait(TraitDefOf.Kind);
            if (kindTrait != null)
            {
                return true;
            }

            // Check for other positive traits
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait != null && trait.def != null)
                {
                    string traitLabel = trait.def.defName.ToLower();
                    string traitLabelDisplay = trait.Label.ToLower();

                    if (traitLabel.Contains("forgiving") ||
                        traitLabel.Contains("calm") ||
                        traitLabelDisplay.Contains("forgiving") ||
                        traitLabelDisplay.Contains("calm"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the recipient has negative thoughts about the initiator from past conflicts
        /// like backstabbing or other negative interactions
        /// </summary>
        private static bool HasNegativeModifierFromConflict(Pawn initiator, Pawn recipient)
        {
            if (recipient.needs == null || recipient.needs.mood == null || recipient.needs.mood.thoughts == null)
            {
                return false;
            }

            // Check for specific negative thoughts that originated from the initiator
            List<Thought_Memory> thoughtsList = recipient.needs.mood.thoughts.memories.Memories;
            foreach (Thought_Memory thought in thoughtsList)
            {
                if (thought.otherPawn == initiator)
                {
                    // Check if thought is negative and significant enough to warrant reconciliation
                    if (thought.def.stages != null && thought.def.stages.Count > 0)
                    {
                        int opinionOffset = thought.CurStageIndex < thought.def.stages.Count ?
                            (int)thought.def.stages[thought.CurStageIndex].baseOpinionOffset : 0;
                        if (opinionOffset < -5) // If the thought creates a negative opinion offset
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}