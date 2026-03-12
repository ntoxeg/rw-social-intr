using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SocialInteractions
{
    /// <summary>
    /// Custom PlayLogEntry for MakeUp interactions that includes success information
    /// </summary>
    public class PlayLogEntry_MakeUp : PlayLogEntry_Interaction
    {
        private bool success = false;

        // Need parameterless constructor for XML serialization
        public PlayLogEntry_MakeUp()
        {
        }

        public PlayLogEntry_MakeUp(InteractionDef intDef, Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, bool success)
            : base(intDef, initiator, recipient, extraSentencePacks)
        {
            this.success = success;
        }

        // Override the ToGameStringFromPOV method to include success information
        public new string ToGameStringFromPOV(Thing pov, bool forceLog = false)
        {
            string actionDesc = success ? "attempted to reconcile with" : "failed to reconcile with";

            if (pov == initiator)
            {
                // From initiator's perspective
                if (success)
                {
                    return string.Format("You successfully tried to make up with {0}", recipient.LabelShort);
                }
                else
                {
                    return string.Format("You tried to make up with {0} but were unsuccessful", recipient.LabelShort);
                }
            }
            else if (pov == recipient)
            {
                // From recipient's perspective
                if (success)
                {
                    return string.Format("{0} successfully apologized to you and cleared up misunderstandings", initiator.LabelShort);
                }
                else
                {
                    return string.Format("{0} tried to apologize to you but you remained unconvinced", initiator.LabelShort);
                }
            }
            else
            {
                // Third person perspective
                if (success)
                {
                    return string.Format("{0} successfully made up with {1}", initiator.LabelShort, recipient.LabelShort);
                }
                else
                {
                    return string.Format("{0} tried to make up with {1} but was unsuccessful", initiator.LabelShort, recipient.LabelShort);
                }
            }
        }
    }
}