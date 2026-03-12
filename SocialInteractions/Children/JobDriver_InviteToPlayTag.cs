using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    public class JobDriver_InviteToPlayTag : JobDriver
    {
        private Pawn TargetChild
        {
            get
            {
                return (Pawn)TargetA.Thing;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetChild, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !TargetChild.Awake());

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            bool wasAccepted = false;
            Pawn targetPawn = null;

            Toil inviteToil = new Toil();
            inviteToil.initAction = delegate
            {
                Pawn actor = inviteToil.actor;
                Pawn target = TargetChild;
                targetPawn = target;

                // Social interaction check
                if (actor.Spawned && !actor.Downed && !actor.Dead && actor.Awake() && 
                    target.Spawned && !target.Downed && !target.Dead && target.Awake())
                {
                    // We could add a custom interaction def, but for now just simulate the invite
                    MoteMaker.MakeInteractionBubble(actor, target, InteractionDefOf.Chitchat.interactionMote, InteractionDefOf.Chitchat.GetSymbol());
                    
                    // Simple chance to accept based on opinion or random
                    bool accepted = true;
                    if (target.relations != null)
                    {
                        int opinion = target.relations.OpinionOf(actor);
                        if (opinion < 0) accepted = false; // Won't play with someone they dislike
                    }

                    wasAccepted = accepted;

                    if (accepted)
                    {
                        // LLM Interaction - Acceptance
                        string subject = string.Format("{0} invites {1} to play tag, and {1} accepts.", actor.Name.ToStringShort, target.Name.ToStringShort);
                        SocialInteractions.HandleNonStoppingInteraction(actor, target, SI_InteractionDefOf.ChildPlayTag, subject);

                        Messages.Message(string.Format("{0} accepted {1}'s invitation to play tag!", target.LabelShort, actor.LabelShort), 
                            new LookTargets(actor, target), MessageTypeDefOf.PositiveEvent);
                    }
                    else
                    {
                        // LLM Interaction - Rejection
                        string subject = string.Format("{0} invites {1} to play tag, and {1} rejects.", actor.Name.ToStringShort, target.Name.ToStringShort);
                        SocialInteractions.HandleNonStoppingInteraction(actor, target, SI_InteractionDefOf.ChildPlayTag, subject);

                        Messages.Message(string.Format("{0} rejected {1}'s invitation to play tag.", target.LabelShort, actor.LabelShort), 
                            new LookTargets(actor, target), MessageTypeDefOf.NeutralEvent);
                    }
                }
            };
            inviteToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return inviteToil;

            // Separate toil to handle job assignment after interaction completes
            Toil assignJobsToil = new Toil();
            assignJobsToil.initAction = delegate
            {
                if (wasAccepted && targetPawn != null && pawn != null)
                {
                    // Create the chaser job for the target
                    Job chaserJob = JobMaker.MakeJob(SI_JobDefOf.SI_PlayTagChaser, pawn);
                    
                    // Start the partner's job (this interrupts their current job)
                    targetPawn.jobs.StartJob(chaserJob, JobCondition.InterruptForced);
                    
                    // Create the runner job for the actor
                    Job runnerJob = JobMaker.MakeJob(SI_JobDefOf.SI_PlayTagRunner);
                    
                    // Start the initiator's job (this will end the current job)
                    pawn.jobs.StartJob(runnerJob, JobCondition.InterruptForced);
                }
            };
            assignJobsToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return assignJobsToil;
        }
    }
}
