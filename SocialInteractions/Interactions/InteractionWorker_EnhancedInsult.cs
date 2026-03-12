using RimWorld;
using Verse;
using Verse.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SocialInteractions
{
    /// <summary>
    /// Interaction worker for enhanced insults with severity levels based on opinion
    /// </summary>
    public class InteractionWorker_EnhancedInsult : InteractionWorker
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
                SLog.Warning("[SocialInteractions] InteractionWorker_EnhancedInsult: Initiator or recipient is null, skipping interaction.");
                // Initialize output parameters and return early
                letterText = null;
                letterLabel = null;
                letterDef = null;
                lookTargets = LookTargets.Invalid;
                return;
            }

            // Check if the recipient is a child and misbehavior is enabled for insult triggering
            if (recipient.RaceProps.Humanlike && ChildrenMisbehaviorManager.IsChild(recipient) && SocialInteractions.Settings.enableChildrenMisbehavior)
            {
                SLog.Message(string.Format("[SocialInteractions] Child {0} received enhanced insult from {1}",
                    recipient.LabelShort, initiator.LabelShort));

                // Give the child a chance to go cry to their parent about being insulted
                TryStartCryingToParent(recipient, initiator);
            }

            // Determine the severity of the insult based on the initiator's opinion of the recipient
            InsultSeverity severity = DetermineInsultSeverity(initiator, recipient);
            
            // Check for potential social fight escalation based on severity and recipient's state
            CheckForSocialFightEscalation(initiator, recipient, severity);
            
            // Generate an appropriate subject based on the severity and whether a fight occurred
            // The fight escalation method may have started a fight, so check the current MentalState
            bool fightOccurred = recipient.MentalState != null && recipient.MentalState.def == MentalStateDefOf.SocialFighting;
            string subject = GenerateInsultSubject(initiator, recipient, severity, fightOccurred);
            
            // Apply appropriate thoughts based on severity and relationship
            ApplyInsultThoughts(initiator, recipient, severity);
            
            // Handle the LLM interaction with the generated subject
            SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, SI_InteractionDefOf.EnhancedInsult, subject);
            
            // Call the base Interacted method to create the normal log entry using XML rules
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            
            // Create a custom log entry for the enhanced insult interaction to ensure it's properly recorded in social history
            try
            {
                // Use the same fight determination as used for the subject
                PlayLogEntry_EnhancedInsult enhancedInsultLogEntry = new PlayLogEntry_EnhancedInsult(SI_InteractionDefOf.EnhancedInsult, initiator, recipient, extraSentencePacks, severity, fightOccurred);
                
                // Add the entry to the play log to update the social history
                if (Find.PlayLog != null)
                {
                    Find.PlayLog.Add(enhancedInsultLogEntry);
                }
            }
            catch (System.Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] InteractionWorker_EnhancedInsult: Failed to add enhanced insult to play log: {0}", ex.Message));
            }
            
            // Log the interaction
            SLog.Message(string.Format("[SocialInteractions] Enhanced insult: {0} insulted {1} with {2} severity", 
                initiator.LabelShort, recipient.LabelShort, severity.ToString()));
        }

        private InsultSeverity DetermineInsultSeverity(Pawn initiator, Pawn recipient)
        {
            if (initiator.relations == null)
            {
                return InsultSeverity.Mild; // Default to mild if no relations data
            }
            
            int opinionOfRecipient = initiator.relations.OpinionOf(recipient);
            
            // Determine severity based on opinion thresholds
            if (opinionOfRecipient <= -50)
            {
                return InsultSeverity.Violent;  // Very negative opinion
            }
            else if (opinionOfRecipient <= -30)
            {
                return InsultSeverity.Severe;   // Quite negative opinion
            }
            else if (opinionOfRecipient <= -10)
            {
                return InsultSeverity.Moderate; // Somewhat negative opinion
            }
            else
            {
                return InsultSeverity.Mild;     // Neutral or positive opinion (backhanded or subtle insults)
            }
        }
        
        private string GenerateInsultSubject(Pawn initiator, Pawn recipient, InsultSeverity severity, bool ledToFight = false)
        {
            if (ledToFight)
            {
                // If the insult led to a fight, create a subject that reflects the escalation
                switch (severity)
                {
                    case InsultSeverity.Violent:
                        return string.Format("A violent verbal attack by {0} against {1} that escalates into a physical fight. The insult is extremely harsh and personal, causing {1} to retaliate physically.", 
                            initiator.LabelShort, recipient.LabelShort);
                            
                    case InsultSeverity.Severe:
                        return string.Format("A severe insult by {0} directed at {1} that results in a physical confrontation. The harsh comment is enough to provoke {1} into fighting.", 
                            initiator.LabelShort, recipient.LabelShort);
                            
                    case InsultSeverity.Moderate:
                        return string.Format("A moderately harsh comment by {0} about {1} that unexpectedly escalates to physical violence. {1} responds aggressively to the criticism.", 
                            initiator.LabelShort, recipient.LabelShort);
                            
                    case InsultSeverity.Mild:
                    default:
                        return string.Format("A subtle or backhanded comment by {0} toward {1} that somehow results in a physical fight. Despite its mild nature, the remark triggers a violent response.", 
                            initiator.LabelShort, recipient.LabelShort);
                }
            }
            else
            {
                // Original subject generation when no fight occurred
                switch (severity)
                {
                    case InsultSeverity.Violent:
                        return string.Format("A violent verbal attack by {0} against {1}. The insult is extremely harsh and personal, reflecting deep hatred and animosity.", 
                            initiator.LabelShort, recipient.LabelShort);
                            
                    case InsultSeverity.Severe:
                        return string.Format("A severe insult by {0} directed at {1}. The comment is harsh and intended to cause significant emotional harm.", 
                            initiator.LabelShort, recipient.LabelShort);
                            
                    case InsultSeverity.Moderate:
                        return string.Format("A moderately harsh comment by {0} about {1}. The remark is critical but not extremely vicious.", 
                            initiator.LabelShort, recipient.LabelShort);
                            
                    case InsultSeverity.Mild:
                    default:
                        return string.Format("A subtle or backhanded comment by {0} toward {1}. The remark may seem casual but contains an underlying criticism or slight.", 
                            initiator.LabelShort, recipient.LabelShort);
                }
            }
        }
        
        private void ApplyInsultThoughts(Pawn initiator, Pawn recipient, InsultSeverity severity)
        {
            // Apply different thoughts based on the severity of the insult
            
            // For the recipient (the target of the insult)
            ThoughtDef insultThought = GetInsultThoughtForSeverity(severity);
            if (insultThought != null && recipient.needs != null && recipient.needs.mood != null)
            {
                recipient.needs.mood.thoughts.memories.TryGainMemory(insultThought, initiator);
            }
            
            // For the initiator (could have thoughts about being mean/venting)
            if (initiator.needs != null && initiator.needs.mood != null)
            {
                // If the initiator has a trait that makes them enjoy being mean, they might get a positive thought
                if (HasTraitThatEnjoysNegativeInteractions(initiator))
                {
                    ThoughtDef enjoyedInsultingThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("SadisticBoast");
                    if (enjoyedInsultingThought != null)
                    {
                        initiator.needs.mood.thoughts.memories.TryGainMemory(enjoyedInsultingThought, recipient);
                    }
                }
            }
        }
        
        private ThoughtDef GetInsultThoughtForSeverity(InsultSeverity severity)
        {
            switch (severity)
            {
                case InsultSeverity.Violent:
                    // Try more severe insult thoughts first, fallback to Insulted if not available
                    ThoughtDef deeplyInsulted = DefDatabase<ThoughtDef>.GetNamedSilentFail("DeeplyInsulted");
                    if (deeplyInsulted != null)
                        return deeplyInsulted;
                    else
                        return DefDatabase<ThoughtDef>.GetNamed("Insulted");
                    
                case InsultSeverity.Severe:
                    ThoughtDef badlyInsulted = DefDatabase<ThoughtDef>.GetNamedSilentFail("BadlyInsulted");
                    if (badlyInsulted != null)
                        return badlyInsulted;
                    else
                        return DefDatabase<ThoughtDef>.GetNamed("Insulted");
                    
                case InsultSeverity.Moderate:
                    return DefDatabase<ThoughtDef>.GetNamed("Insulted");
                    
                case InsultSeverity.Mild:
                default:
                    // For mild insults, try to find a subtle thought or just return null for no specific thought
                    ThoughtDef slightedThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Slighted");
                    if (slightedThought != null)
                    {
                        return slightedThought;
                    }
                    // If Slighted doesn't exist, we might want to return a less severe thought or null
                    // In this case, we'll return null which means no special thought will be applied
                    // for very mild insults, letting the LLM interaction be the main effect
                    return null;
            }
        }
        
        /// <summary>
        /// Checks if the insult should escalate to a social fight based on severity and recipient's state
        /// </summary>
        private void CheckForSocialFightEscalation(Pawn initiator, Pawn recipient, InsultSeverity severity)
        {
            if (initiator == null || recipient == null || recipient.Map == null)
            {
                return;
            }
            
            // Calculate the chance of escalation based on insult severity
            float escalationChance = 0f;
            
            switch (severity)
            {
                case InsultSeverity.Violent:
                    escalationChance = 0.4f; // 40% chance for violent insults
                    break;
                case InsultSeverity.Severe:
                    escalationChance = 0.2f; // 20% chance for severe insults
                    break;
                case InsultSeverity.Moderate:
                    escalationChance = 0.05f; // 5% chance for moderate insults
                    break;
                case InsultSeverity.Mild:
                    escalationChance = 0.01f; // 1% chance for mild insults
                    break;
            }
            
            // Modify escalation chance based on recipient's mood
            if (recipient.needs != null && recipient.needs.mood != null)
            {
                float mood = recipient.needs.mood.CurLevelPercentage;
                // Lower mood increases chance of fight escalation
                if (mood < 0.3f) // Very low mood
                {
                    escalationChance *= 2.0f;
                }
                else if (mood < 0.5f) // Low mood
                {
                    escalationChance *= 1.5f;
                }
            }
            
            // Modify escalation chance based on recipient's traits
            if (HasTraitThatProvokesFights(recipient))
            {
                escalationChance *= 1.8f; // Recipient with fight-provoking traits
            }
            
            // Modify escalation chance based on recipient's opinion of initiator
            if (recipient.relations != null)
            {
                int opinionOfInitiator = recipient.relations.OpinionOf(initiator);
                if (opinionOfInitiator < -40) // Already very negative opinion
                {
                    escalationChance *= 2.0f;
                }
                else if (opinionOfInitiator < -20) // Quite negative opinion
                {
                    escalationChance *= 1.5f;
                }
            }
            
            // Check for random escalation
            if (Rand.Value < escalationChance)
            {
                // Try to start a social fight
                TryStartSocialFight(initiator, recipient);
            }
        }
        
        /// <summary>
        /// Tries to start a social fight between the initiator and recipient
        /// </summary>
        private void TryStartSocialFight(Pawn initiator, Pawn recipient)
        {
            if (initiator == null || recipient == null || recipient.Map == null)
            {
                return;
            }
            
            // Check if both pawns are able to fight (not downed, not in mental state, etc.)
            if (!recipient.CanReach(initiator, PathEndMode.Touch, Danger.Deadly, false, false) ||
                recipient.Downed || recipient.IsPrisoner || recipient.IsSlave ||
                recipient.InAggroMentalState || recipient.MentalState != null ||
                initiator.InAggroMentalState || initiator.MentalState != null)
            {
                // Can't start a social fight if either pawn can't reach or engage
                return;
            }
            
            // Use the proper method to start social fight for both pawns, similar to vanilla RimWorld
            // This follows the same pattern as the decompiled StartSocialFight method
            if (PawnUtility.ShouldSendNotificationAbout(recipient) || PawnUtility.ShouldSendNotificationAbout(initiator))
            {
                Messages.Message("MessageSocialFight".Translate(recipient.LabelShort, initiator.LabelShort, 
                    recipient.Named("PAWN1"), initiator.Named("PAWN2")), 
                    recipient, MessageTypeDefOf.ThreatSmall);
            }
            
            // Start social fighting mental state for both pawns
            recipient.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.SocialFighting, null, false, false, false, initiator);
            initiator.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.SocialFighting, null, false, false, false, recipient);
            
            // Record the tale
            TaleRecorder.RecordTale(TaleDefOf.SocialFight, recipient, initiator);
            
            SLog.Message(string.Format("[SocialInteractions] Social fight started: {0} started fighting {1} after enhanced insult", 
                recipient.LabelShort, initiator.LabelShort));
        }
        
        private bool HasTraitThatEnjoysNegativeInteractions(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }
            
            // Check for traits that would make someone enjoy insulting others
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait != null && trait.def != null)
                {
                    string traitLabel = trait.def.defName.ToLower();
                    string traitLabelDisplay = trait.Label.ToLower();
                    
                    // Check for sadistic, abrasive, or similar traits that enjoy negative interactions
                    if (traitLabel.Contains("sadist") || 
                        traitLabel.Contains("abrasive") || 
                        traitLabel.Contains("psychopath") ||
                        traitLabel.Contains("bully") ||
                        traitLabelDisplay.Contains("sadist") || 
                        traitLabelDisplay.Contains("abrasive") || 
                        traitLabelDisplay.Contains("psychopath") ||
                        traitLabelDisplay.Contains("bully"))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Checks if a pawn has traits that make them more likely to fight back when insulted
        /// </summary>
        private bool HasTraitThatProvokesFights(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }
            
            // Check for traits that make a pawn more likely to fight when insulted
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait != null && trait.def != null)
                {
                    string traitLabel = trait.def.defName.ToLower();
                    string traitLabelDisplay = trait.Label.ToLower();
                    
                    // Check for traits like abrasive, quick-tempered, jealous, etc. that might cause fights
                    if (traitLabel.Contains("abrasive") || 
                        traitLabel.Contains("psychopath") ||
                        traitLabel.Contains("jealous") ||
                        traitLabel.Contains("hothead") ||
                        traitLabel.Contains("quicktemper") ||
                        traitLabel.Contains("shorttemper") ||
                        traitLabelDisplay.Contains("abrasive") || 
                        traitLabelDisplay.Contains("psychopath") ||
                        traitLabelDisplay.Contains("jealous") ||
                        traitLabelDisplay.Contains("hot headed") ||
                        traitLabelDisplay.Contains("hot-tempered") ||
                        traitLabelDisplay.Contains("short temper"))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        private void TryStartCryingToParent(Pawn child, Pawn insulter)
        {
            // Give the child a chance to go cry to their parent (for now, let's say 70% chance)
            if (Rand.Value < 0.7f) // 70% chance for now, can be configurable
            {
                // Find the child's parent or most liked pawn
                Pawn parent = FindParentOrMostLikedPawn(child);

                if (parent != null && parent != insulter) // Don't go to the insulter
                {
                    // Create the job for the child to go cry to the parent
                    Job cryJob = JobMaker.MakeJob(SI_JobDefOf.ChildGoCryToParent, parent);
                    cryJob.count = 0; // 0 = insult-related distress
                    child.jobs.TryTakeOrderedJob(cryJob);

                    SLog.Message(string.Format("[SocialInteractions] Child {0} is going to cry to parent {1} after being insulted by {2}",
                        child.LabelShort, parent.LabelShort, insulter.LabelShort));
                }
                else if (parent == null)
                {
                    SLog.Message(string.Format("[SocialInteractions] Child {0} has no parent to cry to after being insulted", child.LabelShort));
                }
                else
                {
                    SLog.Message(string.Format("[SocialInteractions] Child {0} cannot cry to insulter {1}", child.LabelShort, parent.LabelShort));
                }
            }
        }

        private Pawn FindParentOrMostLikedPawn(Pawn child)
        {
            if (child.relations == null)
            {
                return null;
            }

            // First, look for parents
            foreach (Pawn potentialParent in child.Map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (potentialParent != null && !potentialParent.Dead && potentialParent.Spawned)
                {
                    if (child.relations.DirectRelationExists(PawnRelationDefOf.Parent, potentialParent))
                    {
                        return potentialParent;
                    }
                }
            }

            // If no parents found, look for the most liked pawn (highest opinion of the child)
            Pawn mostLiked = null;
            int highestOpinion = int.MinValue;

            foreach (Pawn potentialPawn in child.Map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (potentialPawn != null && !potentialPawn.Dead && potentialPawn.Spawned && potentialPawn != child)
                {
                    int opinion = (child.relations != null) ? child.relations.OpinionOf(potentialPawn) : 0;
                    if (opinion > highestOpinion)
                    {
                        highestOpinion = opinion;
                        mostLiked = potentialPawn;
                    }
                }
            }

            return mostLiked;
        }
    }

    /// <summary>
    /// Enum for different levels of insult severity
    /// </summary>
    public enum InsultSeverity
    {
        Mild,      // Subtle, backhanded, or barely noticeable insults
        Moderate,  // Noticeable criticism or mildly harsh comments
        Severe,    // Clearly harsh and mean-spirited insults
        Violent    // Extremely vicious, personal attacks
    }
}