using RimWorld;
using Verse;
using SocialInteractions;

namespace SocialInteractions.Dating
{
    public class Thought_WasCheatedOn : Thought_Memory
    {
        public override string LabelCap
        {
            get
            {
                if (this.otherPawn == null)
                {
                    return "Was cheated on".CapitalizeFirst();
                }
                return string.Format("Was cheated on by {0}", this.otherPawn.Name.ToStringShort).CapitalizeFirst();
            }
        }
    }
}