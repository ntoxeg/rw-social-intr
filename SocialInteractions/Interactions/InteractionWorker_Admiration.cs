using RimWorld;
using Verse;
using Verse.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SocialInteractions
{
    /// <summary>
    /// Interaction worker for admiration/praise interactions where pawns with low social influence
    /// praise or promote those they view as leaders based on shared interests/traits
    /// </summary>
    public class InteractionWorker_Admiration : InteractionWorker
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
                SLog.Warning("[SocialInteractions] InteractionWorker_Admiration: Initiator or recipient is null, skipping interaction.");
                // Initialize output parameters and return early
                letterText = null;
                letterLabel = null;
                letterDef = null;
                lookTargets = LookTargets.Invalid;
                return;
            }

            // Determine the type of admiration interaction
            AdmirationType admirationType = DetermineAdmirationType(initiator, recipient);
            
            // Apply appropriate thoughts based on the interaction
            ApplyAdmirationThoughts(initiator, recipient, admirationType);
            
            // Attempt to increase the recipient's opinion of the initiator based on social skill
            OpinionChangeResult opinionChange = AttemptToIncreaseOpinion(initiator, recipient);
            
            // Generate an appropriate subject based on the admiration type and the outcome
            string subject = GenerateAdmirationSubject(initiator, recipient, admirationType, opinionChange);
            
            // Handle the LLM interaction with the generated subject that reflects the outcomes
            SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.Admiration, subject);
            
            // Call the base Interacted method to create the normal log entry using XML rules
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            
            // Create a custom log entry for the admiration interaction
            try
            {
                PlayLogEntry_Admiration admirationLogEntry = new PlayLogEntry_Admiration(SI_InteractionDefOf.Admiration, initiator, recipient, extraSentencePacks, admirationType);
                
                // Add the entry to the play log to update the social history
                if (Find.PlayLog != null)
                {
                    Find.PlayLog.Add(admirationLogEntry);
                }
            }
            catch (System.Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] InteractionWorker_Admiration: Failed to add admiration to play log: {0}", ex.Message));
            }
            
            // Log the interaction
            // SLog.Message(string.Format("[SocialInteractions] Admiration: {0} expressed admiration toward {1}", 
                // initiator.LabelShort, recipient.LabelShort));
        }

        private AdmirationType DetermineAdmirationType(Pawn initiator, Pawn recipient)
        {
            // Determine what kind of admiration based on the relationship and shared interests
            if (initiator.story == null || recipient.story == null)
            {
                return AdmirationType.GeneralPraise; // Default if we can't analyze traits
            }

            // Check for shared traits or values
            bool hasSharedTrait = HasSharedTrait(initiator, recipient);
            bool initiatorValuesRecipientsSkills = ValuesSkillsOfRecipient(initiator, recipient);
            bool recipientIsInspirational = IsInspirational(recipient);
            
            // Prioritize based on shared interests
            if (hasSharedTrait && initiatorValuesRecipientsSkills)
            {
                return AdmirationType.SharedInterestPraise;
            }
            else if (initiatorValuesRecipientsSkills)
            {
                return AdmirationType.SkillBasedAdmiration;
            }
            else if (recipientIsInspirational)
            {
                return AdmirationType.InspirationalPraise;
            }
            else
            {
                return AdmirationType.GeneralPraise;
            }
        }
        
        private bool HasSharedTrait(Pawn initiator, Pawn recipient)
        {
            if (initiator.story.traits == null || recipient.story.traits == null)
            {
                return false;
            }
            
            // Compare key personality/moral traits
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
        
        private bool ValuesSkillsOfRecipient(Pawn initiator, Pawn recipient)
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
        
        private bool IsInspirational(Pawn pawn)
        {
            // For now, just return false as a placeholder
            // We could implement checks for specific statuses in the future
            return false;
        }
        
        private string GenerateAdmirationSubject(Pawn initiator, Pawn recipient, AdmirationType admirationType, OpinionChangeResult opinionChangeResult)
        {
            string baseDescription = "";
            string outcomeNarrative = "";

            // Generate base description based on admiration type with multiple variations
            switch (admirationType)
            {
                case AdmirationType.SharedInterestPraise:
                    string[] sharedInterestPhrases = {
                        string.Format("{0} expresses genuine appreciation to {1} about their shared beliefs and common ground", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} bonds with {1} over their mutual values and similar mindset", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} recognizes {1} as someone who shares their worldview", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} commends {1} for their alignment with shared principles", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} finds common cause with {1} in their shared outlook on life", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} feels a connection with {1} due to their compatible values", 
                            initiator.LabelShort, recipient.LabelShort)
                    };
                    baseDescription = sharedInterestPhrases[Rand.Range(0, sharedInterestPhrases.Length)];
                    break;

                case AdmirationType.SkillBasedAdmiration:
                    string[] skillBasedPhrases = {
                        string.Format("{0} openly acknowledges {1}'s superior capabilities in a field where {0} lacks experience", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} seeks to learn from {1}, who demonstrates remarkable expertise", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} recognizes {1} as highly skilled in an area where {0} struggles", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} expresses admiration for {1}'s impressive competence", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} looks up to {1} as someone with enviable talents", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} defers to {1}'s proven abilities and experience", 
                            initiator.LabelShort, recipient.LabelShort)
                    };
                    baseDescription = skillBasedPhrases[Rand.Range(0, skillBasedPhrases.Length)];
                    break;

                case AdmirationType.InspirationalPraise:
                    string[] inspirationalPhrases = {
                        string.Format("{0} looks to {1} as a source of motivation and positive example", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} seeks wisdom and guidance from {1}, who seems to embody ideal behavior", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} views {1} as a moral compass worth following", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} finds inspiration in {1}'s conduct and achievements", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} regards {1} as a beacon of how things should be done", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} turns to {1} as a respected figure to emulate", 
                            initiator.LabelShort, recipient.LabelShort)
                    };
                    baseDescription = inspirationalPhrases[Rand.Range(0, inspirationalPhrases.Length)];
                    break;

                case AdmirationType.GeneralPraise:
                default:
                    string[] generalPhrases = {
                        string.Format("{0} offers casual praise to {1}", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} gives {1} a compliment in passing", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} acknowledges {1} positively", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} speaks well of {1}", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} shows appreciation towards {1}", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("{0} recognizes {1} with a word of praise", 
                            initiator.LabelShort, recipient.LabelShort)
                    };
                    baseDescription = generalPhrases[Rand.Range(0, generalPhrases.Length)];
                    break;
            }

            // Generate outcome narrative based on the opinion change result with variations
            if (opinionChangeResult.success)
            {
                string[] successPhrases = {
                    string.Format("and {1} receives the recognition warmly", 
                        initiator.LabelShort, recipient.LabelShort),
                    string.Format("and {1} responds positively to the acknowledgment", 
                        initiator.LabelShort, recipient.LabelShort),
                    string.Format("and {1} appreciates being recognized", 
                        initiator.LabelShort, recipient.LabelShort),
                    string.Format("resulting in a positive reception from {1}", 
                        initiator.LabelShort, recipient.LabelShort),
                    string.Format("with {1} responding with gratitude", 
                        initiator.LabelShort, recipient.LabelShort),
                    string.Format("and {1} acknowledges the praise appropriately", 
                        initiator.LabelShort, recipient.LabelShort)
                };
                outcomeNarrative = successPhrases[Rand.Range(0, successPhrases.Length)];
            }
            else
            {
                if (opinionChangeResult.changeAmount < 0)
                {
                    string[] failureNegativePhrases = {
                        string.Format("but {1} receives {0}'s praise with skepticism", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("but {1} seems put off by {0}'s compliments", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("but {1} reacts badly to {0}'s attempts at flattery", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("and {1} appears to be turned away by {0}'s praise", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("but {1} is not receptive to {0}'s words", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("and {1} is left unimpressed by {0}'s flattery", 
                            initiator.LabelShort, recipient.LabelShort)
                    };
                    outcomeNarrative = failureNegativePhrases[Rand.Range(0, failureNegativePhrases.Length)];
                }
                else
                {
                    string[] failureNeutralPhrases = {
                        string.Format("but {1} remains unmoved by {0}'s efforts", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("but {1} doesn't seem to react much to {0}'s praises", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("but {1} shows little response to {0}'s compliments", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("resulting in only a mild response from {1}", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("but {1} shows a neutral reaction to {0}'s words", 
                            initiator.LabelShort, recipient.LabelShort),
                        string.Format("and {1} neither accepts nor rejects {0}'s praise", 
                            initiator.LabelShort, recipient.LabelShort)
                    };
                    outcomeNarrative = failureNeutralPhrases[Rand.Range(0, failureNeutralPhrases.Length)];
                }
            }

            
            return string.Format("{0}, {1}.", baseDescription, outcomeNarrative);
        }
        
        private void ApplyAdmirationThoughts(Pawn initiator, Pawn recipient, AdmirationType admirationType)
        {
            // Apply thoughts based on the admiration interaction
            
            // For the recipient (the target of admiration)
            ThoughtDef admirationReceived = GetAdmirationReceivedThoughtForType(admirationType);
            if (admirationReceived != null && recipient.needs != null && recipient.needs.mood != null)
            {
                recipient.needs.mood.thoughts.memories.TryGainMemory(admirationReceived, initiator);
            }
            
            // For the initiator (could have thoughts about admiring others or seeking approval)
            ThoughtDef seekingApproval = SI_ThoughtDefOf.SeekingApproval;
            if (seekingApproval != null && initiator.needs != null && initiator.needs.mood != null)
            {
                initiator.needs.mood.thoughts.memories.TryGainMemory(seekingApproval, recipient);
            }
        }
        
        private ThoughtDef GetAdmirationReceivedThoughtForType(AdmirationType admirationType)
        {
            switch (admirationType)
            {
                case AdmirationType.SharedInterestPraise:
                    return SI_ThoughtDefOf.AdmiredBySomeone; // Use our specific admiration thought
                case AdmirationType.SkillBasedAdmiration:
                    return SI_ThoughtDefOf.AdmiredBySomeone; // Use our specific admiration thought
                case AdmirationType.InspirationalPraise:
                    return SI_ThoughtDefOf.AdmiredBySomeone; // Use our specific admiration thought
                case AdmirationType.GeneralPraise:
                default:
                    return SI_ThoughtDefOf.AdmiredBySomeone; // Use our specific admiration thought
            }
        }
        
        /// <summary>
        /// Attempts to increase the recipient's opinion of the initiator based on the initiator's social skill
        /// </summary>
        private OpinionChangeResult AttemptToIncreaseOpinion(Pawn initiator, Pawn recipient)
        {
            if (initiator == null || recipient == null || initiator.relations == null || recipient.relations == null)
            {
                return new OpinionChangeResult(false, 0, "No opinion change attempted due to null references");
            }
            
            // Get the initiator's social skill level
            int socialSkillLevel = 0;
            if (initiator.skills != null)
            {
                socialSkillLevel = initiator.skills.GetSkill(SkillDefOf.Social).Level;
            }
            
            // Calculate success chance based on social skill (higher skill = higher chance)
            // Social skill ranges from 0-20, so we'll map this to a reasonable success probability
            float successChance = socialSkillLevel * 0.05f; // 5% per skill level (so max 100% at level 20)
            successChance = Math.Min(successChance, 0.95f); // Cap at 95% to avoid guaranteed success
            
            // Roll for success
            if (Rand.Value < successChance)
            {
                // Success! Apply a positive thought to increase the recipient's opinion of the initiator
                int opinionIncrease = (int)SocialInteractions.Settings.admirationOpinionIncreaseOnSuccess;
                ApplyOpinionChangeThought(recipient, initiator, opinionIncrease);
                
                string outcome = string.Format("success! {0}'s social skill (level {1}) helped increase {2}'s opinion by {3} points", 
                    initiator.LabelShort, socialSkillLevel, recipient.LabelShort, opinionIncrease);
                
                SLog.Message("[SocialInteractions] Admiration " + outcome);
                
                return new OpinionChangeResult(true, opinionIncrease, outcome);
            }
            else
            {
                // Failure - the admiration didn't land as well, maybe a small impact or none
                // Possibly apply a slightly negative thought if the admiration felt forced or awkward
                if (Rand.Value < SocialInteractions.Settings.admirationNegativeImpactChance) // Chance of slight negative impact if poorly executed
                {
                    int opinionChange = (int)SocialInteractions.Settings.admirationOpinionDecreaseOnFail; // Small negative impact
                    ApplyOpinionChangeThought(recipient, initiator, opinionChange);
                    
                    string outcome = string.Format("failure! {0}'s attempt to increase {1}'s opinion failed and may have had slight negative impact of {2} points", 
                        initiator.LabelShort, recipient.LabelShort, opinionChange);
                    
                    SLog.Message("[SocialInteractions] Admiration " + outcome);
                    
                    return new OpinionChangeResult(false, opinionChange, outcome);
                }
                else
                {
                    // No significant change - the admiration was neutral
                    string outcome = string.Format("neutral! {0}'s attempt to increase {1}'s opinion was neutral (success chance {2:F1}%)", 
                        initiator.LabelShort, recipient.LabelShort, successChance * 100);
                    
                    SLog.Message("[SocialInteractions] Admiration " + outcome);
                    
                    return new OpinionChangeResult(false, 0, outcome);
                }
            }
        }
        
        /// <summary>
        /// Applies an opinion-changing thought to affect how the recipient feels about the target
        /// </summary>
        private void ApplyOpinionChangeThought(Pawn recipient, Pawn target, int opinionOffset)
        {
            if (recipient.needs == null || recipient.needs.mood == null || recipient.needs.mood.thoughts == null)
            {
                return; // Recipient doesn't have mood needs or thoughts
            }
            
            // Create a custom temporary thought to represent the admiration effect
            Thought_MemorySocial thought = new Thought_MemorySocial();
            thought.otherPawn = target;
            
            if (opinionOffset > 0)
            {
                // Apply a positive thought about the target to increase recipient's opinion of the target
                // First try our custom admiration thought
                thought.def = SI_ThoughtDefOf.AdmiredBySomeone;
                if (thought.def == null)
                {
                    // Fallback to vanilla RimWorld social thoughts that increase opinion
                    thought.def = DefDatabase<ThoughtDef>.GetNamedSilentFail("KindWords");
                    if (thought.def == null)
                    {
                        thought.def = DefDatabase<ThoughtDef>.GetNamedSilentFail("DeepTalk");
                    }
                    if (thought.def == null)
                    {
                        // Fallback to another positive social thought
                        thought.def = DefDatabase<ThoughtDef>.GetNamed("LikedMyApparel");
                    }
                }
            }
            else
            {
                // Apply a negative thought about the target to decrease recipient's opinion of the target
                // Use a more fitting thought for when admiration is not well received
                thought.def = DefDatabase<ThoughtDef>.GetNamedSilentFail("Slighted");
                if (thought.def == null)
                {
                    // Fallback to another negative social thought
                    thought.def = DefDatabase<ThoughtDef>.GetNamed("HatesMyApparel");
                }
            }
            
            // Apply the thought memory to affect opinion
            if (thought.def != null)
            {
                recipient.needs.mood.thoughts.memories.TryGainMemory(thought, null);
            }
        }
    }
    
    /// <summary>
    /// Enum for different types of admiration interactions
    /// </summary>
    public enum AdmirationType
    {
        GeneralPraise,           // Basic admiration
        SharedInterestPraise,    // Based on shared traits/values
        SkillBasedAdmiration,    // Based on skill admiration
        InspirationalPraise      // Based on inspirational status
    }
    
    /// <summary>
    /// Structure to represent the result of an opinion change attempt
    /// </summary>
    public struct OpinionChangeResult
    {
        public bool success;
        public int changeAmount;
        public string outcomeDescription;
        
        public OpinionChangeResult(bool success, int changeAmount, string outcomeDescription)
        {
            this.success = success;
            this.changeAmount = changeAmount;
            this.outcomeDescription = outcomeDescription;
        }
    }
}