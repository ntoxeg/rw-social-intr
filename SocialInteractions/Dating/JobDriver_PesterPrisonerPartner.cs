using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JobDriver_PesterPrisonerPartner : JobDriver
    {
        private Pawn Target
        {
            get { return (Pawn)this.job.targetA.Thing; }
        }

        private Pawn Initiator
        {
            get { return (Pawn)this.job.targetB.Thing; }
        }

        private int nextInsultTick = 0;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (this.pawn == null)
                return false;

            // Initiator already reserved the target
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextInsultTick, "nextInsultTick", 0);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail if target or initiator is invalid
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnDespawnedOrNull(TargetIndex.B);
            this.FailOnDowned(TargetIndex.A);

            // Initialize
            Toil initialize = new Toil();
            initialize.initAction = () =>
            {
                int interval = Rand.RangeInclusive(
                    SocialInteractions.Settings.pesterInsultIntervalMin,
                    SocialInteractions.Settings.pesterInsultIntervalMax);
                nextInsultTick = Find.TickManager.TicksGame + interval;
            };
            initialize.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return initialize;


            // Follow initiator and occasionally insult target
            Toil followAndJoin = new Toil();
            followAndJoin.tickAction = () =>
            {
                // Check if initiator is still pestering
                if (this.Initiator == null || this.Initiator.CurJobDef != SI_JobDefOf.PesterPrisoner)
                {
                    this.ReadyForNextToil();
                    return;
                }

                // Check if target is still valid for pestering
                if (this.Target == null || this.Target.Dead || this.Target.Downed || !this.Target.Awake() || this.Target.InBed())
                {
                    this.ReadyForNextToil();
                    return;
                }

                // The original check for initiatorPestering is now redundant due to the new check above.
                // However, the user's snippet includes it, so I'll keep it for now, but it will always be true
                // if the previous check passed.
                bool initiatorPestering = this.Initiator.CurJobDef != null && (this.Initiator.CurJobDef.defName == "PesterPrisoner" || this.Initiator.CurJobDef.defName == "AbusiveThreesome");
                
                // If the initiator is not pestering, wait a few ticks before giving up, to handle job transitions
                // This block is now effectively replaced by the new check at the top of tickAction.
                // If the initiator's job is not PesterPrisoner, the job will end immediately.
                // If the initiator's job is PesterPrisoner, this block will not be entered.
                if (!initiatorPestering)
                {
                    if (this.pawn.IsHashIntervalTick(30)) // Check every 0.5s if they are still not pestering before ending
                    {
                        this.EndJobWith(JobCondition.Succeeded);
                        return;
                    }
                    // Otherwise, just wait and let the next tick handle it
                    return;
                }

                // Gain joy over time (less than initiator)
                if (this.pawn.needs.joy != null)
                {
                    JoyKindDef sadisticJoy = DefDatabase<JoyKindDef>.GetNamedSilentFail("Sadistic");
                    if (sadisticJoy == null)
                        sadisticJoy = JoyKindDefOf.Social; // Fallback to social
                    this.pawn.needs.joy.GainJoy(SocialInteractions.Settings.pesterJoyGainRate * 0.5f, sadisticJoy);
                }


                // Check if it's time to insult
                if (Find.TickManager.TicksGame >= nextInsultTick)
                {
                    // Trigger insult interaction
                    if (this.pawn.Position.InHorDistOf(this.Target.Position, 5f))
                    {
                        if (this.pawn.interactions.TryInteractWith(this.Target, InteractionDefOf.Insult))
                        {
                            // If target is a slave, increase suppression
                            if (ModsConfig.IdeologyActive && this.Target.IsSlaveOfColony)
                            {
                                NeedDef suppressionDef = DefDatabase<NeedDef>.GetNamedSilentFail("Suppression");
                                if (suppressionDef != null)
                                {
                                    Need suppression = this.Target.needs.TryGetNeed(suppressionDef);
                                    if (suppression != null)
                                    {
                                        suppression.CurLevel += SocialInteractions.Settings.pesterSuppressionAmount;
                                    }
                                }
                            }
                        }
                    }

                    // Schedule next insult
                    int interval = Rand.RangeInclusive(
                        SocialInteractions.Settings.pesterInsultIntervalMin,
                        SocialInteractions.Settings.pesterInsultIntervalMax);
                    nextInsultTick = Find.TickManager.TicksGame + interval;
                }

                // Follow the target (victim) - chase them!
                if (this.pawn.IsHashIntervalTick(60))
                {
                    float distToTarget = this.pawn.Position.DistanceTo(this.Target.Position);
                    bool isMovingToTarget = this.pawn.pather.Moving && this.pawn.pather.Destination.Cell == this.Target.Position;

                    if (distToTarget > 3.0f || !isMovingToTarget)
                    {
                        this.pawn.pather.StartPath(this.Target, PathEndMode.Touch);
                    }
                }
                
                if (this.Target != null)
                {
                    // Face the target
                    this.pawn.rotationTracker.FaceCell(this.Target.Position);
                }
            };
            followAndJoin.defaultCompleteMode = ToilCompleteMode.Never;
            followAndJoin.socialMode = RandomSocialMode.Off;
            yield return followAndJoin;

            // Finish toil
            Toil finish = new Toil();
            finish.initAction = () =>
            {
                // Give mood buff to partner
                if (this.pawn.needs.mood != null)
                {
                    this.pawn.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.PesteredPrisoner);
                }
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
