using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JobDriver_ChildLightFire : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Child should be able to reserve the flammable thing
            return pawn.Reserve(job.GetTarget(TargetIndex.A).Thing, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail if child is captured or recruited to another faction
            this.FailOn(() => pawn.HostFaction != null || (pawn.Faction != null && pawn.Faction != Faction.OfPlayer));
            // Fail if child gets drafted
            this.FailOn(() => pawn.Drafted);

            // Go to the target flammable thing
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Create the main fire-lighting toil where the child ignites the target
            Toil lightFireToil = new Toil();
            lightFireToil.initAction = delegate
            {
                Thing target = job.GetTarget(TargetIndex.A).Thing;

                if (target == null || target.Destroyed)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_ChildLightFire: Target is null or destroyed, ending job");
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Ignite the target thing
                // We use FireUtility directly to ensure the fire starts immediately so we can make the child flee
                // (TryStartIgnite would start a new job and interrupt this one, preventing the flee logic)
                bool successfullyIgnited = false;
                if (target.FlammableNow && !target.IsBurning())
                {
                     successfullyIgnited = FireUtility.TryStartFireIn(target.Position, target.Map, 0.3f, pawn);
                }

                SLog.Message(string.Format("[SocialInteractions] JobDriver_ChildLightFire: Child {0} lit a fire on {1}, success: {2}",
                    pawn.LabelShort, target.Label, successfullyIgnited));

                // Add a thought to the child about dangerous behavior
                if (pawn.needs != null && pawn.needs.mood != null)
                {
                    pawn.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildReckless, null);
                }

                // If successfully ignited, flee!
                if (successfullyIgnited)
                {
                    SLog.Message(string.Format("[SocialInteractions] Child {0} fleeing from fire at {1}", pawn.LabelShort, target.Position));

                    // Trigger LLM interaction about lighting fire
                    string subject = string.Format("lit a fire on {0}, this could be dangerous!", target.Label);
                    SocialInteractions.HandleMonologue(pawn, subject);

                    // Add exclamation mote
                    MoteMaker.MakeColonistActionOverlay(pawn, ThingDefOf.Mote_ColonistFleeing);

                    // Flee from the fire
                    IntVec3 fleeDest = CellFinderLoose.GetFleeDest(pawn, new List<Thing>{target}, 20f);
                    if (fleeDest != IntVec3.Invalid)
                    {
                         Job runJob = JobMaker.MakeJob(JobDefOf.Goto, fleeDest);
                         runJob.locomotionUrgency = LocomotionUrgency.Sprint;
                         pawn.jobs.StartJob(runJob, JobCondition.InterruptForced);
                    }
                }
            };

            // Complete after a short duration
            lightFireToil.defaultCompleteMode = ToilCompleteMode.Delay;
            lightFireToil.defaultDuration = 180; // Short duration for igniting
            lightFireToil.socialMode = RandomSocialMode.Off; // No social interaction during this dangerous activity
            yield return lightFireToil;
        }
    }
}