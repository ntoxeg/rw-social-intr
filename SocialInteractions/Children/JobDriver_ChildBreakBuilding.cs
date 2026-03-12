using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JobDriver_ChildBreakBuilding : JobDriver
    {
        private const int BonkingDuration = 300; // ~5 seconds

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            
            // Fail if target is not a building with CompBreakdownable
            this.FailOn(() =>
            {
                Thing building = job.GetTarget(TargetIndex.A).Thing;
                if (building == null) return true;
                
                CompBreakdownable breakdownComp = building.TryGetComp<CompBreakdownable>();
                return breakdownComp == null || breakdownComp.BrokenDown;
            });

            // Move to the building
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Bonking toil - attack animation
            Toil bonkToil = new Toil();
            bonkToil.initAction = () =>
            {
                SLog.Message(string.Format("[SocialInteractions] ChildBreakBuilding: Child {0} started bonking {1}",
                    pawn.LabelShort, TargetA.Thing.Label));
            };
            bonkToil.tickAction = () =>
            {
                // Face the building
                pawn.rotationTracker.FaceTarget(TargetA);
                
                // Periodically show attack animation
                if (pawn.IsHashIntervalTick(60)) // Every second
                {
                    pawn.meleeVerbs.TryMeleeAttack(TargetA.Thing);
                }
                
                // Check for nearby adults to flee from
                if (pawn.IsHashIntervalTick(30)) // Check every half second
                {
                    Pawn adult = FindNearbyAdult(pawn);
                    if (adult != null)
                    {
                        SLog.Message(string.Format("[SocialInteractions] Child {0} caught breaking {1} by {2}, fleeing!", 
                            pawn.LabelShort, TargetA.Thing.Label, adult.LabelShort));

                        // Show message to player
                        Messages.Message(string.Format("{0} (child) was caught breaking {1}!", pawn.LabelShort, TargetA.Thing.Label), 
                            MessageTypeDefOf.NegativeEvent);
                        
                        // Add exclamation mote
                        MoteMaker.MakeColonistActionOverlay(pawn, ThingDefOf.Mote_ColonistFleeing);
                        
                        // Trigger "caught" monologue
                        SocialInteractions.HandleMonologue(pawn, string.Format("Uh oh, {0} saw me hitting {1}! I better run!", adult.LabelShort, TargetA.Thing.Label), true, "caught");
                        
                        // Add negative thought for getting caught
                        if (pawn.needs != null && pawn.needs.mood != null)
                        {
                            pawn.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildMisbehaved, null);
                        }
                        
                        // Flee logic
                        IntVec3 fleeDest = CellFinderLoose.GetFleeDest(pawn, new List<Thing>{adult}, 20f);
                        if (fleeDest != IntVec3.Invalid)
                        {
                            Job runJob = JobMaker.MakeJob(JobDefOf.Goto, fleeDest);
                            runJob.locomotionUrgency = LocomotionUrgency.Sprint;
                            pawn.jobs.StartJob(runJob, JobCondition.InterruptForced);
                        }
                        else
                        {
                            EndJobWith(JobCondition.InterruptForced);
                        }
                    }
                }
            };
            bonkToil.defaultCompleteMode = ToilCompleteMode.Delay;
            bonkToil.defaultDuration = BonkingDuration;
            bonkToil.WithProgressBarToilDelay(TargetIndex.A);
            yield return bonkToil;

            // Break the building
            Toil breakToil = new Toil();
            breakToil.initAction = () =>
            {
                Thing building = TargetA.Thing;
                CompBreakdownable breakdownComp = building.TryGetComp<CompBreakdownable>();

                if (breakdownComp != null && !breakdownComp.BrokenDown)
                {
                    // Trigger breakdown
                    breakdownComp.DoBreakdown();

                    SLog.Message(string.Format("[SocialInteractions] ChildBreakBuilding: Child {0} broke {1} at {2}",
                        pawn.LabelShort, building.Label, building.Position));

                    // Show message to player
                    Messages.Message(string.Format("{0} (child) broke {1}!", pawn.LabelShort, building.Label),
                        new LookTargets(pawn, building), MessageTypeDefOf.NegativeEvent);

                    // Add a thought to the child about being destructive
                    if (pawn.needs != null && pawn.needs.mood != null)
                    {
                        pawn.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildDestructive, null);
                    }

                    // Trigger LLM interaction about breaking property
                    string subject = string.Format("broke {0}, sorry about that!", building.Label);
                    SocialInteractions.HandleMonologue(pawn, subject);
                }
            };
            breakToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return breakToil;
        }

        private Pawn FindNearbyAdult(Pawn child)
        {
            if (child.Map == null) return null;
            
            // Use AllPawnsSpawned to be safer, filter by faction
            foreach (Pawn p in child.Map.mapPawns.AllPawnsSpawned)
            {
                if (p.Faction == child.Faction && p.RaceProps.Humanlike && p != child && !p.Dead && !p.Downed)
                {
                    if (!p.Awake()) continue;
                    
                    if (p.ageTracker.AgeBiologicalYears < 13) continue;

                    // Check distance (5 cells)
                    if (p.Position.InHorDistOf(child.Position, 5f))
                    {
                        if (GenSight.LineOfSight(child.Position, p.Position, child.Map))
                        {
                            SLog.Message(string.Format("[SocialInteractions] Found adult {0} near child {1} (Dist: {2:F1})", p.LabelShort, child.LabelShort, p.Position.DistanceTo(child.Position)));
                            return p;
                        }
                    }
                }
            }
            return null;
        }
    }
}
