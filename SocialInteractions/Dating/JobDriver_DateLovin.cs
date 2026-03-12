using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System;

namespace SocialInteractions
{
    public class JobDriver_DateLovin : JobDriver
    {
        /// <summary>
        /// Checks if a pawn is still valid for dating activities
        /// </summary>
        /// <param name="pawn">The pawn to check</param>
        /// <returns>True if the pawn is valid for dating, false otherwise</returns>
        private bool IsPawnValidForDating(Pawn pawn)
        {
            // Add comprehensive null checks
            if (pawn == null)
            {
                SLog.Warning("[SocialInteractions] IsPawnValidForDating: pawn is null.");
                return false;
            }
            
            if (pawn.Destroyed || pawn.Dead || pawn.Downed)
            {
                return false;
            }
            
            if (pawn.InMentalState)
            {
                return false;
            }
            
            // Add null checks for health properties
            if (pawn.health == null)
            {
                SLog.Warning(string.Format("[SocialInteractions] IsPawnValidForDating: pawn {0} has null health.", pawn.LabelShort));
                return false;
            }
            
            if (pawn.health.capacities == null)
            {
                SLog.Warning(string.Format("[SocialInteractions] IsPawnValidForDating: pawn {0} has null health.capacities.", pawn.LabelShort));
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
            
            
            
            return true;
        }
        public int ticksLeft; // Initialize to 0 by default

        private TargetIndex PartnerInd = TargetIndex.A;
        private TargetIndex BedPosInd = TargetIndex.B;

        private Pawn Partner 
        { 
            get 
            { 
                if (job == null) return null;
                return (Pawn)(Thing)job.GetTarget(PartnerInd); 
            } 
        }
        private IntVec3 BedPos 
        { 
            get 
            { 
                if (job == null) return IntVec3.Invalid;
                return job.GetTarget(BedPosInd).Cell; 
            } 
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            
            // Add comprehensive null checks
            if (pawn == null)
            {
                SLog.Warning("[SocialInteractions] JobDriver_DateLovin: pawn is null in Notify_Starting.");
                return;
            }
            
            // When starting a DateLovin job, we want to make sure the pawn doesn't get interrupted by non-critical jobs
            // We'll clear any queued jobs and set the job as player-forced to increase its priority
            if (pawn.jobs != null)
            {
                pawn.jobs.ClearQueuedJobs();
                // Set the current job as player-forced to increase its priority
                if (pawn.jobs.curJob != null)
                {
                    pawn.jobs.curJob.playerForced = true;
                }
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Add comprehensive null checks at the beginning
            if (pawn == null || Partner == null) 
            {
                SLog.Warning("[SocialInteractions] JobDriver_DateLovin: pawn or Partner is null in TryMakePreToilReservations.");
                return false;
            }

            // Check if both pawns are still on a date
            if (!DatingManager.IsOnDate(pawn) || !DatingManager.IsOnDate(Partner))
            {
                SLog.Warning(string.Format("[SocialInteractions] JobDriver_DateLovin: pawn {0} or Partner {1} is no longer on a date in TryMakePreToilReservations.",
                    pawn.LabelShort, Partner.LabelShort));
                return false;
            }

            // Use the helper method to check if both pawns are valid for dating
            if (!IsPawnValidForDating(pawn) || !IsPawnValidForDating(Partner))
            {
                SLog.Warning(string.Format("[SocialInteractions] JobDriver_DateLovin: pawn {0} or Partner {1} is not valid for dating in TryMakePreToilReservations.",
                    pawn.LabelShort, Partner.LabelShort));
                return false;
            }

            // Only the initiator makes reservations. The partner does nothing.
            Pawn initiator = DatingManager.GetInitiatorOfDateWith(pawn);
            if (pawn == initiator)
            {
                // Initiator reserves both the spot and the partner
                if (!pawn.Reserve(job.GetTarget(BedPosInd), job, 1, -1, null, errorOnFailed))
                {
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_DateLovin: Initiator {0} failed to reserve lovin spot.", pawn.LabelShort));
                    return false;
                }
                if (!pawn.Reserve(Partner, job, 1, -1, null, errorOnFailed))
                {
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_DateLovin: Initiator {0} failed to reserve partner {1}.", pawn.LabelShort, Partner.LabelShort));
                    return false;
                }
            }

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(PartnerInd);
            this.FailOn(() => Partner == null || !Partner.health.capacities.CanBeAwake);

            // Conditional Goto to handle both bed (Thing) and random spot (Cell) targets
            if (job.GetTarget(BedPosInd).HasThing)
            {
                // Target is a bed, go to its interaction cell to avoid lying down
                yield return Toils_Goto.GotoThing(BedPosInd, PathEndMode.InteractionCell);
            }
            else
            {
                // Target is just a cell, go onto it
                yield return Toils_Goto.GotoCell(BedPosInd, PathEndMode.OnCell);
            }

            // Add a toil to wait for the partner to get into position
            Toil waitForPartnerToil = ToilMaker.MakeToil("WaitForPartner");
            waitForPartnerToil.tickAction = delegate
            {
                // Add comprehensive null checks to prevent NullReferenceException
                if (pawn == null || Partner == null)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_DateLovin: pawn or Partner is null in waitForPartnerToil tickAction.");
                    // End the job instead of calling ReadyForNextToil to avoid state conflicts
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                // Check if both pawns are within a generous distance of each other
                try
                {
                    if (pawn.Position.DistanceTo(Partner.Position) <= 3.5f)
                    {
                        // If they're close enough, we can proceed to the next toil
                        this.ReadyForNextToil();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] Exception in waitForPartnerToil tickAction distance check: {0}", ex.Message));
                    // End the job on exception
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                // If not close enough, continue waiting
            };
            waitForPartnerToil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return waitForPartnerToil;

            // Store references to both pawns to ensure we can access them later
            Pawn initiator = pawn;
            Pawn partner = Partner;

            Toil lovinToil = ToilMaker.MakeToil("LovinToil");
            lovinToil.initAction = delegate
            {
                // Add comprehensive null checks
                if (pawn == null)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_DateLovin: pawn is null in lovinToil.initAction, ending job.");
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                // Check if the pawn is still on a date in the Lovin stage
                if (!DatingManager.IsOnDate(pawn))
                {
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                Date date = DatingManager.GetDateWith(pawn);
                if (date == null || date.Stage != DateStage.Lovin)
                {
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                ticksLeft = SocialInteractions.Settings.dateLovinTicks;
                // Don't add the SI_Naked hediff here - wait until the pawns actually start the lovin activity
            };
            lovinToil.tickAction = delegate
            {
                // Add comprehensive null checks to prevent NullReferenceException
                if (initiator == null || initiator.jobs == null)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_DateLovin: initiator or initiator.jobs is null in lovinToil.tickAction, ending job.");
                    ReadyForNextToil();
                    return;
                }

                // Check if the job is still running
                if (initiator.jobs.curDriver != this)
                {
                    // This can happen if the job was interrupted or replaced
                    // Let's check if the pawn is still on a date in the Lovin stage
                    Date date = DatingManager.GetDateWith(initiator);
                    if (date != null && date.Stage == DateStage.Lovin)
                    {
                        // The pawn should still be in the DateLovin job
                        // Let's log this situation but continue processing
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_DateLovin: Initiator {0} is on a date in Lovin stage but curDriver is not this job. Continuing processing.", 
                            initiator.LabelShort != null ? initiator.LabelShort : "NULL"));
                    }
                    else
                    {
                        // The pawn is no longer on a date in the Lovin stage
                        // End the toil
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_DateLovin: Initiator {0} is no longer on a date in Lovin stage, ending job.", 
                            initiator.LabelShort != null ? initiator.LabelShort : "NULL"));
                        ReadyForNextToil();
                        return;
                    }
                }
                
                // Re-validate partner reference
                Pawn currentPartner = Partner;
                if (currentPartner == null)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_DateLovin: Partner became null during tick, ending job.");
                    ReadyForNextToil();
                    return;
                }

                // Add the SI_Naked hediff when the lovin activity actually starts (first tick)
                // Only add it once
                if (ticksLeft == SocialInteractions.Settings.dateLovinTicks)
                {
                    // Add null checks before adding hediff
                    if (initiator != null && initiator.health != null)
                    {
                        initiator.health.AddHediff(HediffDef.Named("SI_Naked"));
                    }

                    if (currentPartner != null && currentPartner.health != null)
                    {
                        currentPartner.health.AddHediff(HediffDef.Named("SI_Naked"));
                    }
                }

                ticksLeft--;
                if (ticksLeft <= 0)
                {
                    // Handle thought-giving and date advancement here instead of in finishAction
                    try
                    {
                        // Get the partner from the date
                        Pawn partnerFromDate = null;
                        Date date = DatingManager.GetDateWith(initiator);

                        if (date != null)
                        {
                            if (date.Initiator == initiator)
                            {
                                partnerFromDate = date.Partner;
                            }
                            else if (date.Partner == initiator)
                            {
                                partnerFromDate = date.Initiator;
                            }

                            // "Got some lovin" thoughts, pregnancy, and post-lovin LLM call are now handled in DatingManager.HandleDateStage when stage is Finished

                            // Post-lovin LLM call is now handled in DatingManager.HandleDateStage when stage is Finished
                            
                            // Advance the date stage
                            if (date.Stage == DateStage.Lovin)
                            {
                                DatingManager.AdvanceDateStage(initiator);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] Exception in DateLovin tickAction when handling thoughts/advancement: {0}", ex.Message));
                    }

                    ReadyForNextToil();
                }
                else if (initiator.IsHashIntervalTick(100))
                {
                    try
                    {
                        // Add null checks before creating fleck
                        if (initiator != null && initiator.Position != null && initiator.Map != null)
                        {
                            FleckMaker.ThrowMetaIcon(initiator.Position, initiator.Map, FleckDefOf.Heart);
                        }
                        
                        if (initiator.needs != null && initiator.needs.joy != null)
                        {
                            initiator.needs.joy.GainJoy(0.05f, JoyKindDefOf.Social);
                        }
                    }
                    catch (Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] Exception in DateLovin tickAction: {0}", ex.Message));
                    }
                }
            };
            lovinToil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return lovinToil;

            Toil cleanupToil = ToilMaker.MakeToil("CleanupToil");
            cleanupToil.initAction = delegate
            {
                try
                {
                    // Add comprehensive null checks to prevent NullReferenceException
                    if (pawn == null)
                    {
                        SLog.Warning("[SocialInteractions] JobDriver_DateLovin: pawn is null in cleanupToil.initAction.");
                        return;
                    }
                    
                    // Re-validate partner reference
                    Pawn currentPartner = Partner;
                    if (currentPartner == null)
                    {
                        SLog.Warning("[SocialInteractions] JobDriver_DateLovin: Partner is null in cleanupToil.initAction.");
                        return;
                    }
                    
                    // Additional checks to ensure pawns are still valid
                    if (pawn.Destroyed || currentPartner.Destroyed)
                    {
                        SLog.Warning("[SocialInteractions] JobDriver_DateLovin: One or both pawns are destroyed in cleanupToil.initAction.");
                        return;
                    }
                    
                    // Post-lovin LLM call has been moved to the lovinToil.tickAction where the date is still active
                    
                    // Try to remove SI_Naked hediff from initiator (pawn)
                    if (pawn.health != null && pawn.health.hediffSet != null)
                    {
                        try
                        {
                            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("SI_Naked"));
                            if (hediff != null)
                            {
                                pawn.health.RemoveHediff(hediff);
                            }
                        }
                        catch (Exception ex)
                        {
                            SLog.Warning(string.Format("[SocialInteractions] Exception removing SI_Naked hediff from initiator {0}: {1}", 
                                pawn.LabelShort != null ? pawn.LabelShort : "NULL", ex.Message));
                        }
                    }

                    // Try to remove SI_Naked hediff from partner
                    if (currentPartner.health != null && currentPartner.health.hediffSet != null)
                    {
                        try
                        {
                            Hediff hediff = currentPartner.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("SI_Naked"));
                            if (hediff != null)
                            {
                                currentPartner.health.RemoveHediff(hediff);
                            }
                        }
                        catch (Exception ex)
                        {
                            SLog.Warning(string.Format("[SocialInteractions] Exception removing SI_Naked hediff from partner {0}: {1}", 
                                currentPartner.LabelShort != null ? currentPartner.LabelShort : "NULL", ex.Message));
                        }
                    }
                }
                catch (Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] Exception in cleanupToil.initAction: {0}", ex.Message));
                }
            };
            yield return cleanupToil;
        }

        public override Vector3 ForcedBodyOffset
        {
            get
            {
                // Add comprehensive safety checks to prevent NullReferenceException
                if (pawn == null)
                {
                    return Vector3.zero;
                }

                // Check if ticksLeft is uninitialized (0) or finished
                if (ticksLeft <= 0)
                {
                    // Return zero offset for uninitialized or finished state
                    return Vector3.zero;
                }

                int totalTicks = SocialInteractions.Settings.dateLovinTicks;
                
                // Make sure we don't divide by zero
                if (totalTicks <= 0)
                {
                    return Vector3.zero;
                }

                // Calculate progress (0.0 to 1.0 as time passes)
                float progress = 1.0f - ((float)ticksLeft / totalTicks);

                // Calculate animation speed based on progress
                float animationSpeed = 1.0f;
                if (progress <= 0.90f)
                {
                    // Linear interpolation from 1.0 to 1.75
                    animationSpeed = 1.0f + (progress / 0.90f) * 0.75f;
                }
                else
                {
                    // Drop to 20% speed for the remaining time
                    animationSpeed = 0.3f;
                }
                
                // Calculate the base time parameter
                float baseTime = progress * 8.0f * (totalTicks / 60.0f);
                
                // Apply the animation speed to effectively change the frequency
                // To double the speed, we double the frequency (multiply time by speed)
                float adjustedTime = baseTime * animationSpeed;
                
                float num = Mathf.Sin(adjustedTime);
                Pawn initiator = DatingManager.GetInitiatorOfDateWith(pawn);

                // If we can't get the initiator, just return zero offset
                if (initiator == null)
                {
                    return Vector3.zero;
                }

                // Re-validate partner reference
                Pawn currentPartner = Partner;
                if (currentPartner == null)
                {
                    return Vector3.zero;
                }

                // Male pawns bounce on X axis, female pawns bounce on Z axis
                try
                {
                    if (pawn == initiator ^ initiator.gender == Gender.Female)
                    {
                        // Initiator bounces on X
                        float num2 = Mathf.Sign(num);
                        return new Vector3(EaseInOutQuad(Mathf.Abs(num) * 0.6f) * 0.09f * num2, 0f, 0f);
                    }
                    else
                    {
                        // Partner bounces on Z
                        float z = Mathf.Max(Mathf.Pow((num + 1f) * 0.5f, 2f) * 0.2f - 0.06f, 0f);
                        return new Vector3(0f, 0f, z);
                    }
                }
                catch (Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] Exception in ForcedBodyOffset calculation: {0}", ex.Message));
                    return Vector3.zero;
                }
            }
        }

        public override bool CanBeginNowWhileLyingDown()
        {
            // Allow the job to begin while lying down
            return true;
        }

        private float EaseInOutQuad(float v)
        {
            if (!((double)v < 0.5))
            {
                return 1f - Mathf.Pow(-2f * v + 2f, 4f) / 2f;
            }
            return 8f * v * v * v * v;
        }
    }
}
