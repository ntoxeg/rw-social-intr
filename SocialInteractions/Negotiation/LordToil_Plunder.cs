using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace SocialInteractions
{
    /// <summary>
    /// A more persistent version of LordToil_StealCover that doesn't revert to assault 
    /// if loot isn't immediately found and has a larger search radius.
    /// </summary>
    public class LordToil_Plunder : LordToil_StealCover
    {
        public LordToil_Plunder()
        {
            this.cover = false; // Disable "revert to assault" logic in base class
            this.useAvoidGrid = true;
        }

        protected override bool TryFindGoodOpportunisticTaskTarget(Pawn pawn, out Thing target, List<Thing> alreadyTakenTargets)
        {
            if (pawn.mindState.duty != null && pawn.mindState.duty.def == this.DutyDef && pawn.carryTracker.CarriedThing != null)
            {
                target = pawn.carryTracker.CarriedThing;
                return true;
            }

            // Use a much larger radius than vanilla's 7f
            // We pass alreadyTakenTargets to avoid multiple pawns targeting the same item
            return StealAIUtility.TryFindBestItemToSteal(pawn.Position, pawn.Map, 60f, out target, pawn, alreadyTakenTargets);
        }

        public override void LordToilTick()
        {
            // The base class LordToil_DoOpportunisticTaskOrCover returns early if cover is false.
            // We want it to run periodically anyway to re-assign duties if needed.
            if (Find.TickManager.TicksGame % 181 == 0)
            {
                this.UpdateAllDuties();
            }
        }
    }
}
