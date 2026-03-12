using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    public class MentalState_ChildFleeInTerror : MentalState
    {
        public override void PostStart(string reason)
        {
            base.PostStart(reason);

            // Apply crying thought to the child (fear/distress)
            if (pawn.needs != null && pawn.needs.mood != null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildCrying, null);
            }

            // Show message to player about the child fleeing in terror
            string message = string.Format("{0} is fleeing in terror after taking damage!", pawn.LabelShort);
            Messages.Message(message, new LookTargets(pawn), MessageTypeDefOf.NegativeEvent);

            // Trigger a monologue for the child about being scared (disabled for now)
            // string subject = "I'm so scared after being hurt!";
            // SocialInteractions.HandleMonologue(pawn, subject);
        }

        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);

            // Continue fleeing behavior every tick
            if (pawn.mindState.duty == null || pawn.mindState.duty.def.defName != "ExitMapBest")
            {
                // Find the nearest threat to flee from
                Pawn nearestThreat = FindNearbyThreat();
                if (nearestThreat != null)
                {
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.Goto, nearestThreat.Position); // Move away from the threat
                    pawn.mindState.duty.locomotion = LocomotionUrgency.Sprint; // Flee quickly
                }
                else
                {
                    // If no specific threat, just try to get to the edge of the map
                    IntVec3 edgeCell = FindEdgeCell(pawn.Map);
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBest, edgeCell);
                    pawn.mindState.duty.locomotion = LocomotionUrgency.Sprint;
                }
            }

            // End the mental state when the pawn is in a relatively safe place
            if (IsInSafeLocation() && Find.TickManager.TicksGame % 120 == 0) // Check every 2 seconds
            {
                RecoverFromState();
            }
        }

        private IntVec3 FindEdgeCell(Map map)
        {
            // Find a random edge cell by using GenRadial to generate cells around the center and check if they're at the edge
            IntVec3 center = map.Center;
            int maxRadius = Math.Min(map.Size.x, map.Size.z) / 2;

            // Try to find a cell near the edge of the map
            for (int radius = maxRadius - 5; radius <= maxRadius; radius++)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
                {
                    if (cell.InBounds(map) && cell.Walkable(map))
                    {
                        return cell;
                    }
                }
            }

            // If no edge cell found, return a random walkable cell
            return CellFinder.RandomCell(map);
        }

        private Pawn FindNearbyThreat()
        {
            Pawn nearestThreat = null;
            float nearestDist = float.MaxValue;

            foreach (Pawn otherPawn in pawn.Map.mapPawns.AllPawns)
            {
                if (otherPawn != null && otherPawn != pawn &&
                    !otherPawn.Dead && otherPawn.Spawned && otherPawn.HostileTo(pawn))
                {
                    float dist = (pawn.Position - otherPawn.Position).LengthHorizontal;
                    if (dist < nearestDist && dist < 40f) // Only consider threats within 40 cells
                    {
                        nearestDist = dist;
                        nearestThreat = otherPawn;
                    }
                }
            }

            return nearestThreat;
        }

        private bool IsInSafeLocation()
        {
            // Check if there are no hostile pawns nearby
            foreach (Pawn otherPawn in pawn.Map.mapPawns.AllPawns)
            {
                if (otherPawn != null && otherPawn != pawn &&
                    !otherPawn.Dead && otherPawn.Spawned && otherPawn.HostileTo(pawn))
                {
                    float dist = (pawn.Position - otherPawn.Position).LengthHorizontal;
                    if (dist < 15f) // If threat is still within 15 cells, not safe yet
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public override RandomSocialMode SocialModeMax()
        {
            return RandomSocialMode.Off;
        }

        public override void PostEnd()
        {
            base.PostEnd();

            // After fleeing, look for parent to cry to
            TryStartCryToParentJob();
        }

        private void TryStartCryToParentJob()
        {
            // Find parent or most liked pawn to cry to
            Pawn parent = FindParentOrMostLikedPawn(pawn);

            if (parent != null && parent != pawn && parent.Spawned && !parent.Dead)
            {
                // Check if parent is close enough to go to
                float distance = (pawn.Position - parent.Position).LengthHorizontal;
                if (distance <= 30f) // If parent is within 30 cells
                {
                    // Create the job for the child to go cry to the parent
                    Job cryJob = JobMaker.MakeJob(SI_JobDefOf.ChildGoCryToParent, parent);
                    cryJob.count = 1; // Store the comfort reason type in the job
                    pawn.jobs.TryTakeOrderedJob(cryJob);

                    SLog.Message(string.Format("[SocialInteractions] Child {0} is going to cry to parent {1} after fleeing",
                        pawn.LabelShort, parent.LabelShort));
                }
            }
        }

        private Pawn FindParentOrMostLikedPawn(Pawn child)
        {
            if (child.relations == null)
            {
                return null;
            }

            // First, look for parents
            foreach (Pawn potentialParent in child.Map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (potentialParent != null && !potentialParent.Dead && potentialParent.Spawned)
                {
                    if (child.relations.DirectRelationExists(PawnRelationDefOf.Parent, potentialParent))
                    {
                        return potentialParent;
                    }
                }
            }

            // If no parents found, look for the most liked pawn (highest opinion of the child)
            Pawn mostLiked = null;
            int highestOpinion = int.MinValue;

            foreach (Pawn potentialPawn in child.Map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (potentialPawn != null && !potentialPawn.Dead && potentialPawn.Spawned && potentialPawn != child)
                {
                    int opinion = (child.relations != null) ? child.relations.OpinionOf(potentialPawn) : 0;
                    if (opinion > highestOpinion)
                    {
                        highestOpinion = opinion;
                        mostLiked = potentialPawn;
                    }
                }
            }

            return mostLiked;
        }
    }
}