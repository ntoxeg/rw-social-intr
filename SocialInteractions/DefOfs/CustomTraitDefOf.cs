using RimWorld;
using Verse;

namespace SocialInteractions
{
    [DefOf]
    public static class CustomTraitDefOf
    {
        public static TraitDef Masochist;

        static CustomTraitDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CustomTraitDefOf));
        }
    }
}