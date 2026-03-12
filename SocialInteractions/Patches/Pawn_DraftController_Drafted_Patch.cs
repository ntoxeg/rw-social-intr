
using HarmonyLib;
using RimWorld;
using Verse;
using SocialInteractions.Dating;
using SocialInteractions;

namespace SocialInteractions.Patches
{
    [HarmonyPatch(typeof(Pawn_DraftController))]
    public static class Pawn_DraftController_Drafted_Patch
    {
        [HarmonyPatch("set_Drafted")]
        [HarmonyPostfix]
        public static void SetDrafted_Postfix(Pawn_DraftController __instance, bool value)
        {
            if (value) // if drafted
            {
                Pawn pawn = __instance.pawn;
                Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("OnDate"));
                if (hediff != null)
                {
                    pawn.health.RemoveHediff(hediff);
                }

                // Also end the date in the manager to be safe
                if (DatingManager.Current.IsOnDate(pawn))
                {
                    Date date = DatingManager.Current.GetDateWith(pawn);
                    if (date != null) DatingManager.Current.EndDate(date);
                }
            }
        }
    }
}
