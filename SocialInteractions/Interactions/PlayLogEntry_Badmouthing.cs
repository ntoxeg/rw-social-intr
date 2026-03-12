using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SocialInteractions
{
    /// <summary>
    /// Custom PlayLogEntry for badmouthing interactions that includes information about the target pawn
    /// </summary>
    public class PlayLogEntry_Badmouthing : PlayLogEntry_Interaction
    {
        // Store the target pawn that was badmouthed
        private Pawn targetPawn;

        // Need parameterless constructor for XML serialization
        public PlayLogEntry_Badmouthing()
        {
        }

        public PlayLogEntry_Badmouthing(InteractionDef intDef, Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, Pawn targetPawn = null)
            : base(intDef, initiator, recipient, extraSentencePacks)
        {
            this.targetPawn = targetPawn;
        }

        // Override the ToGameStringFromPOV method to include target pawn information
        public new string ToGameStringFromPOV(Thing pov, bool forceLog = false)
        {
            // Create a simple description of the interaction that includes target information
            if (targetPawn != null)
            {
                if (pov == initiator)
                {
                    // From initiator's perspective: "You spoke negatively about Target to Recipient"
                    return string.Format("You spoke negatively about {0} to {1}", targetPawn.LabelShort, recipient.LabelShort);
                }
                else if (pov == recipient)
                {
                    // From recipient's perspective: "Initiator spoke negatively about Target to you"
                    return string.Format("{0} spoke negatively about {1} to you", initiator.LabelShort, targetPawn.LabelShort);
                }
                else if (pov == targetPawn)
                {
                    // From target's perspective: "Initiator spoke negatively about you to Recipient"
                    return string.Format("{0} spoke negatively about you to {1}", initiator.LabelShort, recipient.LabelShort);
                }
                else
                {
                    // Third person perspective: "Initiator spoke negatively about Target to Recipient"
                    return string.Format("{0} spoke negatively about {1} to {2}", initiator.LabelShort, targetPawn.LabelShort, recipient.LabelShort);
                }
            }
            else
            {
                // Fallback if no target is specified
                if (pov == initiator)
                {
                    return string.Format("You spoke negatively about someone to {0}", recipient.LabelShort);
                }
                else if (pov == recipient)
                {
                    return string.Format("{0} spoke negatively about someone to you", initiator.LabelShort);
                }
                else
                {
                    return string.Format("{0} spoke negatively about someone to {1}", initiator.LabelShort, recipient.LabelShort);
                }
            }
        }
    }
}