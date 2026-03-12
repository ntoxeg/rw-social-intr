using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace SocialInteractions
{
    public class JobDriver_ChildPlayWithRadio : JobDriver
    {
        private bool leakLocation = false;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref leakLocation, "leakLocation", false);
        }

        private bool DetermineLeakOutcome()
        {
            int socialLevel = pawn.skills.GetSkill(SkillDefOf.Social).Level;
            // Chance is 100% at level 0, decreasing by 9% per level, min 10%
            float chance = Mathf.Max(0.1f, 1.0f - (socialLevel * 0.09f));
            return Rand.Value < chance;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.InteractionCell);

            // Wait Toil (15 seconds)
            Toil waitToil = Toils_General.Wait(900, TargetIndex.A);
            waitToil.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            waitToil.WithProgressBarToilDelay(TargetIndex.A);
            waitToil.tickAction = () =>
            {
                if (pawn.IsHashIntervalTick(300))
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Blah blah...", 3f);
                }
            };
            yield return waitToil;

            // Decision Toil
            Toil decisionToil = new Toil();
            decisionToil.initAction = () =>
            {
                leakLocation = DetermineLeakOutcome();
                
                string subject = leakLocation 
                    ? "talking to stranger on radio, accidentally reveals location." 
                    : "talking to stranger on radio, chatting innocently or outsmarting them.";

                // Use helper for LLM monologue
                SocialInteractions.HandleMonologue(pawn, subject, topic: "radio_chatter");

                if (leakLocation)
                {
                    // Trigger Raid Logic
                    IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, pawn.Map);
                    parms.forced = true;
                    parms.target = pawn.Map;

                    QueuedIncident qi = new QueuedIncident(new FiringIncident(IncidentDefOf.RaidEnemy, null, parms), Find.TickManager.TicksGame + 60000);
                    Find.Storyteller.incidentQueue.Add(qi);

                    Find.LetterStack.ReceiveLetter("Location Leaked!", 
                        string.Format("{0} has accidentally revealed your location to raiders while playing with the radio! Expect a raid in about a day.", pawn.LabelShort), 
                        LetterDefOf.ThreatBig, pawn);
                }
            };
            decisionToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return decisionToil;
        }
    }
}
