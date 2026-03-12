using RimWorld;
using Verse;

namespace SocialInteractions
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