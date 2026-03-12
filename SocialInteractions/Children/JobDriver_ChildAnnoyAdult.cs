using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JobDriver_ChildAnnoyAdult : JobDriver
    {
        private const int AnnoyanceInterval = 180; // How often the child pesters the adult (in ticks)
        private int lastAnnoyanceTick = 0;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Children should be able to reserve the target adult
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail if the target adult disappears or becomes invalid
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnMentalState(TargetIndex.A);

            // Go to the target adult initially
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Create the main annoyance toil where the child interacts with the adult
            Toil annoyanceToil = new Toil();
            annoyanceToil.initAction = delegate
            {
                Pawn targetAdult = (Pawn)job.GetTarget(TargetIndex.A).Thing;

                if (targetAdult == null)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_ChildAnnoyAdult: Target adult is null, ending job");
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (targetAdult.Dead || targetAdult.Downed)
                {
                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_ChildAnnoyAdult: Target {0} is dead or downed, ending job", targetAdult.LabelShort));
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Perform the annoying interaction
                TryStartAnnoyingInteraction(targetAdult);
                lastAnnoyanceTick = Find.TickManager.TicksGame;
            };

            // Add a tick action to follow the adult if they move away
            annoyanceToil.tickAction = delegate
            {
                Pawn targetAdult = (Pawn)job.GetTarget(TargetIndex.A).Thing;
                if (targetAdult != null && !targetAdult.Dead && targetAdult.Spawned)
                {
                    // Check if we should update the path to follow the adult
                    if (pawn.IsHashIntervalTick(60)) // Check every second
                    {
                        // If the adult has moved significantly, update our path to follow them
                        float distance = (pawn.Position - targetAdult.Position).LengthHorizontal;
                        if (distance > 2f) // If more than 2 cells away
                        {
                            // Update path to follow adult
                            pawn.pather.StartPath(targetAdult, PathEndMode.Touch);
                        }
                    }
                }
            };

            // Complete after a certain duration (the annoyance job)
            annoyanceToil.defaultCompleteMode = ToilCompleteMode.Delay;
            annoyanceToil.defaultDuration = 1800; // 30 seconds of interaction time
            annoyanceToil.socialMode = RandomSocialMode.Normal; // Allow normal social interactions
            yield return annoyanceToil;
        }

        private void TryStartAnnoyingInteraction(Pawn targetAdult)
        {
            if (pawn == null || targetAdult == null)
            {
                SLog.Warning("[SocialInteractions] JobDriver_ChildAnnoyAdult: Null pawn detected, skipping interaction");
                return;
            }

            // Check if we can interact with the target
            if (!pawn.CanReach(targetAdult, PathEndMode.Touch, Danger.Deadly))
            {
                SLog.Warning("[SocialInteractions] JobDriver_ChildAnnoyAdult: Cannot reach target adult for annoyance interaction");
                return;
            }

            // Apply negative mood to the adult (annoyance effect)
            if (targetAdult.needs != null && targetAdult.needs.mood != null)
            {
                targetAdult.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildAnnoyance, pawn);
            }

            // Perform the annoying interaction using the interaction system
            // This will trigger the interaction and the LLM processing if enabled
            if (pawn.interactions != null)
            {
                pawn.interactions.TryInteractWith(targetAdult, SI_InteractionDefOf.ChildAnnoying);
            }
        }
    }
}