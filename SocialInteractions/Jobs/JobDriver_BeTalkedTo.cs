using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using System;

namespace SocialInteractions
{
    public class JobDriver_BeTalkedTo : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        public void EndJob(JobCondition condition)
        {
            SLog.Message(string.Format("[SocialInteractions] JobDriver_BeTalkedTo.EndJob called with condition: {0}", condition));
            pawn.jobs.EndCurrentJob(condition);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            SLog.Message("[SocialInteractions] JobDriver_BeTalkedTo.MakeNewToils called.");
            
            this.FailOnDespawnedOrNull(TargetIndex.A);
            Toil toil = new Toil();
            toil.initAction = () => {
                SLog.Message("[SocialInteractions] JobDriver_BeTalkedTo: Stopping and facing initiator.");
                pawn.pather.StopDead();
                pawn.rotationTracker.FaceCell(TargetA.Cell);
            };
            toil.tickAction = () => {
                // Check if we should still be in this job
                Pawn initiator = (Pawn)TargetA.Thing;
                if (initiator == null || initiator.jobs == null || initiator.jobs.curDriver == null)
                {
                    // If the initiator is gone or no longer has a job, end this job
                    SLog.Message("[SocialInteractions] JobDriver_BeTalkedTo: Initiator no longer valid, ending job.");
                    pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                    return;
                }
                
                // If the initiator is no longer doing a HaveDeepTalk or CaughtCheating job, 
                // but the conversation is still active or has pending speech bubbles, wait a bit longer
                if (!(initiator.jobs.curDriver is JobDriver_HaveDeepTalk) && !(initiator.jobs.curDriver is JobDriver_CaughtCheating))
                {
                    // Check if there's still an active conversation or pending speech bubbles
                    // This handles the case where the initiator's job has changed but speech bubbles are still displaying
                    bool stillActive = false;
                    try 
                    {
                        // Try to get the conversation ID from the JobDriver_CaughtCheating if it was recently active
                        // Or check if there are any active conversations or pending speech bubbles for this pawn
                        stillActive = SpeechBubbleManager.HasPendingSpeechBubblesForPawn(pawn) || 
                                     SpeechBubbleManager.HasActiveConversations();
                    }
                    catch (Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] JobDriver_BeTalkedTo: Exception while checking conversation status: {0}", ex.Message));
                    }
                    
                    if (stillActive)
                    {
                        // Continue waiting for speech bubbles to finish
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_BeTalkedTo: Initiator {0} no longer doing HaveDeepTalk or CaughtCheating job (currently doing {1}), but conversation still active, continuing to wait.", 
                            initiator.LabelShort, initiator.jobs.curDriver.GetType().Name));
                    }
                    else
                    {
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_BeTalkedTo: Initiator {0} no longer doing HaveDeepTalk or CaughtCheating job (currently doing {1}), and conversation finished, ending job.", 
                            initiator.LabelShort, initiator.jobs.curDriver.GetType().Name));
                        pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                        return;
                    }
                }
                
                pawn.rotationTracker.FaceCell(TargetA.Cell);
                if (pawn.needs != null && pawn.needs.joy != null)
                {
                    JoyKindDef socialJoy = DefDatabase<JoyKindDef>.GetNamed("Social", false);
                    if (socialJoy != null)
                    {
                        pawn.needs.joy.GainJoy(0.00015f, socialJoy);
                    }
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return toil;
        }
    }
}