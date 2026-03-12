using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SocialInteractions
{
    /// <summary>
    /// Custom PlayLogEntry for EnhancedInsult interactions that includes severity information
    /// </summary>
    public class PlayLogEntry_EnhancedInsult : PlayLogEntry_Interaction
    {
        private InsultSeverity severity = InsultSeverity.Mild;
        private bool ledToFight = false;

        // Need parameterless constructor for XML serialization
        public PlayLogEntry_EnhancedInsult()
        {
        }

        public PlayLogEntry_EnhancedInsult(InteractionDef intDef, Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, InsultSeverity severity, bool ledToFight = false)
            : base(intDef, initiator, recipient, extraSentencePacks)
        {
            this.severity = severity;
            this.ledToFight = ledToFight;
        }

        // Override the ToGameStringFromPOV method to include severity-based text
        public new string ToGameStringFromPOV(Thing pov, bool forceLog = false)
        {
            string actionDesc = GetSeverityBasedActionDescription(severity, ledToFight);

            if (pov == initiator)
            {
                // From initiator's perspective
                return string.Format("You {0} {1}", actionDesc, recipient.LabelShort);
            }
            else if (pov == recipient)
            {
                // From recipient's perspective
                return string.Format("{0} {1} you", initiator.LabelShort, actionDesc);
            }
            else
            {
                // Third person perspective
                return string.Format("{0} {1} {2}", initiator.LabelShort, actionDesc, recipient.LabelShort);
            }
        }

        private string GetSeverityBasedActionDescription(InsultSeverity severity, bool ledToFight)
        {
            if (ledToFight)
            {
                switch (severity)
                {
                    case InsultSeverity.Violent:
                        return "launched a violent verbal attack against that escalated to fighting";
                    case InsultSeverity.Severe:
                        return "hurled severe insults at that led to a fight";
                    case InsultSeverity.Moderate:
                        return "made harsh comments that resulted in a physical confrontation";
                    case InsultSeverity.Mild:
                    default:
                        return "made a subtle insult that somehow resulted in a fight";
                }
            }
            else
            {
                switch (severity)
                {
                    case InsultSeverity.Violent:
                        return "launched a violent verbal attack against";
                    case InsultSeverity.Severe:
                        return "hurled severe insults at";
                    case InsultSeverity.Moderate:
                        return "made harsh comments about";
                    case InsultSeverity.Mild:
                    default:
                        return "made a subtle or backhanded comment toward";
                }
            }
        }
    }
}