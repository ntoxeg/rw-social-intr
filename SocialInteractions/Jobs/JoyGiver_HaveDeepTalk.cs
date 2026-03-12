using RimWorld;
using Verse;
using Verse.AI;

namespace SocialInteractions
{
    public class JoyGiver_HaveDeepTalk : JoyGiver
    {
        public override Job TryGiveJob(Pawn pawn)
        {
            if (!SocialInteractionUtility.CanInitiateInteraction(pawn) || pawn.relations == null)
            {
                return null;
            }

            Pawn bestLover = LovePartnerRelationUtility.GetPartnerInMyBed(pawn);
            if (bestLover != null && pawn.relations.OpinionOf(bestLover) > 20)
            {
                Job_HaveDeepTalk job = new Job_HaveDeepTalk(def.jobDef, bestLover);
                job.interactionDef = InteractionDefOf.DeepTalk;
                return job;
            }

            return null;
        }
    }
}