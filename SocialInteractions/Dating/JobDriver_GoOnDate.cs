using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;
using System;
using Verse.Utility;

namespace SocialInteractions
{
    public class JobDriver_GoOnDate : JobDriver
    {
        private Pawn Partner
        {
            get { return (Pawn)this.job.targetA.Thing; }
        }
        
        private JobDef lastKnownInitiatorJobDef = null;
        private const int JoyJobJoinDelay = 180; // 3 seconds delay before trying to join joy job
        private JobDef partnerJoyJobDef = null; // Track the joy job the partner is doing

        /// <summary>
        /// Checks if a pawn is still valid for dating activities
        /// </summary>
        /// <param name="pawn">The pawn to check</param>
        /// <returns>True if the pawn is valid for dating, false otherwise</returns>
        private bool IsPawnValidForDating(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.Downed)
            {
                return false;
            }
            
            if (pawn.InMentalState || pawn.health == null || pawn.health.capacities == null)
            {
                return false;
            }
            
            // Check if the pawn is capable of being awake (basic health check)
            if (!pawn.health.capacities.CanBeAwake)
            {
                return false;
            }
            
            // Check if the pawn is drafted
            if (pawn.Drafted)
            {
                return false;
            }
            
            // Check if the pawn is on a date in the Lovin stage
            // If so, they should not be doing other jobs
            if (DatingManager.IsOnDate(pawn))
            {
                Date date = DatingManager.GetDateWith(pawn);
                if (date != null && date.Stage == DateStage.Lovin)
                {
                    // Allow the DateLovin job to start
                    // If the pawn is in any other job, they should not be doing it
                    if (pawn.jobs != null && pawn.jobs.curJob != null && pawn.jobs.curJob.def != SI_JobDefOf.DateLovin)
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Add null check to prevent NullReferenceException
            if (this.pawn == null)
            {
                SLog.Warning("[SocialInteractions] JobDriver_GoOnDate: pawn is null in TryMakePreToilReservations.");
                return false;
            }
            
            // Use the helper method to check if the pawn is valid for dating
            if (!IsPawnValidForDating(this.pawn))
            {
                SLog.Warning("[SocialInteractions] JobDriver_GoOnDate: pawn is not valid for dating in TryMakePreToilReservations.");
                return false;
            }
            
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Add null check for pawn
            if (this.pawn == null)
            {
                SLog.Warning("[SocialInteractions] JobDriver_GoOnDate: pawn is null in MakeNewToils, ending job.");
                yield break;
            }

            // Fail if the partner is null or despawned
            this.FailOnDespawnedOrNull(TargetIndex.A);

            // Check if recipient is within range
            Toil rangeCheck = new Toil();
            rangeCheck.initAction = () =>
            {
                Pawn recipient = this.Partner;
                // Add null checks
                if (recipient == null || this.pawn == null)
                {
                    SLog.Message("[SocialInteractions] JobDriver_GoOnDate: Pawn or recipient is null in rangeCheck, ending job.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                int maxDistance = SocialInteractions.Settings.maxDistanceForDate; // 50x50 tiles
                if ((Math.Abs(this.pawn.Position.x - recipient.Position.x) + Math.Abs(this.pawn.Position.z - recipient.Position.z)) > maxDistance)
                {
                    this.EndJobWith(JobCondition.Incompletable);
                }
            };
            rangeCheck.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return rangeCheck;

            // Go to the partner
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            // Ask for the date
            Toil askToil = new Toil();
            askToil.initAction = () => {
                Pawn recipient = this.Partner;
                if (recipient == null || this.pawn == null)
                {
                    SLog.Message("[SocialInteractions] JobDriver_GoOnDate: Pawn or recipient is null in askToil, ending job.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Re-validate the recipient before asking for the date
                // Check if the recipient is still valid for dating
                if (recipient.Downed || !recipient.Awake() || recipient.InMentalState || recipient.Drafted || DatingManager.IsOnDate(recipient))
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Recipient {0} is no longer available (Downed/Drafted/OnDate), cancelling.", recipient.LabelShort));
                    Find.PlayLog.Add(new PlayLogEntry_Interaction(DefDatabase<InteractionDef>.GetNamed("DateRejected"), this.pawn, this.Partner, null));
                    DatingManager.RejectDate(this.pawn, this.Partner);
                    SocialInteractions.HandleNonStoppingInteraction(this.pawn, this.Partner, SI_InteractionDefOf.DateRejected, SpeechBubbleManager.GetDateRejectionSubject(this.pawn, this.Partner));
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Calculate acceptance chance based on opinion and mood
                float baseChance = 0.95f; // 95% base chance
                
                // Factor in the recipient's opinion of the initiator
                int opinion = recipient.relations.OpinionOf(this.pawn);
                float opinionFactor = System.Math.Max(0f, System.Math.Min(1f, 0.5f + (opinion / 100f))); // Convert opinion to a 0-1 factor
                
                // Factor in the recipient's current mood
                float moodFactor = 1.0f;
                if (recipient.needs != null && recipient.needs.mood != null)
                {
                    // Convert mood level (0.0 to 1.0) to a factor between 0.5 and 1.5
                    // When mood is very low (0.0), factor is 0.5 (reduces chance)
                    // When mood is neutral (0.5), factor is 1.0 (no effect)
                    // When mood is very high (1.0), factor is 1.5 (increases chance)
                    moodFactor = 0.5f + recipient.needs.mood.CurLevelPercentage;
                }
                
                // Calculate final chance as base chance adjusted by both factors
                float finalChance = baseChance * opinionFactor * moodFactor;
                
                // Cap the chance at 100%
                finalChance = System.Math.Min(finalChance, 1.0f);
                
                // Roll for acceptance
                float roll = Rand.Value;
                bool accepted = roll < finalChance;
                
                SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Acceptance roll for {0} asking {1}. Rolled: {2:F3}, Chance: {3:F3}, Accepted: {4}", 
                    this.pawn.Name.ToStringShort, recipient.Name.ToStringShort, roll, finalChance, accepted));

                if (accepted)
                {
                    Find.PlayLog.Add(new PlayLogEntry_Interaction(DefDatabase<InteractionDef>.GetNamed("DateAccepted"), this.pawn, this.Partner, null));
                    DatingManager.StartDate(this.pawn, this.Partner);
                    Messages.Message(string.Format("{0} and {1} are now going on a date.", this.pawn.Name.ToStringShort, this.Partner.Name.ToStringShort), new LookTargets(this.pawn, this.Partner), MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Find.PlayLog.Add(new PlayLogEntry_Interaction(DefDatabase<InteractionDef>.GetNamed("DateRejected"), this.pawn, this.Partner, null));
                    DatingManager.RejectDate(this.pawn, this.Partner);
                    SocialInteractions.HandleNonStoppingInteraction(this.pawn, this.Partner, SI_InteractionDefOf.DateRejected, SpeechBubbleManager.GetDateRejectionSubject(this.pawn, this.Partner));
                    this.EndJobWith(JobCondition.Incompletable);
                }
            };
            askToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return askToil;

            // Find a joy job and assign jobs
            Toil findJoyJobAndAssign = new Toil();
            findJoyJobAndAssign.initAction = () =>
            {
                // Add null checks
                if (this.pawn == null || this.Partner == null)
                {
                    SLog.Message("[SocialInteractions] JobDriver_GoOnDate: Pawn or Partner is null in findJoyJobAndAssign, ending job.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                // Find a suitable joy job for the initiator
                Job joyJob = FindJoyJobFor(this.pawn, this.Partner);
                if (joyJob == null)
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Could not find joy job for {0} and {1}, ending date.", this.pawn.LabelShort, this.Partner.LabelShort));
                    SocialInteractions.HandleNonStoppingInteraction(this.pawn, this.Partner, SI_InteractionDefOf.DateRejected, 
                        string.Format("{0} accepted the date, but they couldn't find anything to do together.", this.Partner.LabelShort));
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                // --- Trigger LLM Interaction with correct subject ---
                string dateSubject = "";
                if (joyJob.def.defName == "SocialRelaxDate")
                {
                    if (joyJob.targetA.Thing != null)
                        dateSubject = string.Format("{0} has accepted {1}'s invitation to hang out and now they are hanging out at the {2}.", this.Partner.LabelShort, this.pawn.LabelShort, joyJob.targetA.Thing.Label);
                    else
                        dateSubject = string.Format("{0} has accepted {1}'s invitation to hang out and now they are going for a walk.", this.Partner.LabelShort, this.pawn.LabelShort);
                }
                else if (joyJob.def.defName == "PesterPrisoner")
                {
                    string prisonerName = joyJob.targetA.Thing != null ? joyJob.targetA.Thing.LabelShort : "someone";
                    dateSubject = string.Format("{0} has accepted {1}'s invitation for a date! Now they are hanging out together by pestering {2}.", this.Partner.LabelShort, this.pawn.LabelShort, prisonerName);
                }
                else
                {
                    dateSubject = SpeechBubbleManager.GetDateSubject(this.pawn, this.Partner, joyJob.targetA);
                }

                
                SocialInteractions.HandleNonStoppingInteraction(this.pawn, this.Partner, SI_InteractionDefOf.DateAccepted, dateSubject, true);
                // --- End LLM Interaction ---
                
                // Create the FollowAndWatch job for the partner
                // Create the job for the partner
                Job partnerJob;
                if (joyJob.def.defName == "PesterPrisoner")
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Selected PesterPrisoner activity. Assigning PesterPrisonerPartner to {0}.", this.Partner.LabelShort));
                    partnerJob = JobMaker.MakeJob(SI_JobDefOf.PesterPrisonerPartner, joyJob.targetA, this.pawn);
                }
                else if (joyJob.def.defName == "AbusiveThreesome")
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Selected AbusiveThreesome activity. Assigning AbusiveThreesomeParticipant to {0}.", this.Partner.LabelShort));
                    // Mapping: targetA = Abuser (initiator), targetB = Victim (joyJob targetA)
                    partnerJob = JobMaker.MakeJob(SI_JobDefOf.AbusiveThreesomeParticipant, this.pawn, joyJob.targetA);
                }
                else
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: Selected {0} activity. Assigning FollowAndWatchInitiator to {1}.", joyJob.def.defName, this.Partner.LabelShort));
                    partnerJob = JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, this.pawn);
                }
                
                // Start the partner's job
                this.Partner.jobs.StartJob(partnerJob, JobCondition.InterruptForced);
                
                // Store the job def for monitoring
                lastKnownInitiatorJobDef = joyJob.def;
                
                // Start the initiator's joy job
                this.pawn.jobs.StartJob(joyJob, JobCondition.InterruptForced);
                
                // End this job successfully since we've set up the date
                this.EndJobWith(JobCondition.Succeeded);
            };
            findJoyJobAndAssign.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return findJoyJobAndAssign;
        }

        private void TryHavePartnerJoinJoyActivity(JobDef joyJobDef)
        {
            // Check if the partner's joy need is high enough that they don't want to join
            if (this.Partner.needs != null && this.Partner.needs.joy != null && 
                this.Partner.needs.joy.CurLevelPercentage >= 0.95f)
            {
                return;
            }
            
            // Find the joy giver for this job def
            JoyGiverDef initiatorJoyGiver = null;
            foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
            {
                if (joyGiver.jobDef == joyJobDef)
                {
                    initiatorJoyGiver = joyGiver;
                    break;
                }
            }
            
            if (initiatorJoyGiver == null)
            {
                return;
            }
            
            // Try to give the partner the same joy job as the initiator
            Job partnerJoyJob = initiatorJoyGiver.Worker.TryGiveJob(this.Partner);
            if (partnerJoyJob == null)
            {
                return;
            }
            
            // Check if the target locations match or are nearby
            bool targetsMatch = false;
            if (partnerJoyJob.targetA.Thing != null && this.pawn.CurJob.targetA.Thing != null)
            {
                targetsMatch = partnerJoyJob.targetA.Thing == this.pawn.CurJob.targetA.Thing;
            }
            else if (partnerJoyJob.targetA.Cell.IsValid && this.pawn.CurJob.targetA.Cell.IsValid)
            {
                targetsMatch = partnerJoyJob.targetA.Cell.DistanceTo(this.pawn.CurJob.targetA.Cell) <= 7f;
            }
            
            if (targetsMatch)
            {
                // Track the joy job we're starting for the partner
                partnerJoyJobDef = partnerJoyJob.def;
                
                // Enqueue the joy job and then interrupt the current job for a smooth transition
                this.Partner.jobs.jobQueue.EnqueueFirst(partnerJoyJob);
                this.Partner.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }

        private Job FindJoyJobFor(Pawn initiator, Pawn partner)
        {
            if (initiator == null || partner == null)
            {
                SLog.Warning("[SocialInteractions] JobDriver_GoOnDate: initiator or partner is null in FindJoyJobFor, returning null.");
                return null;
            }

            // Get all joy givers
            IEnumerable<JoyGiverDef> joyGivers = DefDatabase<JoyGiverDef>.AllDefs.OrderByDescending(jg => jg.Worker.GetChance(initiator));

            // Create a list of joy givers with their weights (using base chance as weight)
            List<Pair<JoyGiverDef, float>> weightedJoyGivers = new List<Pair<JoyGiverDef, float>>();
            float totalWeight = 0f;

            foreach (JoyGiverDef joyGiverDef in joyGivers)
            {
                float weight = joyGiverDef.Worker.GetChance(initiator);
                
                if (weight > 0)
                {
                    weightedJoyGivers.Add(new Pair<JoyGiverDef, float>(joyGiverDef, weight));
                    totalWeight += weight;
                }
            }

            // If we have no valid joy givers, return null
            if (weightedJoyGivers.Count == 0 || totalWeight <= 0)
            {
                return null;
            }

            // Try multiple times to find a suitable job using weighted random selection
            const int maxAttempts = 10;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Select a joy giver using weighted random selection
                float randomValue = Rand.Value * totalWeight;
                float currentWeight = 0f;
                JoyGiverDef selectedJoyGiverDef = null;

                foreach (var pair in weightedJoyGivers)
                {
                    currentWeight += pair.Second;
                    if (randomValue <= currentWeight)
                    {
                        selectedJoyGiverDef = pair.First;
                        break;
                    }
                }

                // If we didn't select a joy giver (shouldn't happen), default to the first one
                if (selectedJoyGiverDef == null)
                {
                    selectedJoyGiverDef = weightedJoyGivers[0].First;
                }

                try
                {
                    // Try to get a job from this joy giver
                    Job joyJob = selectedJoyGiverDef.Worker.TryGiveJob(initiator);

                    if (joyJob != null)
                    {
                        // Check if partner accepts Pester Prisoner logic
                        if (joyJob.def == SI_JobDefOf.PesterPrisoner && JobDriver_PesterPrisoner.ShouldPartnerRefuse(partner))
                        {
                             // Partner refuses this specific activity, try another
                             continue;
                        }

                        // Skip jobs that don't have a valid target
                        if (joyJob.targetA.Thing == null && !joyJob.targetA.Cell.IsValid)
                        {
                            continue;
                        }
                        
                        // Determine the correct PathEndMode based on whether the target is a Thing or a Cell
                        PathEndMode pathEndMode = joyJob.targetA.HasThing ? PathEndMode.InteractionCell : PathEndMode.OnCell;

                        // Try to reserve the target for both pawns
                        if (initiator.CanReserveAndReach(joyJob.targetA, pathEndMode, Danger.None) &&
                            partner.CanReserveAndReach(joyJob.targetA, pathEndMode, Danger.None))
                        {
                            return joyJob;
                        }
                        // If we can't reserve it, we try again with a different joy giver
                    }
                }
                catch (NullReferenceException nre)
                {
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_GoOnDate: NullReferenceException while trying joy giver {0}: {1}", 
                        selectedJoyGiverDef.defName, nre.Message));
                    // Continue to the next attempt
                }
                catch (Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_GoOnDate: Exception while trying joy giver {0}: {1}", 
                        selectedJoyGiverDef.defName, ex.Message));
                    // Continue to the next attempt
                }
            }
            
            // --- Fallback: SocialRelaxDate ---
            // If we reach here, no vanilla joy giver worked.
            // We'll create a SocialRelaxDate job instead of returning null.
            SLog.Message(string.Format("[SocialInteractions] JobDriver_GoOnDate: No vanilla joy found for {0} and {1}, using SocialRelaxDate fallback.", 
                initiator.LabelShort, partner.LabelShort));

            IntVec3 fallbackSpot = initiator.Position;
            
            // For the fallback, we'll just find a random spot nearby to "walk around" during the date.
            // This is simple and always works.
            fallbackSpot = RCellFinder.RandomWanderDestFor(initiator, initiator.Position, 7f, null, Danger.None);
            
            if (fallbackSpot.IsValid)
            {
                return JobMaker.MakeJob(SI_JobDefOf.SocialRelaxDate, fallbackSpot);
            }

            return null;
        }
    }
}