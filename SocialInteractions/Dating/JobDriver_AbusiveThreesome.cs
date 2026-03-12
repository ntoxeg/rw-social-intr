using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System;

namespace SocialInteractions
{
    public class JobDriver_AbusiveThreesome : JobDriver
    {
        private Pawn Victim
        {
            get { return (Pawn)this.job.targetA.Thing; }
        }

        private Pawn Partner
        {
            get { return (Pawn)this.job.targetB.Thing; }
        }

        public int ticksLeft;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (this.pawn == null)
                return false;

            if (!this.pawn.Reserve(this.Victim, this.job, 1, 1, null, errorOnFailed))
                return false;

            if (this.Partner != null && !this.pawn.Reserve(this.Partner, this.job, 1, 1, null, errorOnFailed))
                return false;

            return true;
        }

        public override bool CanBeginNowWhileLyingDown()
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail if victim is invalid
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnDowned(TargetIndex.A);
            
            // Add fail condition if pawn is drafted or interrupted
            this.FailOn(() => this.pawn.Drafted);

            // Find a suitable location
            Toil findSpot = new Toil();
            findSpot.initAction = () =>
            {
                this.job.targetC = this.Victim.Position;
                SLog.Message(string.Format("[SocialInteractions] AbusiveThreesome: Initiator {0} moving to victim {1} at {2} for stacking.", 
                    this.pawn.LabelShort, (this.Victim != null ? this.Victim.LabelShort : "NULL"), this.job.targetC.Cell));

                // Assign participant jobs to victim and partner IMMEDIATELY
                if (this.Victim != null)
                {
                    Job participantJob = JobMaker.MakeJob(SI_JobDefOf.AbusiveThreesomeParticipant, this.pawn, this.Victim, this.job.targetC);
                    this.Victim.jobs.StartJob(participantJob, JobCondition.InterruptForced);
                    SLog.Message(string.Format("[SocialInteractions] AbusiveThreesome: Assigned participant job to victim {0} at {1}.", this.Victim.LabelShort, this.job.targetC.Cell));
                }
                
                if (this.Partner != null)
                {
                    Job participantJob = JobMaker.MakeJob(SI_JobDefOf.AbusiveThreesomeParticipant, this.pawn, this.Victim, this.job.targetC);
                    this.Partner.jobs.StartJob(participantJob, JobCondition.InterruptForced);
                    SLog.Message(string.Format("[SocialInteractions] AbusiveThreesome: Assigned participant job to partner {0} at {1}.", this.Partner.LabelShort, this.job.targetC.Cell));
                }
            };
            findSpot.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return findSpot;

            // Go to the spot
            if (job.GetTarget(TargetIndex.C).HasThing)
            {
                yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.InteractionCell);
            }
            else
            {
                yield return Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell);
            }

            // Perform the act (Lovin')
            Toil performAct = ToilMaker.MakeToil("PerformAct");
            performAct.initAction = () =>
            {
                ticksLeft = SocialInteractions.Settings.dateLovinTicks;
            };
            performAct.tickAction = () =>
            {
                // Safety checks
                if (this.pawn == null || this.Victim == null)
                {
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Apply naked hediff (Ensuring it's present throughout)
                AddNakedHediff(this.pawn);
                AddNakedHediff(this.Victim);
                if (this.Partner != null)
                {
                    AddNakedHediff(this.Partner);
                }

                ticksLeft--;

                // Throw heart flecks occasionally
                if (this.pawn.IsHashIntervalTick(100))
                {
                    if (this.pawn.Position != null && this.pawn.Map != null)
                    {
                        FleckMaker.ThrowMetaIcon(this.pawn.Position, this.pawn.Map, FleckDefOf.Heart);
                    }
                    
                    // Gain joy
                    if (this.pawn.needs.joy != null)
                    {
                        this.pawn.needs.joy.GainJoy(0.05f, JoyKindDefOf.Social);
                    }
                }

                if (ticksLeft <= 0)
                {
                    this.ReadyForNextToil();
                }
            };
            performAct.defaultCompleteMode = ToilCompleteMode.Never;
            performAct.socialMode = RandomSocialMode.Off;
            yield return performAct;

            // Cleanup toil (Remove hediffs, apply consequences)
            Toil cleanup = ToilMaker.MakeToil("Cleanup");
            cleanup.initAction = () =>
            {
                // Remove naked hediffs
                RemoveNakedHediff(this.pawn);
                RemoveNakedHediff(this.Victim);
                if (this.Partner != null)
                {
                    RemoveNakedHediff(this.Partner);
                }

                // Apply consequences only if job completed successfully (ticksLeft <= 0)
                if (ticksLeft <= 0)
                {
                    ApplyConsequences();
                }
            };
            cleanup.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return cleanup;
        }

        private void ApplyConsequences()
        {
            // Check if victim should suffer consequences
            bool hasHighLibido = false;
            bool hasFreeLovinIdeo = false;

            // Check for high libido gene
            if (this.Victim.genes != null)
            {
                GeneDef highLibidoGene = DefDatabase<GeneDef>.GetNamedSilentFail("Libido_High");
                if (highLibidoGene != null)
                {
                    hasHighLibido = this.Victim.genes.HasActiveGene(highLibidoGene);
                }
            }

            // Check for free lovin ideology
            if (this.Victim.Ideo != null)
            {
                hasFreeLovinIdeo = this.Victim.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("Lovin_FreeApproved"));
            }

            // Apply consequences if victim doesn't have protective traits
            if (!hasHighLibido && !hasFreeLovinIdeo)
            {
                // Apply Abused hediff (9 hour down state)
                Hediff abusedHediff = HediffMaker.MakeHediff(SI_HediffDefOf.Abused, this.Victim);
                this.Victim.health.AddHediff(abusedHediff);

                // Apply mood debuff
                this.Victim.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.WasAbused);

                // Apply opinion penalties for BOTH culprits
                this.Victim.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.AbusedMe, this.pawn);
                if (this.Partner != null)
                {
                    this.Victim.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.AbusedMe, this.Partner);
                }

                Messages.Message(
                    string.Format("{0} has been traumatized by the abuse.",
                        this.Victim.Name.ToStringShort),
                    new LookTargets(this.Victim),
                    MessageTypeDefOf.NegativeEvent);
            }
            else
            {
                // Give mood buffs to the victim if they enjoyed it (High Libido or Free Lovin')
                if (this.Victim.needs.mood != null)
                {
                    ThoughtDef lovinThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("GotSomeLovin");
                    if (lovinThought != null)
                    {
                        // Apply social memories for BOTH culprits
                        this.Victim.needs.mood.thoughts.memories.TryGainMemory(lovinThought, this.pawn);
                        if (this.Partner != null)
                        {
                            this.Victim.needs.mood.thoughts.memories.TryGainMemory(lovinThought, this.Partner);
                        }
                    }
                }
            }

            // Give mood buffs to abusers (reuse existing threesome buffs if available)
            if (this.pawn.needs.mood != null)
            {
                ThoughtDef threesomeThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("GotSomeLovin");
                if (threesomeThought != null)
                {
                    // Initiator was with Victim
                    this.pawn.needs.mood.thoughts.memories.TryGainMemory(threesomeThought, this.Victim);
                }
                else
                {
                    this.pawn.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.PesteredPrisoner);
                }
            }

            if (this.Partner != null && this.Partner.needs.mood != null)
            {
                ThoughtDef threesomeThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("GotSomeLovin");
                if (threesomeThought != null)
                {
                    // Partner was with Victim
                    this.Partner.needs.mood.thoughts.memories.TryGainMemory(threesomeThought, this.Victim);
                }
                else
                {
                    this.Partner.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.PesteredPrisoner);
                }
            }

            // --- Pregnancy logic (Biotech) ---
            if (ModsConfig.BiotechActive && this.Victim != null && this.Victim.gender == Gender.Female && 
                !this.Victim.health.hediffSet.HasHediff(HediffDefOf.PregnantHuman))
            {
                List<Pawn> maleAssailants = new List<Pawn>();
                if (this.pawn != null && this.pawn.gender == Gender.Male) maleAssailants.Add(this.pawn);
                if (this.Partner != null && this.Partner.gender == Gender.Male) maleAssailants.Add(this.Partner);

                if (maleAssailants.Count > 0)
                {
                    Pawn malePawn = maleAssailants.RandomElement();
                    // Use the same pregnancy chance as vanilla lovin
                    float pregnancyChance = 0.05f;
                    
                    if (Rand.Chance(pregnancyChance * PregnancyUtility.PregnancyChanceForPartners(this.Victim, malePawn)))
                    {
                        bool success;
                        GeneSet inheritedGeneSet = PregnancyUtility.GetInheritedGeneSet(malePawn, this.Victim, out success);
                        if (success)
                        {
                            Hediff_Pregnant hediff_Pregnant = (Hediff_Pregnant)HediffMaker.MakeHediff(HediffDefOf.PregnantHuman, this.Victim);
                            hediff_Pregnant.SetParents(null, malePawn, inheritedGeneSet);
                            this.Victim.health.AddHediff(hediff_Pregnant);
                        }
                        else if (PawnUtility.ShouldSendNotificationAbout(malePawn) || PawnUtility.ShouldSendNotificationAbout(this.Victim))
                        {
                            Messages.Message("MessagePregnancyFailed".Translate(malePawn.Named("FATHER"), this.Victim.Named("MOTHER")) + ": " + "CombinedGenesExceedMetabolismLimits".Translate(), new LookTargets(malePawn, this.Victim), MessageTypeDefOf.NegativeEvent);
                        }
                    }
                }
            }
            // --- End Pregnancy logic ---
        }

        private void AddNakedHediff(Pawn p)
        {
            if (p != null && p.health != null)
            {
                HediffDef nakedDef = HediffDef.Named("SI_Naked");
                if (!p.health.hediffSet.HasHediff(nakedDef))
                {
                    p.health.AddHediff(nakedDef);
                }
            }
        }

        private void RemoveNakedHediff(Pawn p)
        {
            if (p != null && p.health != null)
            {
                HediffDef nakedDef = HediffDef.Named("SI_Naked");
                Hediff hediff = p.health.hediffSet.GetFirstHediffOfDef(nakedDef);
                if (hediff != null)
                {
                    p.health.RemoveHediff(hediff);
                }
            }
        }

        public override Vector3 ForcedBodyOffset
        {
            get
            {
                if (pawn == null || ticksLeft <= 0)
                {
                    return Vector3.zero;
                }

                int totalTicks = SocialInteractions.Settings.dateLovinTicks;
                if (totalTicks <= 0) return Vector3.zero;

                float progress = 1.0f - ((float)ticksLeft / totalTicks);

                float animationSpeed = 1.0f;
                if (progress <= 0.90f)
                {
                    animationSpeed = 1.0f + (progress / 0.90f) * 0.75f;
                }
                else
                {
                    animationSpeed = 0.3f;
                }
                
                float baseTime = progress * 8.0f * (totalTicks / 60.0f);
                
                // Initiator uses horizontal bounce (exact standard lovin formula)
                float adjustedTime = baseTime * animationSpeed;
                float num = Mathf.Sin(adjustedTime);
                float num2 = Mathf.Sign(num);
                float x = EaseInOutQuad(Mathf.Abs(num) * 0.6f) * 0.09f * num2;
                
                return new Vector3(x, 0f, 0f);
            }
        }

        private float EaseInOutQuad(float v)
        {
            if (!((double)v < 0.5))
            {
                return 1f - Mathf.Pow(-2f * v + 2f, 4f) / 2f;
            }
            return 8f * v * v * v * v;
        }
    }
}
