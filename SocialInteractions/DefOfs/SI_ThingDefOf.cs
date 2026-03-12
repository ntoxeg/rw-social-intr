using RimWorld;
using Verse;
using SocialInteractions;

namespace SocialInteractions.DefOfs
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