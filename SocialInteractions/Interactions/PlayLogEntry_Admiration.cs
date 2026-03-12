using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SocialInteractions
{
    /// <summary>
    /// Custom PlayLogEntry for Admiration interactions that includes admiration type information
    /// </summary>
    public class PlayLogEntry_Admiration : PlayLogEntry_Interaction
    {
        private AdmirationType admirationType = AdmirationType.GeneralPraise;

        // Need parameterless constructor for XML serialization
        public PlayLogEntry_Admiration()
        {
        }

        public PlayLogEntry_Admiration(InteractionDef intDef, Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, AdmirationType admirationType)
            : base(intDef, initiator, recipient, extraSentencePacks)
        {
            this.admirationType = admirationType;
        }

        // Override the ToGameStringFromPOV method to include admiration-type-based text
        public new string ToGameStringFromPOV(Thing pov, bool forceLog = false)
        {
            string actionDesc = GetAdmirationBasedActionDescription(admirationType);

            if (pov == initiator)
            {
                // From initiator's perspective
                return string.Format("You expressed admiration to {0} ({1})", recipient.LabelShort, actionDesc);
            }
            else if (pov == recipient)
            {
                // From recipient's perspective
                return string.Format("{0} expressed admiration to you ({1})", initiator.LabelShort, actionDesc);
            }
            else
            {
                // Third person perspective
                return string.Format("{0} expressed admiration to {1} ({2})", initiator.LabelShort, recipient.LabelShort, actionDesc);
            }
        }

        private string GetAdmirationBasedActionDescription(AdmirationType admirationType)
        {
            switch (admirationType)
            {
                case AdmirationType.SharedInterestPraise:
                    return "about shared values and interests";
                case AdmirationType.SkillBasedAdmiration:
                    return "about their exceptional skills";
                case AdmirationType.InspirationalPraise:
                    return "as an inspirational figure";
                case AdmirationType.GeneralPraise:
                default:
                    return "with general praise and appreciation";
            }
        }
    }
}