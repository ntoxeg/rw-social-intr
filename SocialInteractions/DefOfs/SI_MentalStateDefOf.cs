using RimWorld;
using Verse;

namespace SocialInteractions
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