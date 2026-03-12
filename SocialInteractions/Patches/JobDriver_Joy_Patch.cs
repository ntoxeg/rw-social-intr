using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(Pawn_JobTracker), "EndCurrentJob")]
    public static class JobDriver_Joy_Patch
    {
        public static void Postfix(Pawn_JobTracker __instance, JobCondition condition)
        {
            // Access the pawn field through reflection with proper error handling
            Pawn pawn = null;
            try
            {
                var field = typeof(Pawn_JobTracker).GetField("pawn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    pawn = (Pawn)field.GetValue(__instance);
                }
            }
            catch (System.Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] Error getting pawn from Pawn_JobTracker: {0}", ex.Message));
            }
            
            // Only proceed if we have a valid pawn
            if (pawn == null) return;
            
            // Check if the pawn is on a date
            if (DatingManager.IsOnDate(pawn))
            {
                // Check if the job is the CaughtCheatingInteraction job, if so, skip the date logic
                if (__instance.curJob != null && __instance.curJob.def == SI_JobDefOf.CaughtCheatingInteraction)
                {
                    return;
                }
                
                // Check if the job was completed successfully
                if (__instance.curJob != null && condition == JobCondition.Succeeded)
                {
                    // Check if the job was a joy job
                    bool isJoyJob = false;
                    foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
                    {
                        if (joyGiver.jobDef == __instance.curJob.def)
                        {
                            isJoyJob = true;
                            break;
                        }
                    }
                    
                    // If it was a joy job, check if this pawn is the initiator of the date
                    if (isJoyJob)
                    {
                        Pawn initiator = DatingManager.GetInitiatorOfDateWith(pawn);
                        if (initiator == pawn)
                        {
                            // This pawn is the initiator, so advance the date stage
                            DatingManager.AdvanceDateStage(pawn);
                        }
                        else
                        {
                            // Check if the initiator is currently performing a specialized dating job
                            // If they are, we should NOT force the partner to follow, but let them join the specialized job
                            Pawn dateInitiator = DatingManager.GetInitiatorOfDateWith(pawn);
                            bool initiatorInSpecialJob = (dateInitiator != null && dateInitiator.CurJobDef != null && 
                                (dateInitiator.CurJobDef.defName == "PesterPrisoner" || 
                                 dateInitiator.CurJobDef.defName == "AbusiveThreesome"));

                            if (!initiatorInSpecialJob)
                            {
                                // This pawn is the partner, so restart their follow job
                                if (dateInitiator != null)
                                {
                                    // Create and start the FollowAndWatch job for the partner
                                    Job followJob = JobMaker.MakeJob(SI_JobDefOf.FollowAndWatchInitiator, dateInitiator);
                                    // Add a small delay before starting the job to prevent race conditions
                                    pawn.jobs.jobQueue.EnqueueFirst(followJob);
                                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                                }
                            }
                            else
                            {
                                SLog.Message(string.Format("[SocialInteractions] JobDriver_Joy_Patch: Skipping FollowAndWatch for {0} because initiator {1} is in specialized job {2}.", 
                                    pawn.LabelShort, dateInitiator.LabelShort, dateInitiator.CurJobDef.defName));
                            }
                        }
                    }
                }
                // If the job was not completed successfully, check if it moved to a non-joy job
                // But only if the pawn is the initiator of the date and the date is in the joy stage
                else if (__instance.curJob != null && condition != JobCondition.Succeeded)
                {
                    // Check if the current job is NOT a joy job (meaning they moved to a non-joy job)
                    bool isCurrentJobJoy = false;
                    foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
                    {
                        if (joyGiver.jobDef == __instance.curJob.def)
                        {
                            isCurrentJobJoy = true;
                            break;
                        }
                    }
                    
                    // Special cases: If the current job is a DateLovin job or Wait_MaintainPosture job, we should not treat it as a non-joy job
                    bool isCurrentJobDateLovin = (__instance.curJob.def == SI_JobDefOf.DateLovin);
                    bool isCurrentJobWaitMaintainPosture = (__instance.curJob.def == JobDefOf.Wait_MaintainPosture);
                    
                    // If the current job is not a joy job and not a DateLovin job and not a Wait_MaintainPosture job, advance the date
                    // But only if the pawn is the initiator of the date and the date is in the joy stage
                    if (!isCurrentJobJoy && !isCurrentJobDateLovin && !isCurrentJobWaitMaintainPosture)
                    {
                        Pawn initiator = DatingManager.GetInitiatorOfDateWith(pawn);
                        Date date = DatingManager.GetDateWith(pawn);
                        if (initiator == pawn && date != null && date.Stage == DateStage.Joy)
                        {
                            // This pawn is the initiator, so advance the date stage
                            DatingManager.AdvanceDateStage(pawn);
                        }
                        // For partners, we don't need to do anything special as they will be handled by the stuck date detection
                        // However, if the partner's DateLovin job was interrupted by a temporary need (like rest)
                        // we'll rely on the stuck date detection to handle restarting the job
                    }
                }
            }
        }
    }
}