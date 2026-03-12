using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
    public static class Debug_JobTracker_StartJob_Patch
    {
        public static void Prefix(Pawn_JobTracker __instance, Job newJob)
        {
            Pawn pawn = (Pawn)typeof(Pawn_JobTracker).GetField("pawn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(__instance);
            if (pawn != null && DatingManager.IsOnDate(pawn))
            {
                // If the pawn is on a date and is being given a job other than DateLovin or a few other valid ones, log it with a stack trace.
                if (newJob.def != SI_JobDefOf.DateLovin && 
                    newJob.def != JobDefOf.Wait_MaintainPosture &&
                    newJob.def != SI_JobDefOf.PesterPrisoner &&
                    newJob.def != SI_JobDefOf.PesterPrisonerPartner &&
                    newJob.def != SI_JobDefOf.AbusiveThreesome &&
                    newJob.def != SI_JobDefOf.AbusiveThreesomeParticipant)
                {
                    /*
                    SLog.Warning(string.Format("[SocialInteractions] DEBUG: Pawn {0} on date is starting new job '{1}'. Stack Trace:\n{2}",
                        pawn.LabelShort,
                        newJob.def.defName,
                        System.Environment.StackTrace));
                    */
                }
            }
        }
    }
}