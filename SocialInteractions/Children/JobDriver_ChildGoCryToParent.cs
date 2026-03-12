using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JobDriver_ChildGoCryToParent : JobDriver
    {
        private const int BaseComfortDuration = 1800; // 30 seconds in ticks
        private int lastComfortInteractionTick = 0;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Child should be able to reserve the parent target
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail if the parent disappears or becomes invalid
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnMentalState(TargetIndex.A);
            // Fail if child is captured or recruited to another faction
            this.FailOn(() => pawn.HostFaction != null || (pawn.Faction != null && pawn.Faction != Faction.OfPlayer));
            // Fail if child gets drafted
            this.FailOn(() => pawn.Drafted);

            // Go to the parent initially
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Create the main comfort-seeking toil where the child follows and periodically seeks comfort from the parent
            Toil comfortToil = new Toil();
            comfortToil.initAction = delegate
            {
                Pawn parent = (Pawn)job.GetTarget(TargetIndex.A).Thing;

                if (parent == null)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_ChildGoCryToParent: Parent is null, ending job");
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (parent.Dead || parent.Downed)
                {
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_ChildGoCryToParent: Parent {0} is dead or downed, ending job", parent.LabelShort));
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Perform the comfort-seeking interaction
                TryStartComfortingInteraction(parent);
                lastComfortInteractionTick = Find.TickManager.TicksGame;
            };

            // Add a tick action to follow the parent if they move away, check valid interaction state, and check for draft/recruitment
            comfortToil.tickAction = delegate
            {
                Pawn parent = (Pawn)job.GetTarget(TargetIndex.A).Thing;

                // Check for recruitment/capture/draft every 60 ticks (roughly every second)
                if (pawn.IsHashIntervalTick(60))
                {
                    // End job if pawn has been recruited, captured, or drafted
                    if (pawn.HostFaction != null || (pawn.Faction != null && pawn.Faction != Faction.OfPlayer) || pawn.Drafted)
                    {
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_ChildGoCryToParent: Child {0} was recruited/captured/drafted, ending job", pawn.LabelShort));
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                        return;
                    }
                }

                if (parent != null && !parent.Dead && parent.Spawned)
                {
                    // Check if we should update the path to follow the parent
                    if (pawn.IsHashIntervalTick(60)) // Check every second
                    {
                        // If the parent has moved significantly, update our path to follow them
                        float distance = (pawn.Position - parent.Position).LengthHorizontal;
                        if (distance > 2f) // If more than 2 cells away
                        {
                            // Update path to follow parent
                            pawn.pather.StartPath(parent, PathEndMode.Touch);
                        }
                    }
                }
            };

            // Complete after a certain duration (the comfort-seeking job)
            comfortToil.defaultCompleteMode = ToilCompleteMode.Delay;
            comfortToil.defaultDuration = BaseComfortDuration;
            comfortToil.socialMode = RandomSocialMode.Normal; // Allow normal social interactions during comfort-seeking
            yield return comfortToil;
        }

        private void TryStartComfortingInteraction(Pawn parent)
        {
            if (pawn == null || parent == null || pawn.Map != parent.Map)
            {
                SLog.Warning("[SocialInteractions] JobDriver_ChildGoCryToParent: Null pawn or pawns on different maps, skipping comfort interaction");
                return;
            }

            // Check if we can reach the parent for interaction
            if (!pawn.CanReach(parent, PathEndMode.Touch, Danger.Deadly))
            {
                SLog.Warning("[SocialInteractions] JobDriver_ChildGoCryToParent: Cannot reach parent for comfort interaction");
                return;
            }

            // Calculate the success chance based on the parent's social skill
            int socialSkill = parent.skills.GetSkill(SkillDefOf.Social).Level;
            // Base success chance from 20% (lowest social skill) to 80% (highest social skill)
            float successChance = 0.2f + (socialSkill / 20.0f) * 0.6f; // 0.2 to 0.8

            bool comfortSuccess = Rand.Value < successChance;

            if (comfortSuccess)
            {
                // Successful comfort: remove the crying thought from the child and add a positive thought about being comforted
                if (pawn.needs != null && pawn.needs.mood != null)
                {
                    // Remove the crying thought if it exists
                    pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ChildThoughtDefOf.ChildCrying);

                    // Add a positive thought about being comforted by a loved one
                    // For now, just remove the negative thought
                }

                // Don't add a positive thought to the parent on successful comfort for now
                // To implement this properly, we'd create custom thoughts in a Def file

                SLog.Message(string.Format("[SocialInteractions] JobDriver_ChildGoCryToParent: Child {0} successfully comforted by {1} (Social Skill: {2}, Chance: {3:P}, Rolled: {4:P})",
                    pawn.LabelShort, parent.LabelShort, socialSkill, successChance, Rand.Value));
            }
            else
            {
                // Failed comfort: both child and parent get additional negative thoughts
                if (pawn.needs != null && pawn.needs.mood != null)
                {
                    // Child stays distressed and adds additional negative thought about remaining upset
                    try
                    {
                        var childDistressThought = DefDatabase<ThoughtDef>.GetNamed("ChildStillDistressed", false);
                        if (childDistressThought != null)
                        {
                            pawn.needs.mood.thoughts.memories.TryGainMemory(childDistressThought, null);
                        }
                    }
                    catch (System.Exception e)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] JobDriver_ChildGoCryToParent: Failed to add ChildStillDistressed thought: {0}", e.Message));
                    }
                }

                if (parent.needs != null && parent.needs.mood != null)
                {
                    // Parent feels inadequate for failing to comfort the child
                    try
                    {
                        var failureThought = DefDatabase<ThoughtDef>.GetNamed("FailedToComfortChild", false);
                        if (failureThought != null)
                        {
                            parent.needs.mood.thoughts.memories.TryGainMemory(failureThought, pawn);
                        }
                    }
                    catch (System.Exception e)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] JobDriver_ChildGoCryToParent: Failed to add FailedToComfortChild thought: {0}", e.Message));
                    }
                }

                SLog.Message(string.Format("[SocialInteractions] JobDriver_ChildGoCryToParent: Child {0} not comforted by {1} (Social Skill: {2}, Chance: {3:P}, Rolled: {4:P})",
                    pawn.LabelShort, parent.LabelShort, socialSkill, successChance, Rand.Value));
            }

            // Try to initiate the actual interaction between child and parent with a specific subject
            // This will properly call the interaction and trigger LLM if enabled
            string subject = FormatComfortSubject(pawn, parent, job, comfortSuccess);
            SocialInteractions.HandleNonStoppingInteraction(pawn, parent, InteractionDefOf.Chitchat, subject);
        }

        private string FormatComfortSubject(Pawn child, Pawn parent, Job job, bool comfortSuccess = true)
        {
            // Get the interaction type from the job's data if available (stored in count or tag)
            // For now, we'll use job's count as the interaction type indicator
            int comfortReasonType = job.count;  // 0 = after insult, 1 = after damage

            string reason = "";
            switch(comfortReasonType)
            {
                case 0: // After being insulted
                    reason = GetMostRecentInsultReason(child, parent);
                    break;
                case 1: // After taking damage
                    reason = "was hurt and felt scared";
                    break;
                default:
                    reason = "felt upset and needed comfort";
                    break;
            }

            if (comfortSuccess)
            {
                return string.Format("{0} came to {1} for comfort after being upset and felt better. Reason: {2}",
                    child.LabelShort, parent.LabelShort, reason);
            }
            else
            {
                return string.Format("{0} came to {1} for comfort after being upset but {1} couldn't help. Reason: {2}",
                    child.LabelShort, parent.LabelShort, reason);
            }
        }

        private string GetMostRecentInsultReason(Pawn child, Pawn parent)
        {
            // Try to get the most recent log entries that involve the child being insulted
            if (Find.PlayLog != null)
            {
                var allEntries = Find.PlayLog.AllEntries;

                // Find the most recent insult to this specific child
                Verse.LogEntry mostRecentInsult = null;
                int mostRecentTick = -1;

                foreach (var entry in allEntries)
                {
                    // Check if this is an interaction log entry using C# 5 compatible syntax
                    Verse.LogEntry interactionEntry = entry as Verse.LogEntry;
                    if (interactionEntry != null)
                    {
                        // We need to check if it's an insult without using direct LogEntry_Interaction type
                        // We'll use reflection to check the properties we need
                        string entryString = entry.ToString();

                        // Check if it's an insult interaction by looking at the string representation
                        if (entryString.Contains("Insult") || entryString.Contains("EnhancedInsult") || entryString.ToLower().Contains("insult"))
                        {
                            // Try to determine if the child was involved by checking the string representation
                            string childName = (child.Name != null) ? child.Name.ToStringShort : child.LabelShort;
                            if (entryString.Contains(childName) ||
                                entryString.Contains(child.LabelShort))
                            {
                                // Track the most recent insult entry
                                if (entry.Age <= 6000) // Only consider insults from the last 100 game seconds (6000 ticks)
                                {
                                    if (mostRecentInsult == null ||
                                        (Find.TickManager.TicksGame - entry.Age) > mostRecentTick)
                                    {
                                        mostRecentInsult = entry;
                                        mostRecentTick = Find.TickManager.TicksGame - entry.Age;
                                    }
                                }
                            }
                        }
                    }
                }

                // If we found the most recent insult, return the formatted log text
                if (mostRecentInsult != null)
                {
                    try
                    {
                        // Only call ToGameStringFromPOV if we know the child was part of the interaction
                        string logText = mostRecentInsult.ToGameStringFromPOV(child);
                        // Clean up rich text tags that might confuse the LLM
                        string cleanLogText = SocialInteractions.RemoveRichTextTags(logText);
                        return string.Format("was insulted: {0}", cleanLogText);
                    }
                    catch
                    {
                        // If there's still an issue with POV, return a generic message
                        return "was recently insulted by someone";
                    }
                }
            }

            // If no specific insult found in recent logs, return a generic message
            return "was recently insulted by someone";
        }
    }
}