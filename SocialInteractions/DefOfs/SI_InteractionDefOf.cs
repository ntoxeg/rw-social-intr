using RimWorld;
using Verse;

namespace SocialInteractions
{
    [DefOf]
    public static class SI_InteractionDefOf
    {
        static SI_InteractionDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SI_InteractionDefOf));
        }

        public static InteractionDef DateRejected;
        public static InteractionDef DateAccepted;
        public static InteractionDef TendPatient;
        public static InteractionDef Lovin;
        public static InteractionDef Rescue;
        public static InteractionDef VisitSickPawn;
        
        public static InteractionDef DateLovin;
        public static InteractionDef CaughtCheating;
        public static InteractionDef ManualChat;
        
        public static InteractionDef Badmouthing;
        public static InteractionDef EnhancedInsult;
        public static InteractionDef Admiration;
        public static InteractionDef Backstabbing;
        public static InteractionDef MakeUp;
        public static InteractionDef ChildAnnoying;
        public static InteractionDef ChildPlayTag;
        public static InteractionDef LoversQuarrel;

    }
}