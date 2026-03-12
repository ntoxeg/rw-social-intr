using RimWorld;
using Verse;
using SocialInteractions;

namespace SocialInteractions.DefOfs
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