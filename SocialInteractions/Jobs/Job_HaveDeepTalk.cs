using RimWorld;
using Verse;
using Verse.AI;
using SocialInteractions;

namespace SocialInteractions.Jobs
{
    public class Job_HaveDeepTalk : Job
    {
        public InteractionDef interactionDef;
        public string subject;

        public Job_HaveDeepTalk(JobDef jobDef, LocalTargetInfo targetA) : base(jobDef, targetA)
        {
        }
    }
}