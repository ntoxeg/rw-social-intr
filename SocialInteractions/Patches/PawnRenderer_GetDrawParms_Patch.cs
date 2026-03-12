using HarmonyLib;
using RimWorld;
using Verse;
using System;
using System.Reflection;
using UnityEngine;

namespace SocialInteractions
{
    [HarmonyPatch]
    public static class PawnRenderer_GetDrawParms_Patch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(PawnRenderer), "GetDrawParms");
        }

        public static void Prefix(PawnRenderer __instance, ref PawnRenderFlags flags)
        {
            try
            {
                Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
                if (pawn != null && pawn.health != null && pawn.health.hediffSet != null && SI_HediffDefOf.SI_Naked != null && pawn.health.hediffSet.HasHediff(SI_HediffDefOf.SI_Naked))
                {
                    flags &= ~PawnRenderFlags.Clothes;
                    flags &= ~PawnRenderFlags.Headgear;
                }
            }
            catch (Exception e)
            {
                SLog.Error("[SocialInteractions] Exception in PawnRenderer_GetDrawParms_Patch: " + e);
            }
        }
    }
}