using RimWorld;
using Verse;

namespace SocialInteractions
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