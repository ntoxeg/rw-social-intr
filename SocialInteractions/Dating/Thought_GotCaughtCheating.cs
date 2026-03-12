using RimWorld;
using Verse;
using SocialInteractions;

namespace SocialInteractions.Dating
{
    public class Thought_GotCaughtCheating : Thought_Memory
    {
        public override string LabelCap
        {
            get
            {
                if (this.otherPawn == null)
                {
                    return "Got caught cheating".CapitalizeFirst();
                }
                return string.Format("Got caught cheating by {0}", this.otherPawn.Name.ToStringShort).CapitalizeFirst();
            }
        }
    }
}