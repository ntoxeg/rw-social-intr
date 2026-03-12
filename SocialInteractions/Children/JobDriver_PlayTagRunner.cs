using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    public class JobDriver_PlayTagRunner : JobDriver
    {
        private int iterations = 0;
        private const int MaxIterations = 5;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true; // No reservations needed to run around
        }



        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Loop start
            Toil loopStart = Toils_General.Label();
            yield return loopStart;

            // Find random spot
            Toil findSpot = ToilMaker.MakeToil("FindRunSpot");
            findSpot.initAction = delegate
            {
                IntVec3 result;
                // Simple radial check for a random cell nearby
                if (CellFinder.TryFindRandomCellNear(pawn.Position, pawn.Map, 40, (IntVec3 c) => c.Standable(pawn.Map) && !c.IsForbidden(pawn) && pawn.CanReach(c, PathEndMode.OnCell, Danger.None), out result))
                {
                    job.targetA = result;
                }
                else
                {
                    // If can't find a spot, just end the job gracefully
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            findSpot.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return findSpot;

            // Run to spot
            Toil runToSpot = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            runToSpot.tickAction = delegate
            {
                // pawn.pather.speedInfo = "Running"; // Not available in standard API // Flavor, doesn't actually change speed logic directly without other mods usually, but good for debug
            };
            yield return runToSpot;

            // Wait/Taunt
            Toil wait = Toils_General.Wait(60 + Rand.Range(0, 60)); // 1-2 seconds
            wait.tickAction = delegate
            {
                // Maybe throw some motes or look at chaser if we had reference
            };
            yield return wait;

            // Check loop condition
            Toil loopCheck = ToilMaker.MakeToil("LoopCheck");
            loopCheck.initAction = delegate
            {
                iterations++;
                if (iterations < MaxIterations)
                {
                    JumpToToil(loopStart);
                }
            };
            loopCheck.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return loopCheck;
        }
    }
}
