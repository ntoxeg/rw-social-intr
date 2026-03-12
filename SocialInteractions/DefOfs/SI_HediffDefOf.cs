using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace SocialInteractions
{
    [DefOf]
    public static class SI_HediffDefOf
    {
        static SI_HediffDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SI_HediffDefOf));
        }

        public static HediffDef OnDate;
        public static HediffDef SI_Naked;
        public static HediffDef SI_Negotiating;
        public static HediffDef Abused;

    }
}