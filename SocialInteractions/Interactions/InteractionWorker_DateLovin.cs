using RimWorld;
using Verse;
using SocialInteractions;

namespace SocialInteractions.Interactions
{
    public class InteractionWorker_DateLovin : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            return 0f; // This interaction should not be triggered randomly
        }
    }
}