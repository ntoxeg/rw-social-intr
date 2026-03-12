using RimWorld;
using Verse;
using Verse.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SocialInteractions
{
    public class JobDriver_FollowAndWatch : JobDriver
    {
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
                    // If the pawn is in any other job, they should not be doing it
                    if (pawn.jobs != null && pawn.jobs.curJob != null && 
                        pawn.jobs.curJob.def != SI_JobDefOf.DateLovin &&
                        pawn.jobs.curJob.def.defName != "PesterPrisoner" &&
                        pawn.jobs.curJob.def.defName != "PesterPrisonerPartner" &&
                        pawn.jobs.curJob.def.defName != "AbusiveThreesome" &&
                        pawn.jobs.curJob.def.defName != "AbusiveThreesomeParticipant")
                    {
                        SLog.Message(string.Format("[SocialInteractions] IsPawnValidForDating: Pawn {0} is on a date in Lovin stage but not in a valid date job.", pawn.LabelShort));
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
                SLog.Warning("[SocialInteractions] JobDriver_FollowAndWatch: pawn is null in TryMakePreToilReservations.");
                return false;
            }
            
            // Use the helper method to check if the pawn is valid for dating
            if (!IsPawnValidForDating(this.pawn))
            {
                SLog.Warning("[SocialInteractions] JobDriver_FollowAndWatch: pawn is not valid for dating in TryMakePreToilReservations.");
                return false;
            }
            
            return true;
        }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A); // Initiator

            Toil follow = new Toil();
            follow.initAction = delegate
            {
                // Add comprehensive null checks
                if (this.pawn == null || this.job == null || this.job.targetA == null) 
                {
                    SLog.Warning("[SocialInteractions] JobDriver_FollowAndWatch: follow.initAction - pawn, job, or targetA is null. Ending job.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                if (this.pawn.pather != null) 
                {
                    Pawn initiator = this.job.targetA.Thing as Pawn;
                    if (initiator != null)
                    {
                        // Log start path attempt
                        string pawnName = (this.pawn != null && this.pawn.Label != null) ? this.pawn.LabelShort : "NULL";
                        string initiatorName = (initiator != null && initiator.Label != null) ? initiator.LabelShort : "NULL";
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: {0} starting path to initiator {1} at {2}.", pawnName, initiatorName, initiator.Position));
                    }
                    this.pawn.pather.StartPath(this.job.targetA, PathEndMode.Touch);
                }
                else
                {
                    string pawnName = (this.pawn != null && this.pawn.Label != null) ? this.pawn.LabelShort : "NULL";
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: {0}'s pather is null. Ending job.", pawnName));
                    this.EndJobWith(JobCondition.Incompletable);
                }
            };
            follow.AddFinishAction(() =>
            {
            });
            follow.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            yield return follow;

            Toil watch = new Toil();
            watch.tickAction = () => {
                // Add comprehensive null checks at the beginning
                if (this.pawn == null)
                {
                    this.ReadyForNextToil();
                    return;
                }
                
                if (this.job == null)
                {
                    this.ReadyForNextToil();
                    return;
                }
                
                Pawn initiator = this.job.targetA.Thing as Pawn;
                if (initiator == null)
                {
                    this.ReadyForNextToil();
                    return;
                }

                // The primary condition for this job to continue is that the date is still active.
                // This is indicated by the initiator having the "OnDate" hediff.
                if (!DatingManager.IsOnDate(initiator))
                {
                    this.ReadyForNextToil(); // End the FollowAndWatch job
                    return;
                }
                
                // Also check if the follower is still on the date
                if (!DatingManager.IsOnDate(this.pawn))
                {
                    this.ReadyForNextToil(); // End the FollowAndWatch job
                    return;
                }
                
                // Check if the initiator has moved on to a non-joy job, and if so, advance the date
                // Only check every 30 ticks (0.5 seconds) to reduce performance impact
                if (initiator.jobs != null && initiator.jobs.curJob != null && this.pawn.IsHashIntervalTick(30))
                {
                    // Check if the initiator's current job is NOT a joy job
                    bool isInitiatorDoingJoyJob = false;
                    foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
                    {
                        if (joyGiver.jobDef == initiator.jobs.curJob.def)
                        {
                            isInitiatorDoingJoyJob = true;
                            break;
                        }
                    }
                    
                    // Special case: If the initiator is doing a DateLovin job, SocialRelaxDate, PesterPrisoner, or AbusiveThreesome, we should account for it
                    bool isInitiatorDoingDatingJob = (initiator.jobs.curJob != null && 
                        (initiator.jobs.curJob.def.defName == "DateLovin" || 
                         initiator.jobs.curJob.def.defName == "SocialRelaxDate" ||
                         initiator.jobs.curJob.def.defName == "PesterPrisoner" ||
                         initiator.jobs.curJob.def.defName == "AbusiveThreesome"));
                    
                    // If the initiator is not doing a joy job and not doing a dating job, advance the date
                    if (!isInitiatorDoingJoyJob && !isInitiatorDoingDatingJob)
                    {
                        // Advance the date stage. The DatingManager will handle ending this job.
                        DatingManager.AdvanceDateStage(this.pawn);
                        return; // Stop processing this tick as the job may have been changed
                    }

                    // Specialized Job Joining: If the initiator is pestering a prisoner, join them!
                    if (initiator.CurJobDef.defName == "PesterPrisoner")
                    {
                        // Only start if not already pestering the same target
                        if (this.pawn.CurJobDef.defName != "PesterPrisonerPartner" || 
                            this.pawn.CurJob.targetA != initiator.CurJob.targetA)
                        {
                            SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: {0} detected initiator {1} is pestering. Switching to PesterPrisonerPartner.", 
                                this.pawn.LabelShort, initiator.LabelShort));
                            
                            Job partnerJob = JobMaker.MakeJob(SI_JobDefOf.PesterPrisonerPartner, initiator.CurJob.targetA, initiator);
                            this.pawn.jobs.StartJob(partnerJob, JobCondition.InterruptForced);
                            return;
                        }
                    }
                    
                    // Specialized Job Joining: If the initiator is in an Abusive Threesome, join them!
                    if (initiator.CurJobDef.defName == "AbusiveThreesome" && SI_JobDefOf.AbusiveThreesomeParticipant != null)
                    {
                        // Only start if not already in the threesome
                        if (this.pawn.CurJobDef.defName != "AbusiveThreesomeParticipant" || 
                            this.pawn.CurJob.targetA != initiator || 
                            this.pawn.CurJob.targetB != initiator.CurJob.targetA)
                        {
                             SLog.Message(string.Format("[SocialInteractions] JobDriver_FollowAndWatch: {0} detected initiator {1} is in Threesome. Switching to AbusiveThreesomeParticipant.", 
                                this.pawn.LabelShort, initiator.LabelShort));
                            
                            // Mapping: targetA = Abuser (initiator), targetB = Victim (initiator's targetA)
                            Job partnerJob = JobMaker.MakeJob(SI_JobDefOf.AbusiveThreesomeParticipant, initiator, initiator.CurJob.targetA);
                            this.pawn.jobs.StartJob(partnerJob, JobCondition.InterruptForced);
                            return;
                        }
                    }
                }

                // Pathing Logic - Continuously update path to follow the initiator
                try
                {
                    if (this.pawn.IsHashIntervalTick(SocialInteractions.Settings.jobCheckIntervalTicks))
                    {
                        // Check if pawn or pather is null before accessing
                        if (this.pawn.pather == null)
                        {
                            this.ReadyForNextToil();
                            return;
                        }

                        // Check if initiator is spawned and on the same map
                        if (!initiator.Spawned || initiator.Map != this.pawn.Map)
                        {
                            this.ReadyForNextToil();
                            return;
                        }

                        // Additional check to ensure both pawns are still valid for dating
                        if (!IsPawnValidForDating(this.pawn) || !IsPawnValidForDating(initiator))
                        {
                            this.ReadyForNextToil();
                            return;
                        }

                        // Check if destination is already set correctly
                        if (this.pawn.pather.Moving && this.pawn.pather.Destination == initiator.Position)
                        {
                            // Already moving to the correct destination, do nothing.
                        }
                        else
                        {
                            // Attempt to start path
                            this.pawn.pather.StartPath(initiator, PathEndMode.Touch);
                            
                            // Basic check for immediate pathing failure (heuristic)
                            if (!this.pawn.pather.Moving && this.pawn.Position.DistanceTo(initiator.Position) > 5f) 
                            {
                                this.ReadyForNextToil(); 
                                return;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    this.ReadyForNextToil(); // End job on exception
                    return;
                }

                // Face the initiator
                this.pawn.rotationTracker.FaceCell(initiator.Position);

                // Gain Joy at the same rate as the initiator would from doing a joy activity
                // This is a social joy gain since the pawn is watching their date do something enjoyable
                if (this.pawn.needs != null && this.pawn.needs.joy != null)
                {
                    this.pawn.needs.joy.GainJoy(0.000144f, JoyKindDefOf.Social);
                }
            };
            watch.AddFinishAction(() => {
                // OnDate hediffs are now handled by DatingManager
            });
            watch.defaultCompleteMode = ToilCompleteMode.Never;
            yield return watch;
        }
    }
}