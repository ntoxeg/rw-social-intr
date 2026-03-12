using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JobDriver_SocialRelaxDate : JobDriver
    {
        private const TargetIndex SpotInd = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // We don't necessarily need to reserve the spot since it might just be a random cell
            // but we'll try to reserve it if it's a thing (like a chair)
            if (job.GetTarget(SpotInd).HasThing)
            {
                return pawn.Reserve(job.GetTarget(SpotInd), job, 1, -1, null, errorOnFailed);
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // End if target is null or despawned (if it's a thing)
            this.EndOnDespawnedOrNull(SpotInd);

            // Go to the spot
            yield return Toils_Goto.GotoCell(SpotInd, PathEndMode.OnCell);

            // Relaxation Toil
            Toil relax = ToilMaker.MakeToil("Relax");
            relax.tickAction = delegate()
            {
                pawn.GainComfortFromCellIfPossible(1);
                
                // Gain Joy at the same rate as the partner would from watching
                if (pawn.needs != null && pawn.needs.joy != null)
                {
                    pawn.needs.joy.GainJoy(0.000144f, JoyKindDefOf.Social);
                }

                // --- Wandering logic ---
                // Every 2.5 seconds (150 ticks), possibly move to a nearby cell
                if (pawn.IsHashIntervalTick(150))
                {
                    // 20% chance to wander if not already moving
                    if (Rand.Value < 0.5f && !pawn.pather.Moving)
                    {
                        // Wander within 4 cells of the original spot
                        IntVec3 wanderTarget = RCellFinder.RandomWanderDestFor(pawn, job.GetTarget(SpotInd).Cell, 4f, null, Danger.None);
                        if (wanderTarget.IsValid && wanderTarget != pawn.Position)
                        {
                            pawn.pather.StartPath(wanderTarget, PathEndMode.OnCell);
                        }
                    }
                }
                
                // If not moving, face the partner to maintain the social atmosphere
                if (pawn.pather != null && !pawn.pather.Moving)
                {
                    Pawn partner = DatingManager.GetPartnerOfDateWith(pawn);
                    if (partner != null && partner.Spawned && partner.Map == pawn.Map)
                    {
                        pawn.rotationTracker.FaceCell(partner.Position);
                    }
                }
                // --- End Wandering logic ---

                // We don't use JoyUtility.JoyTickCheckEnd here because we want the date 
                // to last for a specific duration, not just until joy is full.
            };
            relax.defaultCompleteMode = ToilCompleteMode.Delay;
            relax.defaultDuration = job.def.joyDuration > 0 ? job.def.joyDuration : 4000;
            
            // Allow natural social interactions
            relax.socialMode = RandomSocialMode.SuperActive;
            
            relax.AddFinishAction(delegate
            {
                try
                {
                    if (pawn != null && pawn.needs != null && pawn.needs.mood != null)
                    {
                        JoyUtility.TryGainRecRoomThought(pawn);
                    }
                }
                catch (System.Exception ex)
                {
                    SLog.Warning("Exception in JobDriver_SocialRelaxDate finish action: " + ex.ToString());
                }
            });

            yield return relax;

            // Transition Toil - Separate from the main activity to avoid recursive cleanup NRE
            Toil transition = ToilMaker.MakeToil("Transition");
            transition.initAction = delegate
            {
                if (pawn != null && DatingManager.IsOnDate(pawn))
                {
                    DatingManager.AdvanceDateStage(pawn);
                }
            };
            transition.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return transition;
        }
    }
}
