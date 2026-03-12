using RimWorld;
using Verse;
using SocialInteractions;

namespace SocialInteractions.Dating
{
    public class Thought_CaughtCheating : Thought_Memory
    {
        public override string LabelCap
        {
            get
            {
                return string.Format("Caught {0} cheating", this.otherPawn.Name.ToStringShort).CapitalizeFirst();
            }
        }
    }
}