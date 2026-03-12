using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JobDriver_ChildTrampleCrops : JobDriver
    {
        private const int BaseTrampleDuration = 1800; // 30s duration for crop trampling around an area
        private const int TrampleCheckInterval = 300; // Check every ~5 seconds for new plants to trample

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Child should be able to reserve the target area initially
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail if child is captured or recruited to another faction
            this.FailOn(() => pawn.HostFaction != null || (pawn.Faction != null && pawn.Faction != Faction.OfPlayer));
            // Fail if child gets drafted
            this.FailOn(() => pawn.Drafted);

            // Go to the general area initially (this should be near growing zones)
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            // Create the main trampling toil with a proper movement sequence
            Toil findAndTrampleToil = new Toil()
            {
                initAction = () =>
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_ChildTrampleCrops: Child {0} starting trampling activity in area {1}",
                        pawn.LabelShort, job.GetTarget(TargetIndex.A).Cell));

                    // Find first plant to trample
                    Plant plantToTrample = FindPlantToTrample(pawn);
                    if (plantToTrample != null)
                    {
                        // Set the plant as the target for the next phase
                        job.SetTarget(TargetIndex.B, plantToTrample);
                    }
                    else
                    {
                        // No crops found, child is bored
                        SLog.Message("[SocialInteractions] JobDriver_ChildTrampleCrops: No crops found to trample.");
                        SocialInteractions.HandleMonologue(pawn, "is bored because there are no crops to trample", true, "bored");
                        EndJobWith(JobCondition.Incompletable);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = BaseTrampleDuration,
                socialMode = RandomSocialMode.Off
            };

            findAndTrampleToil.tickAction = () =>
            {
                if (pawn.IsHashIntervalTick(TrampleCheckInterval))
                {
                    // Get the current target plant or find a new one
                    Thing currentTargetPlant = job.GetTarget(TargetIndex.B).Thing;
                    Plant plantToTrample = null;

                    // If current target is valid and not destroyed, we'll use it
                    if (currentTargetPlant != null && currentTargetPlant is Plant &&
                        !currentTargetPlant.Destroyed && ((Plant)currentTargetPlant).Spawned)
                    {
                        plantToTrample = (Plant)currentTargetPlant;
                    }

                    // If current target is not valid, find a new one
                    if (plantToTrample == null)
                    {
                        plantToTrample = FindPlantToTrample(pawn);
                        if (plantToTrample != null)
                        {
                            job.SetTarget(TargetIndex.B, plantToTrample);
                        }
                        else
                        {
                             // No more crops found
                             SLog.Message("[SocialInteractions] JobDriver_ChildTrampleCrops: No more crops found.");
                             SocialInteractions.HandleMonologue(pawn, "is bored because there are no crops to trample", true, "bored");
                             EndJobWith(JobCondition.Incompletable);
                             return;
                        }
                    }

                    if (plantToTrample != null)
                    {
                        // Check if we're close enough to trample the plant
                        if (pawn.Position.DistanceTo(plantToTrample.Position) <= 1.42f)
                        {
                            // Trample the plant by killing it
                            plantToTrample.Kill(null, null);

                            SLog.Message(string.Format("[SocialInteractions] JobDriver_ChildTrampleCrops: Child {0} trampled crop {1} at {2}",
                                pawn.LabelShort, plantToTrample.Label, plantToTrample.Position));

                            // Show message to player
                            // Messages.Message(string.Format("{0} (child) trampled {1} crop!", pawn.LabelShort, plantToTrample.Label),
                            //     new LookTargets(pawn, plantToTrample), MessageTypeDefOf.NegativeEvent);

                            // Add a thought to the child about being destructive
                            if (pawn.needs != null && pawn.needs.mood != null)
                            {
                                pawn.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildDestructive, null);
                            }

                            // Clear the current target so we find a new one next time
                            job.SetTarget(TargetIndex.B, IntVec3.Invalid);
                        }
                        else
                        {
                            // Move toward the plant if not close enough
                            pawn.pather.StartPath(plantToTrample, PathEndMode.Touch);
                        }
                    }
                }

                // Check for nearby adults to flee from
                if (pawn.IsHashIntervalTick(30)) // Check every half second
                {
                    // Only check for adults if we are actually near a crop to trample
                    // This prevents fleeing while just walking to the field
                    Thing targetPlant = job.GetTarget(TargetIndex.B).Thing;
                    bool nearTarget = targetPlant != null && targetPlant.Spawned && pawn.Position.InHorDistOf(targetPlant.Position, 4f);
                    bool inGrowingZone = IsCellInGrowingZone(pawn.Position, pawn.Map);

                    if (nearTarget || inGrowingZone)
                    {
                        // SLog.Message(string.Format("[SocialInteractions] JobDriver_ChildTrampleCrops: checking for adults around {0}!", pawn.LabelShort));
                        Pawn adult = FindNearbyAdult(pawn);
                        if (adult != null)
                        {
                            SLog.Message(string.Format("[SocialInteractions] Child {0} caught by {1}, fleeing!", pawn.LabelShort, adult.LabelShort));

                            // Show message to player
                            Messages.Message(string.Format("{0} (child) was caught trampling crop!", pawn.LabelShort), MessageTypeDefOf.NegativeEvent);
                            
                            // Add exclamation mote
                            MoteMaker.MakeColonistActionOverlay(pawn, ThingDefOf.Mote_ColonistFleeing);
                            
                            // Trigger "caught" monologue
                            SocialInteractions.HandleMonologue(pawn, string.Format("Uh oh, {0} saw me stomping on crops! I better run!", adult.LabelShort), true, "caught");
                            
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
                }
            };

            yield return findAndTrampleToil;

            // After trampling, add a final thought
            yield return new Toil()
            {
                initAction = () =>
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_ChildTrampleCrops: Child {0} finished trampling crops",
                        pawn.LabelShort));

                    // Trigger LLM interaction about destroying crops
                    string subject = string.Format("destroyed some crops by trampling them, sorry about that!");
                    SocialInteractions.HandleMonologue(pawn, subject);
                }
            };
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
                        else
                        {
                            // SLog.Message(string.Format("[SocialInteractions] Adult {0} is nearby ({1:F1}) but no LOS to child {2}", p.LabelShort, p.Position.DistanceTo(child.Position), child.LabelShort));
                        }
                    }
                }
            }
            return null;
        }

        private Plant FindPlantToTrample(Pawn child)
        {
            if (child == null || child.Map == null)
            {
                return null;
            }

            // Search in a radius around the child's current position
            int searchRadius = 50;
            Plant bestPlant = null;
            float closestDist = float.MaxValue;

            foreach (IntVec3 c in GenRadial.RadialCellsAround(child.Position, searchRadius, true))
            {
                if (!c.InBounds(child.Map)) continue;

                // Get all things at this cell
                List<Thing> things = c.GetThingList(child.Map);
                foreach (Thing thing in things)
                {
                    Plant plant = thing as Plant;
                    if (plant != null && !plant.Destroyed && plant.Spawned)
                    {
                        // Check if it's a crop (not wild plants)
                        if (plant.def.plant != null && plant.def.plant.Sowable && plant.Growth >= 0.1f) // Only trample slightly grown crops
                        {
                            // Check if the plant is in a growing zone or hydroponics
                            if (IsPlantInGrowingZone(plant, child.Map))
                            {
                                // Make sure the child can reserve the plant for trampling
                                if (child.CanReserve(plant))
                                {
                                    float dist = (plant.Position - child.Position).LengthHorizontal;
                                    if (dist < closestDist)
                                    {
                                        closestDist = dist;
                                        bestPlant = plant;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return bestPlant;
        }

        private bool IsPlantInGrowingZone(Plant plant, Map map)
        {
            return IsCellInGrowingZone(plant.Position, map);
        }

        private bool IsCellInGrowingZone(IntVec3 cell, Map map)
        {
            // Check if the cell is in a growing zone (growing zone or hydroponics)
            Zone zone = map.zoneManager.ZoneAt(cell);
            if (zone != null)
            {
                // Check if it's a growing zone
                return zone is Zone_Growing;
            }

            // Check if it's in a plant grower building (e.g. hydroponics)
            Building edifice = cell.GetEdifice(map);
            if (edifice is Building_PlantGrower)
            {
                return true;
            }

            return false;
        }
    }
}