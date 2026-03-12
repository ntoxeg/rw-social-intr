using RimWorld;
using Verse;

namespace SocialInteractions
{
    [DefOf]
    public static class ChildThoughtDefOf
    {
        public static ThoughtDef ChildAnnoyance;
        public static ThoughtDef ChildMisbehaved;
        public static ThoughtDef ChildBoredom;
        public static ThoughtDef ChildCrying;
        public static ThoughtDef ChildDestructive;
        public static ThoughtDef ChildMischievous;
        public static ThoughtDef ChildRiskTaking;
        public static ThoughtDef ChildReckless;
        public static ThoughtDef ChildSpying;
        public static ThoughtDef ChildSpyingDisrupted;

        static ChildThoughtDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ChildThoughtDefOf));
        }
    }
}