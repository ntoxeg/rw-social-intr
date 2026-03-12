using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SocialInteractions
{
    /// <summary>
    /// Utility class for calculating social influence and integration metrics
    /// </summary>
    public static class SocialInfluenceUtility
    {
        /// <summary>
        /// Calculates a pawn's social influence score normalized to 0-1 range (0 = worst influence, 1 = max influence)
        /// </summary>
        /// <param name="target">The pawn whose social influence to calculate</param>
        /// <param name="allPawns">All pawns to consider in the calculation</param>
        /// <returns>Normalized social influence score (0 = worst influence, 1 = max influence)</returns>
        public static float CalculateSocialInfluence(Pawn target, List<Pawn> allPawns)
        {
            if (target == null) return 0f;
            
            int totalPositiveOpinion = 0;
            int validOpinionsCount = 0;
            
            // Calculate total positive opinion from all other pawns
            foreach (Pawn otherPawn in allPawns)
            {
                if (otherPawn == target || otherPawn.relations == null) continue;
                
                int opinion = otherPawn.relations.OpinionOf(target);
                totalPositiveOpinion += System.Math.Max(0, opinion); // Only positive opinions contribute to influence
                validOpinionsCount++;
            }
            
            float averagePositiveOpinion = validOpinionsCount > 0 ? (float)totalPositiveOpinion / validOpinionsCount : 0f;
            
            // Get social skill level (normalized to 0-1)
            float socialSkill = target.skills != null ? target.skills.GetSkill(SkillDefOf.Social).Level : 1f;
            float normalizedSocialSkill = socialSkill / 20f;
            
            // Calculate normalized social influence
            float normalizedInfluence = (averagePositiveOpinion / 100.0f) * normalizedSocialSkill;
            
            return System.Math.Max(0f, System.Math.Min(1.0f, normalizedInfluence));
        }
        
        /// <summary>
        /// Calculates how integrated a candidate is within the social circle, normalized to 0-1 range (0 = worst integration, 1 = best integration)
        /// Less integrated pawns are more likely to be targeted for badmouthing
        /// </summary>
        /// <param name="initiator">The pawn initiating the evaluation</param>
        /// <param name="candidate">The pawn being evaluated</param>
        /// <param name="allPawns">All pawns in the colony</param>
        /// <returns>Normalized integration factor (0 = worst integration, 1 = best integration)</returns>
        public static float CalculateSocialIntegration(Pawn initiator, Pawn candidate, List<Pawn> allPawns)
        {
            if (initiator == null || candidate == null) return 0f; // 0 = worst integration
            
            // Calculate how well the candidate is connected to the initiator's social connections
            float connectionScore = 0f;
            int connectionCount = 0;
            
            foreach (Pawn otherPawn in allPawns)
            {
                if (otherPawn == initiator || otherPawn == candidate || otherPawn.relations == null) continue;
                
                // Check if the other pawn has any relationship with the initiator
                int initiatorOpinionOfOther = initiator.relations.OpinionOf(otherPawn);
                
                // Consider pawns that the initiator has any opinion of (positive or negative)
                if (System.Math.Abs(initiatorOpinionOfOther) >= 2)
                {
                    int otherPawnOpinionOfCandidate = otherPawn.relations.OpinionOf(candidate);
                    // Only positive opinions of candidate contribute to integration
                    connectionScore += System.Math.Max(0, otherPawnOpinionOfCandidate);
                    connectionCount++;
                }
            }
            
            // Average positive connection score (higher means more integrated)
            float avgPositiveConnection = connectionCount > 0 ? (float)connectionScore / connectionCount : 0f;
            
            // Normalize to 0-1 range
            float normalizedIntegration = avgPositiveConnection / 100f;
            
            return System.Math.Max(0f, System.Math.Min(1.0f, normalizedIntegration));
        }
    }
}