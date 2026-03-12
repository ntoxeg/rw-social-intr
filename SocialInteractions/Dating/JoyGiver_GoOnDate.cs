using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using System.Collections.Generic;

namespace SocialInteractions
{
    /// <summary>
    /// Job giver that makes pawns go on dates. Handles all the complex logic for finding suitable partners
    /// and determining if a date is appropriate based on pawn relationships, mood, and social dynamics.
    /// </summary>
    public class JoyGiver_GoOnDate : JoyGiver
    {
        private static Dictionary<Pawn, int> lastAttemptTick = new Dictionary<Pawn, int>();

        public override Job TryGiveJob(Pawn pawn)
        {
            // Basic validity checks
            if (pawn == null || !SocialInteractions.Settings.enableDatingFeature || 
                DatingManager.IsOnDateCooldown(pawn)) 
                return null;

            // Cooldown check to prevent spam
            int lastTick;
            if (lastAttemptTick.TryGetValue(pawn, out lastTick) && 
                Find.TickManager.TicksGame - lastTick < SocialInteractions.Settings.goOnDateCooldownTicks)
                return null;
            
            lastAttemptTick[pawn] = Find.TickManager.TicksGame;

            // Check if pawn is already on a date or has a date job
            if (DatingManager.IsOnDate(pawn) || 
                (pawn.jobs != null && pawn.jobs.curJob != null && pawn.jobs.curJob.def == SI_JobDefOf.GoOnDate))
                return null;

            // Check if pawn has joy needs
            if (pawn.needs == null || pawn.needs.joy == null)
                return null;

            // Check joy threshold and various conditions that prevent dating
            if (pawn.needs.joy.CurLevelPercentage > SocialInteractions.Settings.joyThresholdForDate ||
                !pawn.Awake() || pawn.InBed() || 
                (pawn.CurJob != null && pawn.CurJob.def == JobDefOf.LayDown) || 
                pawn.Drafted)
                return null;

            // Find a suitable partner and validate conditions
            Pawn partner = FindPartnerFor(pawn);
            if (partner == null || !SocialInteractionUtility.CanInitiateInteraction(pawn) || 
                !SocialInteractionUtility.CanReceiveInteraction(partner) ||
                !pawn.CanReserve(partner) || !partner.CanReserve(pawn) ||
                DatingManager.IsOnDate(pawn) || DatingManager.IsOnDate(partner) ||
                IsPartnerBeingTargeted(partner))
                return null;
            
            return JobMaker.MakeJob(SI_JobDefOf.GoOnDate, partner);
        }

        /// <summary>
        /// Finds a suitable partner for a date based on relationship status, opinion, and other factors
        /// </summary>
        private Pawn FindPartnerFor(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || pawn.Map.mapPawns == null) 
                return null;
            
            List<Pawn> allPawns = pawn.Map.mapPawns.AllPawnsSpawned.Where(p => 
                p != null && p != pawn && p.Faction != null && p.Faction.IsPlayer && 
                p.IsColonist && !p.IsPrisoner && !p.Downed && 
                p.Awake() && !p.InBed() && !p.Drafted && 
                !p.InMentalState && !DatingManager.IsOnDate(p) && 
                !DatingManager.IsOnDateCooldown(p) &&
                pawn.CanReserveAndReach(p, PathEndMode.InteractionCell, Danger.None)).ToList();
            
            List<KeyValuePair<Pawn, float>> potentialPartners = new List<KeyValuePair<Pawn, float>>();
            
            foreach (Pawn p in allPawns)
            {
                float score = CalculateDateAttractiveness(pawn, p);
                if (score > 0) 
                    potentialPartners.Add(new KeyValuePair<Pawn, float>(p, score));
            }

            if (potentialPartners.Count > 0)
            {
                potentialPartners.Sort((x, y) => y.Value.CompareTo(x.Value));
                Pawn selected = SelectPartnerWeighted(pawn, potentialPartners);
                if (selected != null) 
                    return selected;
            }
            
            return null;
        }
        
        /// <summary>
        /// Calculates a "date attractiveness" score for a potential partner.
        /// Higher scores mean the pawn is more likely to be chosen for a date.
        /// Scores are based on relationships, opinions, and potential cheating considerations.
        /// </summary>
        private float CalculateDateAttractiveness(Pawn initiator, Pawn partner)
        {
            bool isLover = initiator.relations.DirectRelationExists(PawnRelationDefOf.Lover, partner);
            bool isFiance = initiator.relations.DirectRelationExists(PawnRelationDefOf.Fiance, partner);
            bool isSpouse = initiator.relations.DirectRelationExists(PawnRelationDefOf.Spouse, partner);
            
            // If they are already in a romantic relationship, use relationship-based scoring
            if (isLover || isFiance || isSpouse)
            {
                float relationshipScore = isSpouse ? SocialInteractions.Settings.spouseDateWeight : 
                                          isFiance ? SocialInteractions.Settings.fianceDateWeight : 
                                                     SocialInteractions.Settings.loverDateWeight;
                int opinion = initiator.relations.OpinionOf(partner);
                float opinionAdjustment = System.Math.Max(-2f, System.Math.Min(2f, opinion / SocialInteractions.Settings.opinionAdjustmentFactor));
                return relationshipScore + opinionAdjustment;
            }
            
            // For non-related pawns, base the score primarily on opinion
            int baseOpinion = initiator.relations.OpinionOf(partner);
            if (baseOpinion <= 10) 
                return 0f;
            
            float score = baseOpinion * SocialInteractions.Settings.nonRelatedPartnerWeightFactor;
            
            // Check if the initiator has an official romantic partner (spouse/fiance/lover)
            Pawn officialPartner = initiator.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse);
            if (officialPartner == null)
                officialPartner = initiator.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance);
            if (officialPartner == null)
                officialPartner = initiator.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover);
            
            // Apply cheating penalty if the initiator has an official partner
            if (officialPartner != null)
            {
                int opinionOfOfficialPartner = initiator.relations.OpinionOf(officialPartner);
                int opinionDifference = baseOpinion - opinionOfOfficialPartner;
                
                float cheatingPenalty = SocialInteractions.Settings.cheatingPenalty;
                // Reduce penalty if potential partner is preferred (higher opinion)
                if (opinionDifference > 0)
                {
                    float reductionFactor = System.Math.Max(0f, System.Math.Min(1f, opinionDifference / SocialInteractions.Settings.opinionDifferenceThreshold));
                    cheatingPenalty *= (1f - reductionFactor);
                }
                
                score -= cheatingPenalty;
            }
            
            return System.Math.Max(0f, score);
        }
        
        /// <summary>
        /// Selects a partner from a list of potential partners using weighted random selection.
        /// Higher-scoring partners are more likely to be selected.
        /// </summary>
        private Pawn SelectPartnerWeighted(Pawn initiator, List<KeyValuePair<Pawn, float>> potentialPartners)
        {
            if (potentialPartners == null || potentialPartners.Count == 0) 
                return null;
            
            float totalScore = potentialPartners.Sum(pair => pair.Value);
            if (totalScore <= 0f) 
                return potentialPartners[0].Key;
            
            float randomValue = Rand.Value * totalScore;
            float currentScore = 0f;
            foreach (var pair in potentialPartners)
            {
                currentScore += pair.Value;
                if (randomValue <= currentScore) 
                    return pair.Key;
            }
            
            return potentialPartners.Last().Key;
        }
        
        /// <summary>
        /// Checks if a partner is already being targeted for a date by another pawn.
        /// This prevents multiple pawns from simultaneously trying to date the same partner.
        /// </summary>
        private bool IsPartnerBeingTargeted(Pawn partner)
        {
            if (partner == null || partner.Map == null || partner.Map.mapPawns == null) 
                return false;
            
            foreach (Pawn pawn in partner.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn != null && pawn.jobs != null && pawn.jobs.curJob != null && 
                    pawn.jobs.curJob.def == SI_JobDefOf.GoOnDate)
                {
                    Pawn jobTarget = pawn.jobs.curJob.targetA.Thing as Pawn;
                    if (jobTarget == partner) 
                        return true;
                }
            }
            
            return false;
        }
    }
}