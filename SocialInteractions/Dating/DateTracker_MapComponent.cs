using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class DateTracker_MapComponent : MapComponent
    {
        private int lastCleanupTick = 0;
        private const int CleanupInterval = 1800; // 30 seconds

        public DateTracker_MapComponent(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            DatingManager.ExposeData();
            Scribe_Values.Look(ref lastCleanupTick, "lastCleanupTick", 0);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            // Check dates every 180 ticks (3 seconds)
            if (Find.TickManager.TicksGame % 180 == 0)
            {
                // Check for stuck dates
                DatingManager.CheckForStuckDates(this.map);
                
                foreach (Date date in DatingManager.GetAllDates())
                {
                    Pawn initiator = date.Initiator;
                    Pawn partner = date.Partner;
                    
                    // Skip invalid dates
                    if (initiator == null || partner == null)
                    {
                        SLog.Warning("[SocialInteractions] DateTracker: Found date with null initiator or partner, ending date.");
                        DatingManager.EndDate(date);
                        continue;
                    }
                    
                    // Check if either pawn in the date is no longer in a valid state for dating
                    if (initiator.Dead || partner.Dead || 
                        initiator.Downed || partner.Downed || 
                        initiator.InMentalState || partner.InMentalState ||
                        !IsPawnHealthyForDating(initiator) || !IsPawnHealthyForDating(partner))
                    {
                        DatingManager.EndDate(date);
                        continue;
                    }

                    // Advance date stage based on joy need
                    if (date.Stage == DateStage.Joy)
                    {
                        // Check if the initiator's joy need is satisfied
                        if (initiator != null && initiator.needs != null && initiator.needs.joy != null && 
                            initiator.needs.joy.CurLevelPercentage >= 0.99f)
                        {
                            // Check if the date is already in the Lovin stage or beyond
                            Date dateStatus = DatingManager.GetDateWith(initiator);
                            if (dateStatus != null && dateStatus.Stage < DateStage.Lovin)
                            {
                                // Don't advance if we are in a special job that we want to keep in Joy/initial stage
                                if (initiator.CurJobDef == SI_JobDefOf.PesterPrisoner || initiator.CurJobDef == SI_JobDefOf.AbusiveThreesome)
                                {
                                    continue;
                                }

                                // Initiator's joy need is satisfied, advance to Lovin stage
                                DatingManager.AdvanceDateStage(initiator);
                            }
                        }
                        else
                        {
                            // Check if the initiator is doing a joy job
                            // Only check every 30 ticks (0.5 seconds) to reduce performance impact
                            bool isDoingJoyJob = false;
                            JobDef initiatorJoyJobDef = null;
                            if (initiator != null && initiator.CurJob != null && Find.TickManager.TicksGame % 30 == 0)
                            {
                                // Check if the job is a joy job
                                foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
                                {
                                    if (joyGiver.jobDef == initiator.CurJob.def)
                                    {
                                        isDoingJoyJob = true;
                                        initiatorJoyJobDef = initiator.CurJob.def;
                                        break;
                                    }
                                }
                            }
                            
                            // If the initiator is doing a joy job, check if the partner should join in or continue with their current activity
                            if (initiator != null && isDoingJoyJob && initiatorJoyJobDef != null)
                            {
                                HandlePartnerJoyActivity(initiator, partner, initiatorJoyJobDef);
                            }
                        }
                    }
                }
            }

            // Periodically clean up expired date cooldowns
            if (Find.TickManager.TicksGame - lastCleanupTick >= CleanupInterval)
            {
                DatingManager.CleanupExpiredDateCooldowns();
                lastCleanupTick = Find.TickManager.TicksGame;
            }
        }

        /// <summary>
        /// Checks if a pawn is healthy enough for dating activities
        /// </summary>
        /// <param name="pawn">The pawn to check</param>
        /// <returns>True if the pawn is healthy enough for dating, false otherwise</returns>
        private bool IsPawnHealthyForDating(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.capacities == null)
            {
                return false;
            }

            // Check if the pawn is capable of being awake (basic health check)
            if (!pawn.health.capacities.CanBeAwake)
            {
                return false;
            }

            return true;
        }

        private void HandlePartnerJoyActivity(Pawn initiator, Pawn partner, JobDef joyJobDef)
        {
            // Check if we have valid pawns
            if (initiator == null || partner == null || joyJobDef == null)
            {
                return;
            }
            
            // Additional validation to ensure both pawns are still valid for dating
            if (!IsPawnHealthyForDating(initiator) || !IsPawnHealthyForDating(partner))
            {
                return;
            }
            
            // Check if the partner is already doing the same joy job
            if (partner.CurJobDef == joyJobDef)
            {
                // Partner is already doing the joy job, let them continue
                // Check if they've gained enough joy and should go back to following
                if (partner.needs != null && partner.needs.joy != null && 
                    partner.needs.joy.CurLevelPercentage >= 0.99f)
                {
                    // Partner's joy need is satisfied, interrupt their joy job to go back to following
                    partner.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    
                    // Start the FollowAndWatch job for the partner
                    Job partnerJob = JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, initiator);
                    partner.jobs.StartJob(partnerJob, JobCondition.InterruptForced);
                }
                return;
            }
            
            // Check if the partner is doing a DateLovin job or other specialized dating jobs
            if (partner.CurJobDef == SI_JobDefOf.DateLovin ||
                partner.CurJobDef.defName == "PesterPrisonerPartner" ||
                partner.CurJobDef.defName == "AbusiveThreesomeParticipant" ||
                partner.CurJobDef.defName == "SocialRelaxDate")
            {
                return;
            }
            
            // Check if the partner is doing the FollowAndWatch job
            if (partner.CurJobDef == SI_JobDefOf.FollowAndWatchInitiator)
            {
                // Partner is following, check if they should join the joy activity
                // Check if the partner's joy need is high enough that they don't want to join
                if (partner.needs != null && partner.needs.joy != null && 
                    partner.needs.joy.CurLevelPercentage >= 0.95f)
                {
                    return;
                }
                
                // Try to have the partner join the joy activity
                TryHavePartnerJoinJoyActivity(initiator, partner, joyJobDef);
                return;
            }
            
            // If the partner is doing some other job, we'll assume they're not part of the date anymore
            // This could happen if they were interrupted by something else
            
            // Start the FollowAndWatch job for the partner
            Job followJob = JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, initiator);
            partner.jobs.StartJob(followJob, JobCondition.InterruptForced);
        }

        private void TryHavePartnerJoinJoyActivity(Pawn initiator, Pawn partner, JobDef joyJobDef)
        {
            // Check if we have valid pawns
            if (initiator == null || partner == null || joyJobDef == null)
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
            Job partnerJoyJob = initiatorJoyGiver.Worker.TryGiveJob(partner);
            if (partnerJoyJob == null)
            {
                return;
            }
            
            // Check if the target locations match or are nearby
            bool targetsMatch = false;
            if (partnerJoyJob.targetA.Thing != null && initiator.CurJob.targetA.Thing != null)
            {
                targetsMatch = partnerJoyJob.targetA.Thing == initiator.CurJob.targetA.Thing;
            }
            else if (partnerJoyJob.targetA.Cell.IsValid && initiator.CurJob.targetA.Cell.IsValid)
            {
                targetsMatch = partnerJoyJob.targetA.Cell.DistanceTo(initiator.CurJob.targetA.Cell) <= 7f;
            }
            else
            {
                // For jobs without fixed locations (like reading a book), we'll assume they can join
                // if they're reasonably close to each other
                if (partner.Position.IsValid && initiator.Position.IsValid)
                {
                    targetsMatch = partner.Position.DistanceTo(initiator.Position) <= 15f;
                }
            }
            
            if (targetsMatch)
            {
                // Enqueue the joy job and then interrupt the current job for a smooth transition
                partner.jobs.jobQueue.EnqueueFirst(partnerJoyJob);
                partner.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }
    }
}
