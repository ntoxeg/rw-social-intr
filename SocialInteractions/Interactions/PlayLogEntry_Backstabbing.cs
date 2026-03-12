using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SocialInteractions
{
    /// <summary>
    /// Custom PlayLogEntry for Backstabbing interactions that includes target pawn and success information
    /// </summary>
    public class PlayLogEntry_Backstabbing : PlayLogEntry_Interaction
    {
        private Pawn targetPawn;
        private bool success;

        // Need parameterless constructor for XML serialization
        public PlayLogEntry_Backstabbing()
        {
        }

        public PlayLogEntry_Backstabbing(InteractionDef intDef, Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, Pawn targetPawn, bool success)
            : base(intDef, initiator, recipient, extraSentencePacks)
        {
            this.targetPawn = targetPawn;
            this.success = success;
        }

        // Override the ToGameStringFromPOV method to include backstabbing-specific text with target information
        public new string ToGameStringFromPOV(Thing pov, bool forceLog = false)
        {
            string actionDesc = GetBackstabBasedActionDescription(success);

            if (pov == initiator)
            {
                // From initiator's perspective
                return string.Format("You {0} {1} about {2}", actionDesc, recipient.LabelShort, targetPawn.LabelShort);
            }
            else if (pov == recipient)
            {
                // From recipient's perspective
                if (success)
                {
                    return string.Format("{0} successfully deceived you about {1}, turning you against them", initiator.LabelShort, targetPawn.LabelShort);
                }
                else
                {
                    return string.Format("{0} tried to deceive you about {1}, but you saw through their lie", initiator.LabelShort, targetPawn.LabelShort);
                }
            }
            else if (pov == targetPawn)
            {
                // From target's perspective
                if (success)
                {
                    return string.Format("{0} successfully turned {1} against you through deception", initiator.LabelShort, recipient.LabelShort);
                }
                else
                {
                    return string.Format("{0} attempted to turn {1} against you, but {1} saw through the deception", initiator.LabelShort, recipient.LabelShort);
                }
            }
            else
            {
                // Third person perspective
                return string.Format("{0} {1} {2} about {3}", initiator.LabelShort, actionDesc, recipient.LabelShort, targetPawn.LabelShort);
            }
        }

        private string GetBackstabBasedActionDescription(bool success)
        {
            if (success)
            {
                return "deceived and manipulated";
            }
            else
            {
                return "tried to deceive";
            }
        }
    }
}