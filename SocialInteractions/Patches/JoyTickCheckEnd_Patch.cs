using HarmonyLib;
using RimWorld;
using Verse;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(JoyUtility), "JoyTickCheckEnd")]
    public static class JoyTickCheckEnd_Patch
    {
        static void Prefix(Pawn pawn, ref JoyTickFullJoyAction fullJoyAction)
        {
            // If the job was going to end due to full joy, check if the pawn is on a date or doing a specialized activity.
            if (fullJoyAction == JoyTickFullJoyAction.EndJob)
            {
                // Check if the pawn itself is doing a specialized job
                if (pawn.CurJobDef != null && 
                    (pawn.CurJobDef.defName == "PesterPrisoner" || 
                     pawn.CurJobDef.defName == "PesterPrisonerPartner" ||
                     pawn.CurJobDef.defName == "AbusiveThreesome" ||
                     pawn.CurJobDef.defName == "AbusiveThreesomeParticipant" ||
                     pawn.CurJobDef.defName == "SocialRelaxDate" ||
                     pawn.CurJobDef.defName == "DateLovin"))
                {
                    fullJoyAction = JoyTickFullJoyAction.None;
                    return;
                }

                Pawn partner = DatingManager.GetPartnerOfDateWith(pawn);
                // If they have a partner, and that partner is doing a dating-related job, don't end the activity.
                if (partner != null && partner.CurJob != null)
                {
                    string partnerJobName = partner.CurJob.def.defName;
                    if (partnerJobName == "FollowAndWatchInitiator" ||
                        partnerJobName == "PesterPrisoner" ||
                        partnerJobName == "PesterPrisonerPartner" ||
                        partnerJobName == "AbusiveThreesome" ||
                        partnerJobName == "AbusiveThreesomeParticipant" ||
                        partnerJobName == "SocialRelaxDate" ||
                        partnerJobName == "DateLovin")
                    {
                        fullJoyAction = JoyTickFullJoyAction.None;
                    }
                }
            }
        }
    }
}
