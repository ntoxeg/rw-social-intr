using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    public class JobDriver_PlayTagChaser : JobDriver
    {
        private Pawn Runner
        {
            get
            {
                return (Pawn)TargetA.Thing;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true; // No reservations needed to chase
        }



        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            
            // Wait for runner to start their job (prevent race condition)
            Toil startWait = Toils_General.Wait(30);
            yield return startWait;

            // Loop to keep following
            Toil loopStart = Toils_General.Label();
            yield return loopStart;

            // Go to runner
            Toil follow = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            follow.FailOnDespawnedOrNull(TargetIndex.A);
            follow.FailOn(() => Runner.CurJob == null || Runner.CurJob.def != SI_JobDefOf.SI_PlayTagRunner);
            yield return follow;

            // Wait a tick to prevent tight loop if already there
            yield return Toils_General.Wait(10);

            // Loop back
            yield return Toils_Jump.Jump(loopStart);
        }
    }
}
