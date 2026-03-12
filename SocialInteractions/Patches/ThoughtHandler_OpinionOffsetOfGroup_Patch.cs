using HarmonyLib;
using RimWorld;
using Verse;
using System;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(ThoughtHandler), "OpinionOffsetOfGroup")]
    public static class ThoughtHandler_OpinionOffsetOfGroup_Patch
    {
        public static bool Prefix(ISocialThought group, ref int __result)
        {
            if (group == null)
            {
                __result = 0; // Return 0 opinion offset if the group is null
                return false; // Skip the original method
            }
            return true; // Continue with the original method
        }
    }
}