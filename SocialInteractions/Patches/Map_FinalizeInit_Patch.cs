using HarmonyLib;
using RimWorld;
using Verse;
using System.Linq;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(Map), "FinalizeInit")]
    public static class Map_FinalizeInit_Patch
    {
        public static void Postfix(Map __instance)
        {
            if (__instance.components.All(c => c.GetType() != typeof(DateTracker_MapComponent)))
            {
                __instance.components.Add(new DateTracker_MapComponent(__instance));
            }

            if (__instance.components.All(c => c.GetType() != typeof(ChildrenMisbehaviorTracker_MapComponent)))
            {
                __instance.components.Add(new ChildrenMisbehaviorTracker_MapComponent(__instance));
            }
        }
    }
}
