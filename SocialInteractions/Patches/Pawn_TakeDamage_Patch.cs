using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Reflection;
using Verse.AI.Group;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(ThingWithComps), "PreApplyDamage")]
    public static class Pawn_TakeDamage_Patch
    {
        public static void Postfix(ThingWithComps __instance, DamageInfo dinfo, ref bool absorbed)
        {
            // Cast to Pawn to check if it's a child and misbehavior is enabled
            Pawn pawn = __instance as Pawn;

            if (pawn != null && pawn.RaceProps.Humanlike && ChildrenMisbehaviorManager.IsChild(pawn) && SocialInteractions.Settings.enableChildrenMisbehavior)
            {
                SLog.Message(string.Format("[SocialInteractions] Child {0} took damage of type {1}, amount {2}",
                    pawn.LabelShort, dinfo.Def.defName, dinfo.Amount));

                // Calculate chance for fleeing based on skills (lower chance if child is skilled)
                float fleeChance = CalculateFleeChance(pawn);

                if (Rand.Value < fleeChance)
                {
                    // Start the flee in terror mental state for the child
                    bool mentalStateStarted = pawn.mindState.mentalStateHandler.TryStartMentalState(
                        SI_MentalStateDefOf.ChildFleeInTerror, "TookDamage", true, false, true, pawn);

                    if (mentalStateStarted)
                    {
                        SLog.Message(string.Format("[SocialInteractions] Child {0} entered flee in terror state after taking damage",
                            pawn.LabelShort));
                    }
                    else
                    {
                        SLog.Message(string.Format("[SocialInteractions] Failed to start flee in terror mental state for child {0}",
                            pawn.LabelShort));
                    }
                }
                else
                {
                    SLog.Message(string.Format("[SocialInteractions] Child {0} did not flee (chance was {1:F2}, rolled {2:F2})",
                        pawn.LabelShort, fleeChance, Rand.Value));
                }
            }
            
            // Check for Raid Negotiation Aggression Trigger
            if (pawn != null && pawn.GetLord() != null)
            {
                var lordJob = pawn.GetLord().LordJob as LordJob_NegotiatedRaid;
                if (lordJob != null)
                {
                    lordJob.Notify_RaiderHarmed(pawn, dinfo);
                }
            }
        }

        private static float CalculateFleeChance(Pawn child)
        {
            // Base chance of fleeing when taking damage
            float baseChance = 0.8f; // 80% base chance for unskilled child soldiers

            // Reduce chance based on shooting and melee skills
            if (child.skills != null)
            {
                float shootingSkill = child.skills.GetSkill(SkillDefOf.Shooting).Level;
                float meleeSkill = child.skills.GetSkill(SkillDefOf.Melee).Level;

                // The higher the skills, the less likely to flee (based on the TODO: "very low once it's past 10")
                float skillFactor = (shootingSkill + meleeSkill) / 20f; // Normalize to 0-1 range (if both skills are 10, factor = 1)

                // Cap the reduction so there's always some chance to flee
                skillFactor = UnityEngine.Mathf.Clamp(skillFactor, 0f, 0.9f); // Max 90% reduction

                baseChance = baseChance * (1f - skillFactor);
            }

            // Ensure minimum chance doesn't go below 5%
            baseChance = UnityEngine.Mathf.Max(baseChance, 0.05f);

            return baseChance;
        }

        private static IntVec3 FindFleeLocation(Pawn child)
        {
            if (child.Map == null)
            {
                return IntVec3.Invalid;
            }

            // Find a random location away from the danger (the damage source)
            IntVec3 sourceLocation = child.Position;

            // Look for a safe location in a 20-cell radius
            foreach (IntVec3 c in GenRadial.RadialCellsAround(child.Position, 20, true))
            {
                if (c.InBounds(child.Map) && c.Walkable(child.Map) && !c.Fogged(child.Map))
                {
                    // Check if it's a reasonably safe location (not near enemies, etc.)
                    if (IsSafeLocation(child, c))
                    {
                        return c;
                    }
                }
            }

            // If no specific safe location found, return the child's current position as default
            return child.Position;
        }

        private static bool IsSafeLocation(Pawn child, IntVec3 location)
        {
            // Check if there are any hostile pawns nearby that could pose a threat
            foreach (Pawn otherPawn in child.Map.mapPawns.AllPawns)
            {
                if (otherPawn != null && otherPawn != child &&
                    !otherPawn.Dead && otherPawn.Spawned &&
                    otherPawn.HostileTo(child)) // Check if this pawn is hostile to the child
                {
                    // If this hostile pawn is too close to the proposed location, it's not safe
                    float distance = (location - otherPawn.Position).LengthHorizontal;
                    if (distance < 10f) // Consider as potentially unsafe if closer than 10 cells
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void FindParentToCryTo(Pawn child, int reasonType = 0)
        {
            // After fleeing, there's a chance the child will go to a parent if one is close
            if (Rand.Value < 0.5f) // 50% chance to look for parent after fleeing
            {
                Pawn parent = FindParentOrMostLikedPawn(child);

                if (parent != null && parent != child && parent.Spawned && !parent.Dead)
                {
                    // Check if parent is close enough to go to
                    float distance = (child.Position - parent.Position).LengthHorizontal;
                    if (distance <= 30f) // If parent is within 30 cells
                    {
                        // Create the job for the child to go cry to the parent, with reason type
                        Job cryJob = JobMaker.MakeJob(SI_JobDefOf.ChildGoCryToParent, parent);
                        cryJob.count = reasonType; // Store the comfort reason type in the job
                        child.jobs.TryTakeOrderedJob(cryJob);

                        SLog.Message(string.Format("[SocialInteractions] Child {0} is going to cry to parent {1} after being scared",
                            child.LabelShort, parent.LabelShort));
                    }
                }
            }
        }

        private static Pawn FindParentOrMostLikedPawn(Pawn child)
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