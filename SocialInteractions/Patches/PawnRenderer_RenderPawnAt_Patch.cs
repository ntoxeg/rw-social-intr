using HarmonyLib;
using UnityEngine;
using Verse;

namespace SocialInteractions
{
    // [HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")] // Attribute removed
    public static class PawnRenderer_RenderPawnAt_Patch
    {
        public static void Prefix(PawnRenderer __instance, ref Vector3 drawLoc, Pawn ___pawn)
        {
            SLog.Message(string.Format("RenderPawnAt patch running for {0}", ___pawn.Name.ToStringShort));
            // Commenting out LovinBouncer references as the class is not defined
            /*
            if (LovinBouncer.bounces.ContainsKey(___pawn))
            {
                SLog.Message(string.Format("Pawn {0} is in bouncer", ___pawn.Name.ToStringShort));
                float bounceOffset = LovinBouncer.bounces[___pawn];
                drawLoc.z += bounceOffset;
            }
            */
        }
    }
}