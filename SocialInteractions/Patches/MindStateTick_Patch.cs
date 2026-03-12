using HarmonyLib;
using RimWorld;
using Verse;

namespace SocialInteractions
{
    // This patch is being removed as we're moving the cheating detection to Pawn_Tick_Patch
    // to avoid issues with missing type references
    public static class MindStateTick_Patch
    {
        // Intentionally left empty - cheating detection moved to Pawn_Tick_Patch
    }
}