using RimWorld;
using Verse;

namespace SocialInteractions
{
    [DefOf]
    public static class SI_ThoughtDefOf
    {
        static SI_ThoughtDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SI_ThoughtDefOf));
        }

        public static ThoughtDef CaughtCheating;
        public static ThoughtDef GotCaughtCheating;
        public static ThoughtDef WasCheatedOn;
        
        // Badmouthing/gossip thoughts
        public static ThoughtDef BondedOverSharedDislike;
        public static ThoughtDef FoundCommonGround;
        
        // Admiration thoughts
        public static ThoughtDef SeekingApproval;
        public static ThoughtDef AdmiredBySomeone;
        
        // Backstabbing thoughts
        public static ThoughtDef WasBackstabbed;
        public static ThoughtDef SuccessfullyBackstabbedSomeone;
        public static ThoughtDef WasManipulatedAgainstSomeone;
        public static ThoughtDef WasTargetOfFailedManipulation;
        public static ThoughtDef FailedBackstabAttempt;
        
        // Dating thoughts
        public static ThoughtDef EnjoyedDateWith;
        public static ThoughtDef DateWentBadly;
        
        // Negotiation thoughts
        public static ThoughtDef SI_NegotiationPositive;
        public static ThoughtDef SI_NegotiationNegative;
        
        // Pester prisoner thoughts
        public static ThoughtDef WasAbused;
        public static ThoughtDef AbusedMe;
        public static ThoughtDef PesteredPrisoner;

    }
}