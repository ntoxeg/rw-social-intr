using RimWorld;
using Verse;
using SocialInteractions;

namespace SocialInteractions.DefOfs
{
    [DefOf]
    public static class SI_MentalStateDefOf
    {
        static SI_MentalStateDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SI_MentalStateDefOf));
        }

        public static MentalStateDef ChildFleeInTerror;
    }
}