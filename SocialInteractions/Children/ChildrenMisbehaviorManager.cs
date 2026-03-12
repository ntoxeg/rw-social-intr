using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace SocialInteractions
{
    public static class ChildrenMisbehaviorManager
    {
        // Misbehavior factor calculation constants
        private const float MaxMisbehaviorFactor = 1.0f;
        private const float MinMisbehaviorFactor = 0.0f;
        private const float BaseParentalOpinionThreshold = 20f; // Opinion below this increases misbehavior
        private const int ChildAgeLimit = 13; // Pawns under this age are considered children for misbehavior (12 and under)
        private const int ChildMinAge = 3; // Minimum age to be considered for misbehavior
        private const int TeenagerAgeLimit = 17; // Pawns under this age may have different behavior patterns
        private const int MaxTimeSinceParentInteraction = 180000; // 5 days in ticks, after which misbehavior increases
        
        // Misbehavior level thresholds
        private const float Level1Threshold = 0.1f; // Annoying adults
        private const float Level2Threshold = 0.2f; // Misplacing items
        private const float Level3Threshold = 0.3f; // Damaging property
        private const float Level4Threshold = 0.5f; // Dangerous behavior

        // Cooldown constants
        private const int ShortCooldownTicks = 3000; // ~1.2 hours
        private const int LongCooldownTicks = 60000; // 24 hours

        // Track when the child is allowed to misbehave next
        private static Dictionary<Pawn, int> nextAllowedMisbehaviorTick = new Dictionary<Pawn, int>();
        private static int misbehaviorCheckInterval = 3000; // Check every 3000 ticks

        /// <summary>
        /// Calculates the misbehavior factor for a child pawn based on parental opinion and other factors
        /// </summary>
        public static float CalculateMisbehaviorFactor(Pawn child)
        {
            if (child == null || !IsChild(child))
            {
                return 0f;
            }

            float misbehaviorFactor = 0.3f; // Base factor
            float parentImpactMultiplier = SocialInteractions.Settings.childrenMisbehaviorParentOpinionImpact;

            // Factor in child's current mood
            float moodFactor = 0f;
            if (child.needs != null && child.needs.mood != null)
            {
                float moodPercent = child.needs.mood.CurLevelPercentage;
                // Lower mood = higher misbehavior tendency (inverted relationship)
                moodFactor = (0.5f - moodPercent) * 0.4f; // Scale the mood impact
            }

            // Check for parents/guardians and their opinions
            List<Pawn> allPotentialParents = GetParentsAndGuardians(child);

            // Filter to only the most important relationships (parents, spouses, lovers, close family) to avoid dilution
            List<Pawn> significantParents = new List<Pawn>();
            foreach (Pawn potentialParent in allPotentialParents)
            {
                if (potentialParent != null && !potentialParent.Dead && potentialParent.Spawned)
                {
                    // Only include immediate family and significant relationships (not extended family that dilutes the effect)
                    if (child.relations != null)
                    {
                        var directRelationsEnum = child.GetRelations(potentialParent);
                        List<PawnRelationDef> directRelations = directRelationsEnum.ToList(); // Convert to list to allow indexing
                        bool isSignificant = false;

                        foreach (var relation in directRelations)
                        {
                            // Only consider immediate family relations
                            if (relation == PawnRelationDefOf.Parent ||
                                relation == PawnRelationDefOf.Bond)
                            {
                                isSignificant = true;
                                break;
                            }
                        }

                        if (isSignificant)
                        {
                            significantParents.Add(potentialParent);
                        }
                    }
                }
            }

            if (significantParents.Count == 0)
            {
                // No significant parents/guardians = maximum misbehavior tendency
                misbehaviorFactor = Mathf.Min(misbehaviorFactor + 0.5f, MaxMisbehaviorFactor);
            }
            else
            {
                // Calculate based on average significant parental opinion
                float totalOpinion = 0f;
                int validParents = 0;

                foreach (Pawn parent in significantParents)
                {
                    if (parent != null && !parent.Dead && parent.Spawned)
                    {
                        int opinion = 0;
                        if (child.relations != null)
                        {
                            opinion = child.relations.OpinionOf(parent); // FIXED: Child's opinion of the parent, not parent's opinion of child
                        }
                        totalOpinion += opinion;
                        validParents++;
                    }
                }

                if (validParents > 0)
                {
                    float avgParentOpinion = totalOpinion / validParents;

                    // Lower parent opinion increases misbehavior factor
                    if (avgParentOpinion < BaseParentalOpinionThreshold)
                    {
                        float opinionFactor = (BaseParentalOpinionThreshold - avgParentOpinion) / BaseParentalOpinionThreshold;
                        float increaseAmount = opinionFactor * 0.6f * parentImpactMultiplier; // Increased from 0.5f to 0.6f
                        misbehaviorFactor = Mathf.Min(misbehaviorFactor + increaseAmount, MaxMisbehaviorFactor);
                    }
                    else
                    {
                        // Higher parent opinion decreases misbehavior factor
                        float opinionFactor = Mathf.Clamp((avgParentOpinion - BaseParentalOpinionThreshold) / 100f, 0f, 1f);
                        float decreaseAmount = opinionFactor * 0.4f * parentImpactMultiplier; // Increased from 0.3f to 0.4f
                        misbehaviorFactor = Mathf.Max(misbehaviorFactor - decreaseAmount, 0.1f); // Keep minimum at 0.1f
                    }
                }
            }

            // Apply mood factor as a multiplicative factor instead of additive
            float moodMultiplier = 1.0f + (moodFactor * 1.0f); // Adjust multiplier factor as needed
            misbehaviorFactor *= moodMultiplier;

            // Add random factor based on child traits
            float traitInfluence = GetTraitInfluenceOnMisbehavior(child);
            misbehaviorFactor += traitInfluence;

            // Clamp to reasonable range to prevent negative factors from eliminating misbehavior entirely
            misbehaviorFactor = Mathf.Clamp(misbehaviorFactor, 0.1f, MaxMisbehaviorFactor);

            return misbehaviorFactor;
        }

        /// <summary>
        /// Determines if a pawn should engage in misbehavior based on their misbehavior factor and other conditions
        /// </summary>
        public static bool ShouldChildMisbehave(Pawn child, out float misbehaviorLevel)
        {
            misbehaviorLevel = 0f;

            if (child == null)
            {
                return false;
            }

            if (!IsChild(child))
            {
                SLog.Message(string.Format("[SocialInteractions] ShouldChildMisbehave: pawn {0} is not a child", child.LabelShort));
                return false;
            }

            // Do not trigger misbehavior if the child is already in a mental state
            if (child.InMentalState)
            {
                return false;
            }

            // Check if children misbehavior is enabled in settings
            if (!SocialInteractions.Settings.enableChildrenMisbehavior)
            {
                return false;
            }

            // Check if enough time has passed since last misbehavior
            if (nextAllowedMisbehaviorTick.ContainsKey(child))
            {
                int nextTick = nextAllowedMisbehaviorTick[child];
                if (Find.TickManager.TicksGame < nextTick)
                {
                    return false;
                }
            }

            // Check if child is currently in a job that would prevent misbehavior
            if (child.jobs != null && child.CurJob != null)
            {
                if (child.CurJob.def != JobDefOf.GotoWander && child.CurJob.def != JobDefOf.Wait_Wander)
                {
                    return false;
                }
            }

            // Calculate misbehavior factor
            float misbehaviorFactor = CalculateMisbehaviorFactor(child);

            // Apply base chance from settings
            float baseChance = SocialInteractions.Settings.baseChildrenMisbehaviorChance;

            // Calculate the total probability
            float totalChance = baseChance * misbehaviorFactor;
            float randomValue = Rand.Value;

            // Use a random chance based on the base chance and misbehavior factor
            if (randomValue < totalChance)
            {
                SLog.Message(string.Format("[SocialInteractions] ShouldChildMisbehave: Child {0} with misbehavior factor {1} will misbehave! (chance {2:F3} > random {3:F3})",
                    child.LabelShort, misbehaviorFactor, totalChance, randomValue));
                misbehaviorLevel = misbehaviorFactor;
                // Note: We do NOT set the tick here anymore. It's set in ExecuteMisbehavior based on what they actually do.
                return true;
            }

            return false;
        }

        /// <summary>
        /// Executes a misbehavior action for a child pawn
        /// Higher misbehavior levels unlock more options, but only one is randomly selected
        /// </summary>
        public static void ExecuteMisbehavior(Pawn child, float misbehaviorLevel)
        {
            if (child == null)
            {
                return;
            }

            if (!IsChild(child))
            {
                SLog.Message(string.Format("[SocialInteractions] ExecuteMisbehavior: pawn {0} is not a child", child.LabelShort));
                return;
            }

            // Check if misbehavior level is too low first
            if (misbehaviorLevel < Level1Threshold)
            {
                SLog.Message(string.Format("[SocialInteractions] ExecuteMisbehavior: Child {0} with misbehavior factor {1} is too low to misbehave", child.LabelShort, misbehaviorLevel));
                return; // No behaviors executed
            }

            // Build a list of eligible misbehavior activities and their associated cooldowns
            // Using KeyValuePair<Action, int> where Key is the action and Value is the cooldown ticks
            List<KeyValuePair<Action, int>> eligibleBehaviors = new List<KeyValuePair<Action, int>>();

            if (misbehaviorLevel >= Level1Threshold)
            {
                eligibleBehaviors.Add(new KeyValuePair<Action, int>(() => AnnoyAdults(child), ShortCooldownTicks));
                eligibleBehaviors.Add(new KeyValuePair<Action, int>(() => SpyOnCouples(child), ShortCooldownTicks));
            }

            if (misbehaviorLevel >= Level2Threshold)
            {
                eligibleBehaviors.Add(new KeyValuePair<Action, int>(() => MisplaceItems(child), LongCooldownTicks));
                eligibleBehaviors.Add(new KeyValuePair<Action, int>(() => PlayTag(child), LongCooldownTicks)); // Tag is social but Tier 2
            }

            if (misbehaviorLevel >= Level3Threshold)
            {
                eligibleBehaviors.Add(new KeyValuePair<Action, int>(() => TrampleCrops(child), LongCooldownTicks));
                eligibleBehaviors.Add(new KeyValuePair<Action, int>(() => DestroyRandomProperty(child), LongCooldownTicks));
            }

            if (misbehaviorLevel >= Level4Threshold)
            {
                eligibleBehaviors.Add(new KeyValuePair<Action, int>(() => LightFire(child), LongCooldownTicks));
                eligibleBehaviors.Add(new KeyValuePair<Action, int>(() => PlayWithWeapon(child), LongCooldownTicks));
                eligibleBehaviors.Add(new KeyValuePair<Action, int>(() => LeakLocation(child), LongCooldownTicks));
            }

            // Select one behavior randomly from the eligible options
            if (eligibleBehaviors.Count > 0)
            {
                int selectedIndex = Rand.Range(0, eligibleBehaviors.Count);
                var selected = eligibleBehaviors[selectedIndex];

                SLog.Message(string.Format("[SocialInteractions] ExecuteMisbehavior: Child misbehavior behavior selected. Index: {0}, Total eligible: {1}",
                    selectedIndex, eligibleBehaviors.Count));

                // Execute the selected behavior
                selected.Key();

                // Apply cooldown based on the selected tier
                int cooldown = selected.Value;
                nextAllowedMisbehaviorTick[child] = Find.TickManager.TicksGame + cooldown;
            }
            else
            {
                SLog.Message(string.Format("[SocialInteractions] ExecuteMisbehavior: No eligible behaviors found for child misbehavior"));
            }
            // No fallback monologue - each behavior that needs dialogue handles it internally
        }

        private static void HandleMisbehaviorFailure(Pawn child, string subject, ThoughtDef thoughtDef)
        {
            SLog.Message(string.Format("[SocialInteractions] Misbehavior failed: Child {0} could not perform action. Subject: {1}", child.LabelShort, subject));
            
            if (child.needs != null && child.needs.mood != null && thoughtDef != null)
            {
                child.needs.mood.thoughts.memories.TryGainMemory(thoughtDef, null);
            }

            SocialInteractions.HandleMonologue(child, subject);
        }

        private static void AnnoyAdults(Pawn child)
        {
            if (child == null || child.Map == null)
            {
                SLog.Warning("[SocialInteractions] AnnoyAdults: child is null or map is null");
                return;
            }

            // Find nearby adults to annoy
            Pawn targetAdult = FindNearbyAnnoyableAdult(child);

            SLog.Message(string.Format("[SocialInteractions] AnnoyAdults: FindNearbyAnnoyableAdult returned: {0}", targetAdult != null ? targetAdult.LabelShort : "null"));

            if (targetAdult != null)
            {
                // Show warning message to player that child is about to pester an adult
                Messages.Message(string.Format("{0} (child) is about to pester {1} (adult) with annoying questions!", child.LabelShort, targetAdult.LabelShort),
                    new LookTargets(child, targetAdult), MessageTypeDefOf.CautionInput);

                // Log the action
                SLog.Message(string.Format("[SocialInteractions] AnnoyAdults: Child {0} is annoying adult {1}", child.LabelShort, targetAdult.LabelShort));

                // Create the ChildAnnoyAdult job for the child to follow and pester the adult
                Job annoyJob = JobMaker.MakeJob(SI_JobDefOf.ChildAnnoyAdult, targetAdult);
                bool jobTaken = child.jobs.TryTakeOrderedJob(annoyJob);
                SLog.Message(string.Format("[SocialInteractions] AnnoyAdults: Child {0} tried to take ChildAnnoyAdult job with {1}, success: {2}",
                    child.LabelShort, targetAdult.LabelShort, jobTaken));

                if (!jobTaken)
                {
                    SLog.Warning(string.Format("[SocialInteractions] AnnoyAdults: Child {0} failed to take ChildAnnoyAdult job with {1}",
                        child.LabelShort, targetAdult.LabelShort));
                }
            }
            else
            {
                SLog.Message(string.Format("[SocialInteractions] AnnoyAdults: Child {0} found no adults to annoy, becoming bored", child.LabelShort));

                // If no adult found, child becomes bored and expresses it
                // Apply boredom thought to the child
                if (child.needs != null && child.needs.mood != null)
                {
                    child.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildBoredom, null);
                    SLog.Message(string.Format("[SocialInteractions] AnnoyAdults: Applied ChildBoredom thought to {0}", child.LabelShort));
                }

                // Trigger LLM monologue about being bored with proper subject formatting
                string subject = "Is bored because there's nobody to play with";
                SLog.Message(string.Format("[SocialInteractions] AnnoyAdults: Triggering monologue for bored child: {0}", subject));
                SocialInteractions.HandleMonologue(child, subject);
            }
        }

        private static void ApplyNegativeMoodToAdult(Pawn adult, Pawn child)
        {
            if (adult == null || adult.needs == null || adult.needs.mood == null || child == null)
            {
                return;
            }

            // Add a thought to the adult about being annoyed by the child
            adult.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildAnnoyance, child);
        }

        private static void MisplaceItems(Pawn child)
        {
            if (child == null || child.Map == null || child.jobs == null)
            {
                SLog.Warning("[SocialInteractions] MisplaceItems: child is null, map is null, or jobs is null");
                return;
            }

            // Find valuable items in storage zones near the child that they can take
            Thing itemToTake = FindValuableItemInStorage(child, child.Map, 50); // Look in 50-cell radius

            if (itemToTake != null)
            {
                // Show warning message to player that child is about to misplace items
                Messages.Message(string.Format("{0} (child) is about to take {1} to play with!", child.LabelShort, itemToTake.Label),
                    new LookTargets(child, itemToTake), MessageTypeDefOf.CautionInput);

                // Find a random location for the child to go play with the item
                IntVec3 playLocation = FindRandomPlayLocation(child, child.Map);

                if (playLocation != IntVec3.Invalid)
                {
                    // Start the job for the child to take the item to the play location and play with it
                    Job playWithItemJob = JobMaker.MakeJob(SI_JobDefOf.ChildPlayWithItem, itemToTake, playLocation);
                    child.jobs.TryTakeOrderedJob(playWithItemJob);

                    SLog.Message(string.Format("[SocialInteractions] MisplaceItems: Child {0} is taking item {1} to play with at location {2}",
                        child.LabelShort, itemToTake.Label, playLocation));
                }
                else
                {
                    SLog.Message(string.Format("[SocialInteractions] MisplaceItems: Child {0} found item {1} but no suitable play location",
                        child.LabelShort, itemToTake.Label));
                }
            }
            else
            {
                SLog.Message(string.Format("[SocialInteractions] MisplaceItems: Child {0} found no valuable items to take", child.LabelShort));
            }
        }

        private static void SpyOnCouples(Pawn child)
        {
            if (child == null || child.Map == null)
            {
                SLog.Warning("[SocialInteractions] SpyOnCouples: child is null or map is null");
                return;
            }

            // Find a couple engaging in Lovin'
            Pawn target = FindCoupleLovin(child);

            if (target != null)
            {
                // Show warning message
                Messages.Message(string.Format("{0} (child) is going to spy on {1}!", child.LabelShort, target.LabelShort),
                    new LookTargets(child, target), MessageTypeDefOf.CautionInput);

                SLog.Message(string.Format("[SocialInteractions] SpyOnCouples: Child {0} is spying on {1}", child.LabelShort, target.LabelShort));

                // Find a spot to watch from
                IntVec3 watchSpot = CellFinder.RandomClosewalkCellNear(target.Position, child.Map, 4, (IntVec3 c) => 
                    c.Standable(child.Map) && 
                    !c.IsForbidden(child) && 
                    GenSight.LineOfSight(c, target.Position, child.Map) &&
                    c.DistanceTo(target.Position) >= 2f); // Don't get too close

                if (watchSpot != IntVec3.Invalid)
                {
                    Job spyJob = JobMaker.MakeJob(SI_JobDefOf.ChildSpyOnLovin, target, watchSpot);
                    child.jobs.TryTakeOrderedJob(spyJob);
                }
                else
                {
                    SLog.Message("[SocialInteractions] SpyOnCouples: Could not find a good watch spot.");
                }
            }
        }

        private static Pawn FindCoupleLovin(Pawn child)
        {
            if (child == null || child.Map == null) return null;

            List<Pawn> potentialTargets = new List<Pawn>();

            foreach (Pawn p in child.Map.mapPawns.FreeColonistsSpawned)
            {
                if (p == child) continue;
                
                // Check distance
                if (p.Position.DistanceTo(child.Position) > 30f) continue;

                // Check if doing Lovin'
                if (p.CurJob != null && (p.CurJob.def == JobDefOf.Lovin || p.CurJob.def == SI_JobDefOf.DateLovin))
                {
                    potentialTargets.Add(p);
                }
            }

            if (potentialTargets.Count > 0)
            {
                return potentialTargets.RandomElement();
            }

            return null;
        }

        private static void PlayTag(Pawn child)
        {
            if (child == null || child.Map == null)
            {
                SLog.Warning("[SocialInteractions] PlayTag: child is null or map is null");
                return;
            }

            // Find another child to play with
            Pawn targetChild = FindTagTarget(child);

            if (targetChild != null)
            {
                // Show warning message
                Messages.Message(string.Format("{0} (child) wants to play tag with {1}!", child.LabelShort, targetChild.LabelShort),
                    new LookTargets(child, targetChild), MessageTypeDefOf.NeutralEvent);

                SLog.Message(string.Format("[SocialInteractions] PlayTag: Child {0} is inviting {1} to play tag", child.LabelShort, targetChild.LabelShort));

                // Create invite job
                Job inviteJob = JobMaker.MakeJob(SI_JobDefOf.SI_InviteToPlayTag, targetChild);
                child.jobs.TryTakeOrderedJob(inviteJob);
            }
            else
            {
                SLog.Message(string.Format("[SocialInteractions] PlayTag: Child {0} found no one to play tag with", child.LabelShort));
                
                // Fallback to boredom if no one to play with
                if (child.needs != null && child.needs.mood != null)
                {
                    child.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildBoredom, null);
                }
                string subject = "Is bored because there's nobody to play tag with";
                SocialInteractions.HandleMonologue(child, subject);
            }
        }

        private static Pawn FindTagTarget(Pawn child)
        {
            if (child == null || child.Map == null) return null;

            List<Pawn> potentialTargets = new List<Pawn>();

            foreach (Pawn p in child.Map.mapPawns.FreeColonistsSpawned)
            {
                if (p == child) continue;
                
                // Must be a child
                if (!IsChild(p)) continue;

                // Must be awake and capable
                if (!p.Awake() || p.Downed || p.Dead || p.InMentalState) continue;

                // Check distance (within 40 cells)
                if (p.Position.DistanceTo(child.Position) > 40f) continue;

                // Check if busy with something critical? 
                // For now, just check if they are not drafted or doing something emergency
                if (p.Drafted) continue;

                potentialTargets.Add(p);
            }

            if (potentialTargets.Count > 0)
            {
                // Prefer friends?
                return potentialTargets.RandomElement();
            }

            return null;
        }

        /// <summary>
        /// Finds a valuable item in storage zones near the child
        /// </summary>
        private static Thing FindValuableItemInStorage(Pawn child, Map map, int radius)
        {
            List<Thing> potentialItems = new List<Thing>();

            foreach (IntVec3 c in GenRadial.RadialCellsAround(child.Position, radius, true))
            {
                if (!c.InBounds(map)) continue;

                // Check if this cell is part of any storage (Stockpile zone or Building_Storage like shelves)
                SlotGroup slotGroup = map.haulDestinationManager.SlotGroupAt(c);
                if (slotGroup != null)
                {
                    foreach (Thing thing in c.GetThingList(map))
                    {
                        // Only consider items that can be hauled and have some value
                        if (thing.def.EverHaulable &&
                            !thing.Position.Fogged(map) &&
                            thing.Spawned &&
                            thing.MarketValue > 10f) // Only items worth more than 10 silver
                        {
                            // Consider items that are in storage as "valuable"
                            potentialItems.Add(thing);
                        }
                    }
                }
            }

            // If we found valuable items, pick one randomly
            if (potentialItems.Count > 0)
            {
                // Sort by value to make more valuable items more likely to be chosen
                potentialItems.Sort((a, b) => b.MarketValue.CompareTo(a.MarketValue));

                // Weighted selection - more valuable items have higher chance
                float totalValue = 0f;
                foreach (Thing item in potentialItems)
                {
                    totalValue += item.MarketValue;
                }

                if (totalValue > 0)
                {
                    float randomValue = Rand.Value * totalValue;
                    float currentValue = 0f;

                    foreach (Thing item in potentialItems)
                    {
                        currentValue += item.MarketValue;
                        if (randomValue <= currentValue)
                        {
                            return item;
                        }
                    }
                }

                // Fallback to random selection
                return potentialItems[Rand.Range(0, potentialItems.Count)];
            }

            return null;
        }

        /// <summary>
        /// Finds a random suitable location for the child to play
        /// </summary>
        private static IntVec3 FindRandomPlayLocation(Pawn child, Map map)
        {
            List<IntVec3> possibleCells = new List<IntVec3>();

            foreach (IntVec3 c in GenRadial.RadialCellsAround(child.Position, 20, true)) // Look in 20 cell radius
            {
                if (c.InBounds(map) && c.Walkable(map) && !c.Fogged(map))
                {
                    // Avoid building areas and prefer open spaces
                    if (c.GetEdifice(map) == null && c.GetFirstItem(map) == null)
                    {
                        possibleCells.Add(c);
                    }
                }
            }

            if (possibleCells.Count > 0)
            {
                return possibleCells[Rand.Range(0, possibleCells.Count)];
            }

            return IntVec3.Invalid; // No suitable location found
        }

        private static IntVec3 FindInappropriateStorageLocation(Pawn child, Map map, Thing item)
        {
            // Look for inappropriate storage zones like dumping areas or random locations
            // Try to find a trash zone or unassigned area to dump items

            // Look in a larger radius for potential inappropriate locations
            foreach (IntVec3 c in GenRadial.RadialCellsAround(child.Position, 20, true))
            {
                if (!c.InBounds(map)) continue;

                // Check if this is an unassigned area that's not meant for storage
                if (c.GetEdifice(map) == null) // No building blocking placement
                {
                    // Check if it's a dumping cell (like a waste area)
                    Zone zone = map.zoneManager.ZoneAt(c);
                    if (zone != null && zone is Zone_Stockpile)
                    {
                        // If it's a stockpile with inappropriate categories for this item, use it
                        Zone_Stockpile stockpile = (Zone_Stockpile)zone;
                        if (!stockpile.Accepts(item))
                        {
                            return c;
                        }
                    }
                    else if (zone != null && zone.label.Contains("Dumping"))
                    {
                        // Found a dumping zone
                        return c;
                    }
                }
            }

            // If no specific bad zones found, return invalid to indicate random placement
            return IntVec3.Invalid;
        }

        private static bool TrampleCrops(Pawn child)
        {
            if (child == null || child.Map == null)
            {
                return false;
            }

            // Find a growing zone or area with crops to trample
            IntVec3 trampleArea = FindGrowingAreaWithCrops(child);

            if (trampleArea != IntVec3.Invalid)
            {
                // Show warning message
                Messages.Message(string.Format("{0} (child) is about to trample some crops!", child.LabelShort),
                    new LookTargets(child), MessageTypeDefOf.CautionInput);

                // Create the job for the child to go to the area and trample crops there
                Job trampleJob = JobMaker.MakeJob(SI_JobDefOf.ChildTrampleCrops, trampleArea);
                bool jobTaken = child.jobs.TryTakeOrderedJob(trampleJob);

                if (jobTaken)
                {
                    SLog.Message(string.Format("[SocialInteractions] TrampleCrops: Child {0} is going to trample crops in area {1}",
                        child.LabelShort, trampleArea));

                    return true;
                }
                else
                {
                    SLog.Message(string.Format("[SocialInteractions] TrampleCrops: Child {0} failed to take trample crops job",
                        child.LabelShort));
                }
            }

            HandleMisbehaviorFailure(child, "wanted to trample some crops but couldn't find any", ChildThoughtDefOf.ChildMischievous);
            return false;
        }

        private static IntVec3 FindGrowingAreaWithCrops(Pawn child)
        {
            if (child == null || child.Map == null)
            {
                return IntVec3.Invalid;
            }

            int searchRadius = 25;
            IntVec3 bestArea = IntVec3.Invalid;
            int closestDistance = int.MaxValue;

            foreach (IntVec3 c in GenRadial.RadialCellsAround(child.Position, searchRadius, true))
            {
                if (!c.InBounds(child.Map)) continue;

                // Check if this cell is in a growing zone OR has a plant grower building (hydroponics)
                bool isGrowingArea = false;
                Zone zone = child.Map.zoneManager.ZoneAt(c);
                if (zone is Zone_Growing)
                {
                    isGrowingArea = true;
                }
                else
                {
                    Building edifice = c.GetEdifice(child.Map);
                    if (edifice is Building_PlantGrower)
                    {
                        isGrowingArea = true;
                    }
                }

                if (isGrowingArea)
                {
                    // Count the number of mature crops in this area
                    int cropCount = CountMatureCropsInArea(c, child.Map, 5); // Check 5-cell radius around this point

                    if (cropCount > 0)
                    {
                        int distance = (int)(c - child.Position).LengthHorizontal;
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            bestArea = c;
                        }
                    }
                }
            }

            // If we didn't find a specific growing zone/building, look for any area with mature crops
            if (bestArea == IntVec3.Invalid)
            {
                foreach (IntVec3 c in GenRadial.RadialCellsAround(child.Position, searchRadius, true))
                {
                    if (!c.InBounds(child.Map)) continue;

                    // Count mature crops in this area (even if not in a zone)
                    int cropCount = CountMatureCropsInArea(c, child.Map, 5);

                    if (cropCount > 0)
                    {
                        int distance = (int)(c - child.Position).LengthHorizontal;
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            bestArea = c;
                        }
                    }
                }
            }

            return bestArea;
        }

        private static int CountMatureCropsInArea(IntVec3 center, Map map, int radius)
        {
            int count = 0;

            foreach (IntVec3 c in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!c.InBounds(map)) continue;

                List<Thing> things = c.GetThingList(map);
                foreach (Thing thing in things)
                {
                    Plant plant = thing as Plant;
                    if (plant != null && !plant.Destroyed && plant.Spawned)
                    {
                        // Check if it's a crop (not wild plants)
                        if (plant.def.plant != null && plant.def.plant.Sowable && plant.Growth >= 0.1f)
                        {
                            count++;
                        }
                    }
                }
            }

            return count;
        }

        private static bool DestroyRandomProperty(Pawn child)
        {
            if (child == null || child.Map == null)
            {
                return false;
            }

            // Find buildings with breakdown component (workbenches, furniture, etc.)
            int searchRadius = 20;
            List<Thing> breakableBuildings = new List<Thing>();

            foreach (IntVec3 c in GenRadial.RadialCellsAround(child.Position, searchRadius, true))
            {
                if (!c.InBounds(child.Map)) continue;

                // Check for buildings
                Thing edifice = c.GetEdifice(child.Map);
                if (edifice != null)
                {
                    // Check if it's a building with CompBreakdownable
                    CompBreakdownable breakdownComp = edifice.TryGetComp<CompBreakdownable>();
                    if (breakdownComp != null && !breakdownComp.BrokenDown)
                    {
                        // Exclude critical infrastructure
                        if (edifice.def.defName != "Door" && edifice.def.defName != "Autodoor" &&
                            !edifice.def.defName.Contains("Wall") && 
                            !edifice.def.defName.Contains("Vent"))
                        {
                            breakableBuildings.Add(edifice);
                        }
                    }
                }
            }

            if (breakableBuildings.Count > 0)
            {
                // Randomly select a building to break
                Thing buildingToBreak = breakableBuildings[Rand.Range(0, breakableBuildings.Count)];

                // Show warning message to player that child is about to destroy property
                Messages.Message(string.Format("{0} (child) is about to smash {1}!", child.LabelShort, buildingToBreak.Label),
                    new LookTargets(child, buildingToBreak), MessageTypeDefOf.CautionInput);

                // Create job for child to break the building
                Job breakJob = JobMaker.MakeJob(SI_JobDefOf.ChildBreakBuilding, buildingToBreak);
                bool jobTaken = child.jobs.TryTakeOrderedJob(breakJob);

                if (jobTaken)
                {
                    SLog.Message(string.Format("[SocialInteractions] DestroyRandomProperty: Child {0} is going to break {1}",
                        child.LabelShort, buildingToBreak.Label));
                    return true;
                }
            }

            HandleMisbehaviorFailure(child, "wanted to smash something but couldn't find a good target", ChildThoughtDefOf.ChildMischievous);
            return false;
        }

        private static bool LightFire(Pawn child)
        {
            if (child == null || child.Map == null)
            {
                return false;
            }

            // Find a flammable target to ignite
            Thing flammableTarget = FindFlammableTarget(child);

            if (flammableTarget != null)
            {
                // Show warning message to player that child is about to light a fire
                Messages.Message(string.Format("{0} (child) is about to light a fire on {1}!", child.LabelShort, flammableTarget.Label),
                    new LookTargets(child, flammableTarget), MessageTypeDefOf.ThreatBig);

                // Create the job for the child to go to the flammable target and light it
                Job lightFireJob = JobMaker.MakeJob(SI_JobDefOf.ChildLightFire, flammableTarget);
                bool jobTaken = child.jobs.TryTakeOrderedJob(lightFireJob);

                if (jobTaken)
                {
                    SLog.Message(string.Format("[SocialInteractions] LightFire: Child {0} is going to light a fire on {1}",
                        child.LabelShort, flammableTarget.Label));

                    return true;
                }
                else
                {
                    SLog.Message(string.Format("[SocialInteractions] LightFire: Child {0} failed to take light fire job",
                        child.LabelShort));
                }
            }

            HandleMisbehaviorFailure(child, "wanted to light a fire but couldn't find anything flammable", ChildThoughtDefOf.ChildRiskTaking);
            return false;
        }

        private static Thing FindFlammableTarget(Pawn child)
        {
            if (child == null || child.Map == null)
            {
                return null;
            }

            int searchRadius = 20;

            foreach (IntVec3 c in GenRadial.RadialCellsAround(child.Position, searchRadius, true))
            {
                if (!c.InBounds(child.Map)) continue;

                // Get all things at this cell
                List<Thing> things = c.GetThingList(child.Map);
                foreach (Thing thing in things)
                {
                    // Check if the thing is flammable, not burning, and appropriate for this
                    if ((thing.def.category == ThingCategory.Building ||
                         thing.def.category == ThingCategory.Item ||
                         thing.def.category == ThingCategory.Plant) &&
                        thing.FlammableNow &&
                        !thing.IsBurning() &&
                        !thing.Position.Fogged(child.Map) && // Make sure it's not fogged
                        thing.Spawned)
                    {
                        // Prefer flammable items first, then buildings, then plants
                        if (thing.def.category == ThingCategory.Item)
                        {
                            // Additional check: make sure it's a valuable/flammable item
                            if (thing.def.BaseMarketValue > 0)
                            {
                                return thing;
                            }
                        }
                        else if (thing.def.category == ThingCategory.Building ||
                                 thing.def.category == ThingCategory.Plant)
                        {
                            return thing; // Accept buildings and plants too
                        }
                    }
                }
            }

            return null; // No suitable flammable target found
        }

        private static IntVec3 FindSafeFireLocation(Pawn child)
        {
            if (child.Map == null) return IntVec3.Invalid;

            // Look for locations that are flammable but not critical infrastructure
            int searchRadius = 20;

            foreach (IntVec3 c in GenRadial.RadialCellsAround(child.Position, searchRadius, true))
            {
                if (!c.InBounds(child.Map)) continue;

                // Find a location that is walkable and doesn't contain critical objects
                if (c.Walkable(child.Map) && !c.Fogged(child.Map))
                {
                    // Check if there are flammable things at the location
                    List<Thing> things = c.GetThingList(child.Map);
                    bool hasFlammableThing = false;

                    foreach (Thing thing in things)
                    {
                        if (thing.def.category == ThingCategory.Item &&
                            (thing.def.defName == "WoodLog" || thing.def.defName.Contains("Hay") || thing.def.defName.Contains("Plant") || thing.def.IsCorpse) &&
                            thing.def.BaseMarketValue > 0) // Make sure it's a valuable item
                        {
                            hasFlammableThing = true;
                            break;
                        }
                    }

                    // If we found a flammable thing or an open area that could support a fire
                    if (hasFlammableThing || c.GetTerrain(child.Map).burnedDef == null)
                    {
                        // Ensure nothing critical is at this location
                        if (c.GetEdifice(child.Map) == null || c.GetEdifice(child.Map).def.defName.Contains("Fence")) // Allow placing fire near low-priority structures
                        {
                            return c;
                        }
                    }
                }
            }

            // If no specific location with flammable items found, return an arbitrary open space
            foreach (IntVec3 c in GenRadial.RadialCellsAround(child.Position, searchRadius, true))
            {
                if (c.InBounds(child.Map) &&
                    c.Walkable(child.Map) &&
                    !c.Fogged(child.Map) &&
                    c.GetEdifice(child.Map) == null)
                {
                    // Avoid critical areas (near medical beds, food storage, etc.)
                    if (!IsCriticalArea(child.Map, c))
                    {
                        return c;
                    }
                }
            }

            return IntVec3.Invalid; // No safe location found
        }

        private static bool IsCriticalArea(Map map, IntVec3 c)
        {
            // Check if the cell is near critical infrastructure
            foreach (IntVec3 checkCell in GenRadial.RadialCellsAround(c, 5, true))
            {
                if (!checkCell.InBounds(map)) continue;

                // Check for critical buildings
                Building building = checkCell.GetEdifice(map) as Building;
                if (building != null)
                {
                    // Check for critical building types
                    if (building.def.defName.Contains("Bed") ||
                        building.def.defName.Contains("Hospital") ||
                        building.def.defName.Contains("Shelf") ||
                        building.def.defName.Contains("Cooler") ||
                        building.def.defName.Contains("CryptosleepCasket"))
                    {
                        return true; // This is a critical area
                    }
                }
            }

            return false;
        }

        private static bool PlayWithWeapon(Pawn child)
        {
            // For now, we'll implement additional dangerous behaviors like:
            // - Attempting to use weapons
            // - Attempting to go to dangerous zones

            if (child == null || child.Map == null)
            {
                return false;
            }

            Thing weaponToUse = null;

            // 1. Check if child already has a ranged weapon equipped
            if (child.equipment != null && child.equipment.Primary != null && child.equipment.Primary.def.IsRangedWeapon)
            {
                weaponToUse = child.equipment.Primary;
                SLog.Message(string.Format("[SocialInteractions] PlayWithWeapon: Child {0} already has ranged weapon {1} equipped.", child.LabelShort, weaponToUse.Label));
            }
            else
            {
                // 2. Find a ranged weapon on the map
                IntVec3 weaponLocation = FindNearbyRangedWeapon(child);

                if (weaponLocation != IntVec3.Invalid)
                {
                    List<Thing> things = weaponLocation.GetThingList(child.Map);
                    foreach (Thing thing in things)
                    {
                        if (thing.def.IsRangedWeapon && thing.def.equipmentType == EquipmentType.Primary)
                        {
                            weaponToUse = thing;
                            break;
                        }
                    }
                }
            }

            if (weaponToUse != null)
            {
                // Show warning message to player that child is about to play with a weapon unsafely
                Messages.Message(string.Format("{0} (child) is about to play with {1} unsafely!", child.LabelShort, weaponToUse.Label),
                    new LookTargets(child, weaponToUse), MessageTypeDefOf.ThreatBig);

                // Create the job for the child to go play with the weapon unsafely
                Job weaponPlayJob = JobMaker.MakeJob(SI_JobDefOf.ChildPlayWithWeapon, weaponToUse);
                bool jobTaken = child.jobs.TryTakeOrderedJob(weaponPlayJob);

                if (jobTaken)
                {
                    SLog.Message(string.Format("[SocialInteractions] PlayWithWeapon: Child {0} is going to play with weapon {1}",
                        child.LabelShort, weaponToUse.Label));

                    return true;
                }
                else
                {
                    SLog.Message(string.Format("[SocialInteractions] PlayWithWeapon: Child {0} failed to take weapon play job",
                        child.LabelShort));
                }
            }

            HandleMisbehaviorFailure(child, "wanted to play with a weapon but couldn't find one", ChildThoughtDefOf.ChildRiskTaking);
            return false;
        }

        private static IntVec3 FindNearbyRangedWeapon(Pawn child)
        {
            if (child.Map == null) return IntVec3.Invalid;

            // Find weapons in a radius around the child
            int searchRadius = 35;

            foreach (IntVec3 c in GenRadial.RadialCellsAround(child.Position, searchRadius, true))
            {
                if (!c.InBounds(child.Map)) continue;

                List<Thing> things = c.GetThingList(child.Map);
                foreach (Thing thing in things)
                {
                    if (thing.def.IsRangedWeapon && thing.def.equipmentType == EquipmentType.Primary) // It's a ranged weapon
                    {
                        return c; // Found a weapon
                    }
                }
            }

            return IntVec3.Invalid;
        }

        private static IntVec3 FindNearbyWeapon(Pawn child)
        {
            if (child.Map == null) return IntVec3.Invalid;

            // Find weapons in a radius around the child
            int searchRadius = 15;

            foreach (IntVec3 c in GenRadial.RadialCellsAround(child.Position, searchRadius, true))
            {
                if (!c.InBounds(child.Map)) continue;

                List<Thing> things = c.GetThingList(child.Map);
                foreach (Thing thing in things)
                {
                    if (thing.def.IsWeapon && thing.def.equipmentType == EquipmentType.Primary) // It's a weapon
                    {
                        return c; // Found a weapon
                    }
                }
            }

            return IntVec3.Invalid; // No weapon found
        }

        private static string GetMisbehaviorLevelDescription(float misbehaviorLevel)
        {
            if (misbehaviorLevel < Level1Threshold) return "no";
            else if (misbehaviorLevel < Level2Threshold) return "level 1";
            else if (misbehaviorLevel < Level3Threshold) return "level 2";
            else if (misbehaviorLevel < Level4Threshold) return "level 3";
            else return "level 4";
        }

        private static void TriggerChildMonologue(Pawn child, float misbehaviorLevel)
        {
            string subject = GetMisbehaviorSubject(child, misbehaviorLevel);
            SocialInteractions.HandleMonologue(child, subject);
        }

        private static string GetMisbehaviorSubject(Pawn child, float misbehaviorLevel)
        {
            if (misbehaviorLevel < Level2Threshold)
            {
                return "acted up for attention";
            }
            else if (misbehaviorLevel < Level3Threshold)
            {
                return "misplaced something";
            }
            else if (misbehaviorLevel < Level4Threshold)
            {
                return "damaged something";
            }
            else
            {
                return "did something dangerous";
            }
        }

        private static string GetRandomAnnoyanceText()
        {
            string[] annoyanceTexts = {
                "Are we there yet?",
                "I'm boooored!",
                "Why do I have to?",
                "Are you sure?",
                "I know something you don't know!",
                "Make me!",
                "It's not fair!",
                "I didn't do anything!",
                "Why not?",
                "Because I said so!",
                "You're not the boss of me!",
                "I hate you!",
                "I'm telling mom/dad!",
                "You started it!",
                "It's not my fault!"
            };

            return annoyanceTexts[Rand.Range(0, annoyanceTexts.Length)];
        }

        private static Pawn FindNearbyAnnoyableAdult(Pawn child)
        {
            if (child == null || child.Map == null)
            {
                return null;
            }

            // Find colonists in an XY region around the child
            // Using GenRadial.RadialCellsAround to get cells in a circular area around the child
            List<Pawn> candidates = new List<Pawn>();

            // Define the region around the child to search for adults
            int searchRadius = 20;
            IntVec3 centerPos = child.Position;

            // Search for nearby colonists within a circular area around the child
            // Using a more efficient approach with GenRadial.RadialCellsAround
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(centerPos, searchRadius, true))
            {
                if (!cell.InBounds(child.Map))
                {
                    continue;
                }

                // Get pawns at this cell
                List<Thing> things = cell.GetThingList(child.Map);
                foreach (Thing thing in things)
                {
                    Pawn pawn = thing as Pawn;
                    if (pawn != null &&
                        pawn.Faction == child.Faction &&
                        !pawn.Dead &&
                        pawn.Spawned &&
                        pawn != child &&
                        IsAdult(pawn))
                    {
                        // Only consider adults who are idle or doing non-critical work
                        if (pawn.CurJob == null ||
                            pawn.CurJob.def == JobDefOf.Wait ||
                            pawn.CurJob.def == JobDefOf.Wait_Wander ||
                            pawn.CurJob.def == JobDefOf.GotoWander)
                        {
                            candidates.Add(pawn);
                        }
                    }
                }
            }

            if (candidates.Count > 0)
            {
                return candidates[Rand.Range(0, candidates.Count)];
            }

            return null;
        }

        private static List<Pawn> GetParentsAndGuardians(Pawn child)
        {
            List<Pawn> parents = new List<Pawn>();
            
            if (child.relations != null)
            {
                // Get direct parents (biological, adoptive, etc.)
                foreach (Pawn otherPawn in child.Map.mapPawns.AllPawns)
                {
                    if (otherPawn != null && !otherPawn.Dead && otherPawn.Spawned && otherPawn.RaceProps.Humanlike)
                    {
                        if (child.relations.DirectRelationExists(PawnRelationDefOf.Parent, otherPawn))
                        {
                            parents.Add(otherPawn);
                        }
                        else
                        {
                            // Check if they are family by blood
                            var allRelations = child.GetRelations(otherPawn);
                            foreach (var relation in allRelations)
                            {
                                if (relation == PawnRelationDefOf.Parent || relation == PawnRelationDefOf.Child ||
                                    relation == PawnRelationDefOf.Sibling || relation == PawnRelationDefOf.Grandchild ||
                                    relation == PawnRelationDefOf.Grandparent)
                                {
                                    parents.Add(otherPawn);
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return parents;
        }

        private static bool HasParentBeenAbsentForLongTime(Pawn parent, Pawn child)
        {
            // This is a simplified check - in a real implementation, we'd need to track 
            // actual interaction times between parent and child
            return false; // Placeholder implementation
        }

        private static float GetTraitInfluenceOnMisbehavior(Pawn child)
        {
            float traitInfluence = 0f;
            
            if (child.story != null && child.story.traits != null)
            {
                foreach (Trait trait in child.story.traits.allTraits)
                {
                    if (trait != null)
                    {
                        switch (trait.def.defName)
                        {
                            case "Rebellious":
                                traitInfluence += 0.2f;
                                break;
                            case "Nervous":
                                traitInfluence += 0.1f;
                                break;
                            case "Wimp":
                                traitInfluence += 0.1f;
                                break;
                            case "Bloodlust":
                                traitInfluence += 0.15f;
                                break;
                            case "Psychopath":
                                traitInfluence += 0.3f;
                                break;
                            case "Kind":
                                traitInfluence -= 0.2f;
                                break;
                            default:
                                // Other traits can be added as needed
                                break;
                        }
                    }
                }
            }
            
            return Mathf.Clamp(traitInfluence, -0.5f, 0.5f);
        }

        private static bool LeakLocation(Pawn child)
        {
            if (child == null || child.Map == null) return false;

            Thing commsConsole = FindCommsConsole(child);
            if (commsConsole != null)
            {
                // Show warning message
                Messages.Message(string.Format("{0} (child) is playing with the radio!", child.LabelShort),
                    new LookTargets(child, commsConsole), MessageTypeDefOf.ThreatBig);

                Job leakJob = JobMaker.MakeJob(SI_JobDefOf.ChildPlayWithRadio, commsConsole);
                bool jobTaken = child.jobs.TryTakeOrderedJob(leakJob);
                
                if (jobTaken)
                {
                     SLog.Message(string.Format("[SocialInteractions] LeakLocation: Child {0} might leak location via {1}", child.LabelShort, commsConsole.Label));
                     return true;
                }
            }
            HandleMisbehaviorFailure(child, "wanted to play with the radio but couldn't find a comms console", ChildThoughtDefOf.ChildRiskTaking);
            return false;
        }

        private static Thing FindCommsConsole(Pawn child)
        {
            if (child.Map == null) return null;

            return GenClosest.ClosestThingReachable(child.Position, child.Map, 
                ThingRequest.ForDef(ThingDefOf.CommsConsole), 
                PathEndMode.InteractionCell, 
                TraverseParms.For(child), 
                9999f, 
                (Thing t) => {
                    Building_CommsConsole comms = t as Building_CommsConsole;
                    return comms != null && comms.CanUseCommsNow && child.CanReserve(t);
                });
        }

        public static bool IsChild(Pawn pawn)
        {
            if (pawn == null || pawn.ageTracker == null)
            {
                return false;
            }

            // Check if pawn is humanlike and flesh (excludes mechanoids, androids, droids)
            if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike || !pawn.RaceProps.IsFlesh)
            {
                return false;
            }

            // Exclude specific droid flesh types or races that pretend to be flesh
            // "Asimov_Automaton" is used by Outer Rim droids but claimed to be flesh for compatibility
            if (pawn.RaceProps.FleshType != null)
            {
                string fleshName = pawn.RaceProps.FleshType.defName;
                if (fleshName.Contains("Asimov") || 
                    fleshName.Contains("Automaton") || 
                    fleshName.Contains("Droid") || 
                    fleshName.Contains("Robot"))
                {
                    return false;
                }
            }

            // Using the standard RimWorld age classification
            // Only process pawns that are between ChildMinAge-ChildAgeLimit (3-12 years old, exclude toddlers under 3)
            int age = pawn.ageTracker.AgeBiologicalYears;
            return age >= ChildMinAge && age < ChildAgeLimit;
        }

        private static bool IsAdult(Pawn pawn)
        {
            if (pawn == null || pawn.ageTracker == null)
            {
                return false;
            }

            return pawn.ageTracker.AgeBiologicalYears >= ChildAgeLimit;
        }

        /// <summary>
        /// Cleanup method to be called periodically (e.g. with a MapComponent)
        /// </summary>
        public static void Cleanup()
        {
            // Remove references to pawns that are no longer valid
            List<Pawn> toRemove = new List<Pawn>();
            
            foreach (var kvp in nextAllowedMisbehaviorTick)
            {
                if (kvp.Key == null || kvp.Key.Dead || !kvp.Key.Spawned)
                {
                    toRemove.Add(kvp.Key);
                }
            }
            
            foreach (Pawn pawn in toRemove)
            {
                nextAllowedMisbehaviorTick.Remove(pawn);
            }
        }
    }
}