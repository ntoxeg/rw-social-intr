using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JobDriver_BackstabbingGatherInfo : JobDriver
    {
        private const int BaseInfoGatheringDuration = 400; // 6.6 seconds in ticks

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnMentalState(TargetIndex.A);
            
            // Go to the target
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            
            Toil infoGatheringToil = new Toil();
            infoGatheringToil.initAction = delegate
            {
                Pawn targetPawn = (Pawn)job.GetTarget(TargetIndex.A).Thing;
                
                if (targetPawn == null)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_BackstabbingGatherInfo: Target pawn is null, ending job");
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                if (targetPawn.Dead || targetPawn.Downed)
                {
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_BackstabbingGatherInfo: Target {0} is dead or downed, ending job", targetPawn.LabelShort));
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                // Additional checks for valid interaction state
                if (!CanInteractWithTarget(targetPawn))
                {
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_BackstabbingGatherInfo: Target {0} is not in valid state for interaction, ending job", targetPawn.LabelShort));
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                // Perform the information gathering interaction
                TryStartInfoGatheringInteraction(targetPawn);
            };
            
            // Add a tick action to follow the target if they move away and check valid interaction state
            infoGatheringToil.tickAction = delegate
            {
                Pawn targetPawn = (Pawn)job.GetTarget(TargetIndex.A).Thing;
                if (targetPawn != null && !targetPawn.Dead && targetPawn.Spawned)
                {
                    // Check if target is in a valid state for interaction
                    if (!CanInteractWithTarget(targetPawn))
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    
                    // Check if we should update the path to follow the target
                    if (pawn.IsHashIntervalTick(60)) // Check every second
                    {
                        // If the target has moved significantly, update our path to follow them
                        float distance = (pawn.Position - targetPawn.Position).LengthHorizontal;
                        if (distance > 2f) // If more than 2 cells away
                        {
                            // Update path to follow target
                            pawn.pather.StartPath(targetPawn, PathEndMode.Touch);
                        }
                    }
                }
            };
            
            infoGatheringToil.defaultCompleteMode = ToilCompleteMode.Delay;
            infoGatheringToil.defaultDuration = 1800; // 30 seconds of interaction time
            infoGatheringToil.socialMode = RandomSocialMode.Normal;
            yield return infoGatheringToil;
        }
        
        private void TryStartInfoGatheringInteraction(Pawn targetPawn)
        {
            if (pawn == null || targetPawn == null)
            {
                SLog.Warning("[SocialInteractions] JobDriver_BackstabbingGatherInfo: Null pawn detected, skipping interaction");
                return;
            }
            
            // For info gathering, we're more lenient about the CanTradeNow check since this is a strategic action
            // that should be able to interrupt temporarily busy pawns
            if (!pawn.CanTradeNow)
            {
            }
            
            if (!targetPawn.CanTradeNow)
            {
            }
            
            // Check if we can interact with the target
            if (!pawn.CanReach(targetPawn, PathEndMode.Touch, Danger.Deadly))
            {
                SLog.Warning("[SocialInteractions] JobDriver_BackstabbingGatherInfo: Cannot reach target for info gathering");
                return;
            }
            
            // Perform the information gathering interaction
            string letterText, letterLabel;
            LetterDef letterDef;
            LookTargets lookTargets;
            
            InteractionWorker_Backstabbing backstabbingWorker = new InteractionWorker_Backstabbing();
            
            // This will perform the information gathering using the worker
            backstabbingWorker.Interacted(pawn, targetPawn, null, out letterText, out letterLabel, out letterDef, out lookTargets);
            
            // After info gathering is complete, we may want to schedule the actual backstabbing
            // For now, we'll just complete this job and the decision about backstabbing could be made elsewhere
        }
        
        /// <summary>
        /// Checks if the target pawn is in a valid state for interaction
        /// Hard blocks prevent backstabbing, soft blocks (busy jobs) do not
        /// Note: This method is called frequently during tickAction, so logging is minimal
        /// </summary>
        private bool CanInteractWithTarget(Pawn targetPawn)
        {
            // SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingGatherInfo: Checking if {0} can interact with {1}", 
            //     pawn.LabelShort, targetPawn != null ? targetPawn.LabelShort : "null"));
                
            if (targetPawn == null)
            {
                // SLog.Message("[SocialInteractions] JobDriver_BackstabbingGatherInfo: Target is null, cannot interact");
                return false;
            }
                
            // Hard blocks - these completely prevent interaction
            if (targetPawn.Dead || targetPawn.Downed || targetPawn.Destroyed)
            {
                // SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingGatherInfo: Target {0} is dead/downed/destroyed, cannot interact", targetPawn.LabelShort));
                return false;
            }
            
            // Hard blocks - mental states completely prevent interaction
            if (targetPawn.InMentalState)
            {
                // SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingGatherInfo: Target {0} is in mental state, cannot interact", targetPawn.LabelShort));
                return false;
            }
            
            // Hard blocks - sleeping completely prevents interaction (natural interruption)
            if (targetPawn.CurJobDef == JobDefOf.LayDown || targetPawn.InBed())
            {
                // SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingGatherInfo: Target {0} is sleeping/in bed, cannot interact", targetPawn.LabelShort));
                return false;
            }
            
            // SLog.Message(string.Format("[SocialInteractions] JobDriver_BackstabbingGatherInfo: Target {0} is in acceptable state for backstabbing interaction", targetPawn.LabelShort));
            return true;
        }
    }
}