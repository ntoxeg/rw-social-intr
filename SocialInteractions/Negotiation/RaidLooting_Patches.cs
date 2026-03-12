using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(GenAI), "InDangerousCombat")]
    public static class GenAI_InDangerousCombat_Patch
    {
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            // If the pawn is part of a negotiated raid and is currently plundering, 
            // ignore "dangerous combat" so they keep stealing instead of fighting.
            if (pawn != null && pawn.GetLord() != null && pawn.GetLord().LordJob is LordJob_NegotiatedRaid)
            {
                if (pawn.mindState != null && pawn.mindState.duty != null && (pawn.mindState.duty.def == DutyDefOf.Steal || pawn.mindState.duty.def.defName == "Steal"))
                {
                    __result = false;
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(StealAIUtility), "TryFindBestItemToSteal")]
    public static class StealAIUtility_TryFindBestItemToSteal_Patch
    {
        public static bool Prefix(IntVec3 root, Map map, float maxDist, ref Thing item, Pawn thief, List<Thing> disallowed, ref bool __result)
        {
            // If this is one of our plundering raiders, we want to allow them to pass through doors
            // and potentially search a wider area (though the caller usually defines maxDist).
            if (thief != null && thief.GetLord() != null && thief.GetLord().LordJob is LordJob_NegotiatedRaid)
            {
                if (thief.mindState != null && thief.mindState.duty != null && (thief.mindState.duty.def == DutyDefOf.Steal || thief.mindState.duty.def.defName == "Steal"))
                {
                    // Run a custom search with TraverseMode.PassDoors
                    // We FORCE a larger distance (60f) here because JobGiver_Steal normally passes a small 12f radius.
                    float forcedMaxDist = Math.Max(maxDist, 60f);
                    __result = TryFindBestItemToStealCustom(root, map, forcedMaxDist, out item, thief, disallowed);
                    
                    if (__result)
                        SLog.Message("[Plunder] " + thief.LabelShort + " found item to steal: " + item.Label + " at distance " + forcedMaxDist);
                    else
                        SLog.Message("[Plunder] " + thief.LabelShort + " found NO item to steal within " + forcedMaxDist + " (attempted with door passing)");

                    return false; // Skip vanilla logic
                }
            }
            return true;
        }

        private static bool TryFindBestItemToStealCustom(IntVec3 root, Map map, float maxDist, out Thing item, Pawn thief, List<Thing> disallowed = null)
        {
            if (map == null)
            {
                item = null;
                return false;
            }
            if (thief != null && !thief.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                item = null;
                return false;
            }

            // Check reachability to edge - using PassDoors here too
            if (thief != null && !map.reachability.CanReachMapEdge(thief.Position, TraverseParms.For(thief, Danger.Some, TraverseMode.PassDoors)))
            {
                item = null;
                return false;
            }

            Predicate<Thing> validator = delegate(Thing t)
            {
                if (thief != null && !thief.CanReserve(t))
                {
                    return false;
                }
                if (disallowed != null && disallowed.Contains(t))
                {
                    return false;
                }
                if (!t.def.stealable)
                {
                    return false;
                }
                return !t.IsBurning();
            };

            // The KEY CHANGE: TraverseMode.PassDoors instead of NoPassClosedDoors
            item = GenClosest.ClosestThing_Regionwise_ReachablePrioritized(
                root, 
                map, 
                ThingRequest.ForGroup(ThingRequestGroup.HaulableEverOrMinifiable), 
                PathEndMode.ClosestTouch, 
                TraverseParms.For(TraverseMode.PassDoors, Danger.Some), 
                maxDist, 
                validator, 
                (Thing x) => StealAIUtility.GetValue(x), 
                15, 
                15
            );

            if (item != null && StealAIUtility.GetValue(item) < 320f)
            {
                item = null;
            }

            return item != null;
        }
    }
}
