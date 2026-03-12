using RimWorld;
using Verse;

namespace SocialInteractions
{
    [DefOf]
    public static class SI_ThingDefOf
    {
        public static ThingDef PauseableMote;

        static SI_ThingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SI_ThingDefOf));
        }
    }
}