using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using System.Collections.Generic;

namespace SocialInteractions
{
    /// <summary>
    /// Joy giver that makes pawns with certain traits/genes pester prisoners or slaves for joy.
    /// Requires: Psychopath trait, incapable of social, or aggressive genes.
    /// </summary>
    public class JoyGiver_PesterPrisoner : JoyGiver
    {
        private static Dictionary<Pawn, int> lastAttemptTick = new Dictionary<Pawn, int>();

        public override Job TryGiveJob(Pawn pawn)
        {
            // Basic validity checks
            if (pawn == null || !SocialInteractions.Settings.enablePesterPrisonerFeature)
                return null;

            // Cooldown check to prevent spam
            int lastTick;
            if (lastAttemptTick.TryGetValue(pawn, out lastTick) &&
                Find.TickManager.TicksGame - lastTick < 600) // 10 second cooldown
                return null;

            lastAttemptTick[pawn] = Find.TickManager.TicksGame;

            // Check if pawn has joy needs
            if (pawn.needs == null || pawn.needs.joy == null)
                return null;

            // Check basic conditions
            if (!pawn.Awake() || pawn.InBed() || pawn.Drafted || pawn.InMentalState)
                return null;

            // Check if pawn has qualifying traits/genes
            if (!HasQualifyingTraitsOrGenes(pawn))
                return null;

            // Find a suitable prisoner/slave target
            Pawn target = FindPrisonerOrSlaveTarget(pawn);
            if (target == null || !pawn.CanReserve(target))
                return null;

            return JobMaker.MakeJob(SI_JobDefOf.PesterPrisoner, target);
        }

        /// <summary>
        /// Checks if pawn has qualifying traits or genes for pestering prisoners
        /// </summary>
        private bool HasQualifyingTraitsOrGenes(Pawn pawn)
        {
            if (pawn.story == null || pawn.story.traits == null)
                return false;

            // Check for Psychopath trait
            if (pawn.story.traits.HasTrait(TraitDefOf.Psychopath))
                return true;

            // Check for incapable of social
            if (pawn.WorkTagIsDisabled(WorkTags.Social))
                return true;

            // Check for aggressive genes
            if (pawn.genes != null)
            {
                // Check for aggressive/violent genes
                GeneDef aggressiveGeneDef = DefDatabase<GeneDef>.GetNamedSilentFail("Aggression_HyperAggressive");
                if (aggressiveGeneDef != null && pawn.genes.HasActiveGene(aggressiveGeneDef))
                    return true;

                GeneDef violentGeneDef = DefDatabase<GeneDef>.GetNamedSilentFail("Violence_Aggressive");
                if (violentGeneDef != null && pawn.genes.HasActiveGene(violentGeneDef))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Finds a suitable prisoner or slave that the pawn dislikes
        /// </summary>
        private Pawn FindPrisonerOrSlaveTarget(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || pawn.Map.mapPawns == null)
                return null;

            // Get all prisoners and slaves
            List<Pawn> allTargets = pawn.Map.mapPawns.AllPawnsSpawned.Where(p =>
                p != null && p != pawn &&
                (p.IsPrisonerOfColony || p.IsSlave) &&
                !p.Downed && !p.Dead &&
                p.Awake() && !p.InBed() &&
                pawn.CanReserveAndReach(p, PathEndMode.InteractionCell, Danger.None)).ToList();

            if (allTargets.Count == 0)
                return null;

            // Calculate weights based on opinion (more negative = higher weight)
            List<KeyValuePair<Pawn, float>> weightedTargets = new List<KeyValuePair<Pawn, float>>();

            foreach (Pawn target in allTargets)
            {
                int opinion = pawn.relations.OpinionOf(target);
                
                // Only consider targets with negative opinion
                if (opinion >= 0)
                    continue;

                // Weight increases with more negative opinion
                float weight = System.Math.Abs(opinion);
                
                // Compound the weight based on number of qualifying conditions
                int qualifyingConditions = CountQualifyingConditions(pawn);
                weight *= (1f + (qualifyingConditions - 1) * 0.5f);

                weightedTargets.Add(new KeyValuePair<Pawn, float>(target, weight));
            }

            if (weightedTargets.Count == 0)
                return null;

            // Select using weighted random
            return SelectTargetWeighted(weightedTargets);
        }

        /// <summary>
        /// Counts how many qualifying conditions the pawn has
        /// </summary>
        private int CountQualifyingConditions(Pawn pawn)
        {
            int count = 0;

            if (pawn.story != null && pawn.story.traits != null)
            {
                if (pawn.story.traits.HasTrait(TraitDefOf.Psychopath))
                    count++;

                if (pawn.WorkTagIsDisabled(WorkTags.Social))
                    count++;
            }

            if (pawn.genes != null)
            {
                GeneDef aggressiveGeneDef = DefDatabase<GeneDef>.GetNamedSilentFail("Aggression_HyperAggressive");
                if (aggressiveGeneDef != null && pawn.genes.HasActiveGene(aggressiveGeneDef))
                    count++;

                GeneDef violentGeneDef = DefDatabase<GeneDef>.GetNamedSilentFail("Violence_Aggressive");
                if (violentGeneDef != null && pawn.genes.HasActiveGene(violentGeneDef))
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Selects a target using weighted random selection
        /// </summary>
        private Pawn SelectTargetWeighted(List<KeyValuePair<Pawn, float>> weightedTargets)
        {
            if (weightedTargets == null || weightedTargets.Count == 0)
                return null;

            float totalWeight = weightedTargets.Sum(pair => pair.Value);
            if (totalWeight <= 0f)
                return weightedTargets[0].Key;

            float randomValue = Rand.Value * totalWeight;
            float currentWeight = 0f;
            foreach (var pair in weightedTargets)
            {
                currentWeight += pair.Value;
                if (randomValue <= currentWeight)
                    return pair.Key;
            }

            return weightedTargets.Last().Key;
        }
    }
}
