using RimWorld;
using Verse;

namespace SocialInteractions
{
    public class InteractionWorker_DateLovin : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            return 0f; // This interaction should not be triggered randomly
        }
    }
}