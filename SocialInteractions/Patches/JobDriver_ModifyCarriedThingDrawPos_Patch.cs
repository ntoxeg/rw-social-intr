using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(JobDriver), "ModifyCarriedThingDrawPos")]
    public static class JobDriver_ModifyCarriedThingDrawPos_Patch
    {
        public static void Postfix(JobDriver __instance, ref Vector3 drawPos, ref bool flip)
        {
            // Check if this is our custom job driver
            JobDriver_ChildPlayWithItem playDriver = __instance as JobDriver_ChildPlayWithItem;
            if (playDriver != null)
            {
                if (playDriver.isPlaying)
                {
                    // Bounce up and down (half sine wave)
                    // Use TicksGame for game-tick based animation as requested
                    // Use Max(0, sin) to bounce up only
                    int ticks = Find.TickManager.TicksGame;
                    float bounce = Mathf.Max(0f, Mathf.Sin(ticks / 10f)) * 0.1f;
                    drawPos.z += bounce;

                    // Spin around while 'in air'
                    if (bounce > 0.001f)
                    {
                        if (playDriver.pawn.carryTracker.CarriedThing != null)
                        {
                            playDriver.pawn.carryTracker.CarriedThing.Rotation = new Rot4((ticks / 10) % 4);
                        }
                    }
                    else
                    {
                         // Reset rotation when on ground
                         if (playDriver.pawn.carryTracker.CarriedThing != null)
                         {
                             playDriver.pawn.carryTracker.CarriedThing.Rotation = Rot4.South;
                         }
                    }
                }
            }
        }
    }
}
