using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using UnityEngine;

namespace SocialInteractions
{
    public class JobDriver_PesterPrisoner : JobDriver
    {
        private Pawn Target
        {
            get { return (Pawn)this.job.targetA.Thing; }
        }

        private int pesterStartTick = 0;
        private int nextInsultTick = 0;
        private Pawn partner = null;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (this.pawn == null)
                return false;

            return this.pawn.Reserve(this.Target, this.job, 1, 1, null, errorOnFailed);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pesterStartTick, "pesterStartTick", 0);
            Scribe_Values.Look(ref nextInsultTick, "nextInsultTick", 0);
            Scribe_References.Look(ref partner, "partner");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail if target is invalid
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnDowned(TargetIndex.A);
            this.FailOnMentalState(TargetIndex.A);

            // Initialize
            Toil initialize = new Toil();
            initialize.initAction = () =>
            {
                pesterStartTick = Find.TickManager.TicksGame;
                int interval = Rand.RangeInclusive(
                    SocialInteractions.Settings.pesterInsultIntervalMin,
                    SocialInteractions.Settings.pesterInsultIntervalMax);
                nextInsultTick = pesterStartTick + interval;

                // Get the date partner
                partner = FindPartner();
                if (partner != null)
                {
                    // Partner joins
                    if (partner.CurJobDef != SI_JobDefOf.PesterPrisonerPartner)
                    {
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_PesterPrisoner: Partner {0} is in job {1}. Starting PesterPrisonerPartner.", 
                            partner.LabelShort, partner.CurJobDef != null ? partner.CurJobDef.defName : "NULL"));
                        Job partnerJob = JobMaker.MakeJob(SI_JobDefOf.PesterPrisonerPartner, this.Target, this.pawn);
                        partner.jobs.StartJob(partnerJob, JobCondition.InterruptForced);
                    }
                    else
                    {
                         SLog.Message(string.Format("[SocialInteractions] JobDriver_PesterPrisoner: Partner {0} is already in PesterPrisonerPartner.", partner.LabelShort));
                    }
                    
                    Messages.Message(
                        string.Format("{0} and {1} are pestering {2}.",
                            this.pawn.Name.ToStringShort,
                            partner.Name.ToStringShort,
                            this.Target.Name.ToStringShort),
                        new LookTargets(this.pawn, partner, this.Target),
                        MessageTypeDefOf.NeutralEvent);
                }
                else
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_PesterPrisoner: {0} found no partner to join pestering.", this.pawn.LabelShort));
                }
            };
            initialize.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return initialize;

            // Go to the target first
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Trigger solo pestering dialogue if no partner is present
            Toil soloDialogue = ToilMaker.MakeToil("SoloDialogue");
            soloDialogue.initAction = () =>
            {
                if (partner == null)
                {
                    SocialInteractions.HandleSoloPesterPrompt(this.pawn, this.Target);
                }
            };
            soloDialogue.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return soloDialogue;

            // Follow and pester the target
            Toil followAndPester = new Toil();
            followAndPester.tickAction = () =>
            {
                // Check if we should end (time expired)
                if (Find.TickManager.TicksGame - pesterStartTick >= SocialInteractions.Settings.pesterPrisonerDuration)
                {
                    SLog.Message(string.Format("[SocialInteractions] PesterPrisoner: {0} finished pestering duration. Moving to cleanup.", this.pawn.LabelShort));
                    this.ReadyForNextToil();
                    return;
                }

                // Check if target is still valid for pestering
                if (this.Target == null || this.Target.Dead || this.Target.Downed || !this.Target.Awake() || this.Target.InBed())
                {
                    SLog.Message(string.Format("[SocialInteractions] PesterPrisoner: {0} target {1} is no longer available (dead/downed/asleep). Ending job.", this.pawn.LabelShort, (this.Target != null) ? this.Target.LabelShort : "null"));
                    this.ReadyForNextToil();
                    return;
                }

                // Gain joy over time
                if (this.pawn.needs.joy != null)
                {
                    JoyKindDef sadisticJoy = DefDatabase<JoyKindDef>.GetNamedSilentFail("Sadistic");
                    if (sadisticJoy == null)
                        sadisticJoy = JoyKindDefOf.Social; // Fallback to social
                    this.pawn.needs.joy.GainJoy(SocialInteractions.Settings.pesterJoyGainRate, sadisticJoy);
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

                // Follow the target (throttled check)
                if (this.pawn.IsHashIntervalTick(60))
                {
                    if (!this.pawn.Position.InHorDistOf(this.Target.Position, 3f))
                    {
                        this.pawn.pather.StartPath(this.Target, PathEndMode.Touch);
                    }
                }
                
                if (this.pawn.Position.InHorDistOf(this.Target.Position, 3f))
                {
                    // Face the target
                    this.pawn.rotationTracker.FaceCell(this.Target.Position);
                }
            };
            followAndPester.defaultCompleteMode = ToilCompleteMode.Never;
            followAndPester.socialMode = RandomSocialMode.Off;
            yield return followAndPester;

            // Finish toil
            Toil finish = new Toil();
            finish.initAction = () =>
            {
                SLog.Message(string.Format("[SocialInteractions] PesterPrisoner finish toil started for {0}. Partner null: {1}", 
                    this.pawn.LabelShort, (partner == null)));

                // Give mood buff to initiator
                if (this.pawn.needs.mood != null)
                {
                    this.pawn.needs.mood.thoughts.memories.TryGainMemory(SI_ThoughtDefOf.PesteredPrisoner);
                }

                // Check for threesome escalation
                // Only escalate if we have a partner and we are currently on a date
                // Fallback: If partner is null (e.g. from a save or initial failure), try finding them again
                if (partner == null)
                {
                    SLog.Message(string.Format("[SocialInteractions] PesterPrisoner finish: Partner was null for {0}, attempting recovery.", this.pawn.LabelShort));
                    partner = FindPartner();
                }

                bool isOnDate = DatingManager.IsOnDate(this.pawn);
                SLog.Message(string.Format("[SocialInteractions] PesterPrisoner finish: Checking escalation. Partner: {0}, IsOnDate: {1}", 
                    (partner != null ? partner.LabelShort : "NULL"), isOnDate));

                if (partner != null && isOnDate)
                {
                    TryEscalateToThreesome();
                }
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }

        /// <summary>
        /// Finds a partner for the job. Only considers the partner if currently on a date.
        /// </summary>
        private Pawn FindPartner()
        {
            // Only join if we are on a date
            if (!DatingManager.IsOnDate(this.pawn))
            {
                SLog.Message(string.Format("[SocialInteractions] FindPartner: {0} is not on a date (hediff check).", this.pawn.LabelShort));
                return null;
            }

            Pawn datePartner = DatingManager.GetPartnerOfDateWith(this.pawn);
            if (datePartner == null)
            {
                SLog.Message(string.Format("[SocialInteractions] FindPartner: No partner found in dating list for {0}.", this.pawn.LabelShort));
                return null;
            }

            if (IsPartnerAvailable(datePartner))
            {
                return datePartner;
            }
            
            SLog.Message(string.Format("[SocialInteractions] FindPartner: Partner {0} found but not available.", datePartner.LabelShort));
            return null;
        }

        /// <summary>
        /// Checks if partner is available to join
        /// </summary>
        private bool IsPartnerAvailable(Pawn p)
        {
            return p != null && !p.Dead && !p.Downed &&
                   p.Spawned && p.Awake() && !p.InBed() &&
                   !p.Drafted && !p.InMentalState &&
                   p.Map == this.pawn.Map;
        }

        /// <summary>
        /// Checks if partner should refuse based on traits
        /// </summary>
        public static bool ShouldPartnerRefuse(Pawn p)
        {
            if (p.story == null || p.story.traits == null)
                return false;

            // Kind trait makes them refuse
            if (p.story.traits.HasTrait(TraitDefOf.Kind))
                return true;

            // Incapable of violence makes them refuse
            if (p.WorkTagIsDisabled(WorkTags.Violent))
                return true;

            return false;
        }

        /// <summary>
        /// Tries to escalate to a threesome with the prisoner/slave
        /// </summary>
        private void TryEscalateToThreesome()
        {
            // SLog.Message(string.Format("[SocialInteractions] TryEscalateToThreesome called for {0} targeting {1}", 
            //     this.pawn.LabelShort, this.Target.LabelShort));

            // Check probability based on traits
            float escalationChance = CalculateEscalationChance(this.pawn);
            if (!Rand.Chance(escalationChance))
            {
                SLog.Message(string.Format("[SocialInteractions] TryEscalateToThreesome: Random chance failed ({0:P0}).", escalationChance));
                return;
            }

            // Check romantic compatibility (One-sided: Abuser -> Victim)
            float compatibility = GetAbuserCompatibility(this.pawn, this.Target);
            SLog.Message(string.Format("[SocialInteractions] TryEscalateToThreesome: Compatibility score: {0}, Chance: {1:P0}", compatibility, escalationChance));
            
            if (compatibility <= 0f)
            {
                SLog.Message("[SocialInteractions] TryEscalateToThreesome: Failed compatibility check.");
                return;
            }

            // Check if target is romantically compatible
            if (this.Target.story == null || this.Target.gender == Gender.None)
            {
                SLog.Message("[SocialInteractions] TryEscalateToThreesome: Target invalid (no story or gender).");
                return;
            }

            // Send LLM dialogue
            if (partner != null)
            {
                SLog.Message(string.Format("[SocialInteractions] TryEscalateToThreesome: Triggering LLM prompt with partner {0}", partner.LabelShort));
                SocialInteractions.HandleAbusiveThreesomePrompt(this.pawn, partner, this.Target);
            }

            // Trigger threesome
            SLog.Message("[SocialInteractions] TryEscalateToThreesome: Starting AbusiveThreesome job.");

            Job threesomeJob = JobMaker.MakeJob(SI_JobDefOf.AbusiveThreesome, this.Target, partner);
            this.pawn.jobs.StartJob(threesomeJob, JobCondition.InterruptForced);
        }

        private float CalculateEscalationChance(Pawn initiator)
        {
            float chance = 0.05f; // Baseline 5%

            if (initiator.story == null || initiator.story.traits == null)
                return chance;

            foreach (Trait trait in initiator.story.traits.allTraits)
            {
                if (trait.def == TraitDefOf.Psychopath) chance += 0.35f;
                else if (trait.def == TraitDefOf.Bloodlust) chance += 0.20f;
                else if (trait.def.defName == "Cannibal") chance += 0.10f;
                else if (trait.def.defName.Contains("Sadist") || 
                         trait.def.defName.Contains("Abusive") || 
                         trait.def.defName.Contains("Disturbing") || 
                         trait.def.defName.Contains("Evil"))
                {
                    chance += 0.30f;
                }
            }

            // Check genes (Biotech)
            if (ModsConfig.BiotechActive && initiator.genes != null)
            {
                GeneDef aggressive = DefDatabase<GeneDef>.GetNamedSilentFail("Aggressive_Strong");
                GeneDef veryAggressive = DefDatabase<GeneDef>.GetNamedSilentFail("Aggressive_Hyper");

                if (aggressive != null && initiator.genes.HasActiveGene(aggressive)) chance += 0.15f;
                if (veryAggressive != null && initiator.genes.HasActiveGene(veryAggressive)) chance += 0.30f;
            }

            return Mathf.Clamp(chance, 0.05f, 0.75f);
        }
        /// <summary>
        /// Calculates compatibility primarily from the abuser's perspective.
        /// Ignores the victim's orientation/preferences.
        /// </summary>
        private float GetAbuserCompatibility(Pawn abuser, Pawn victim)
        {
            if (abuser == null || victim == null) return 0f;
            if (abuser == victim) return 0f;

            // Basic race check
            if (!abuser.RaceProps.Humanlike || !victim.RaceProps.Humanlike) return 0f;

            // Age check
            if (abuser.ageTracker.AgeBiologicalYearsFloat < 16f || victim.ageTracker.AgeBiologicalYearsFloat < 16f) return 0f;

            // Abuser orientation check
            if (abuser.story != null && abuser.story.traits != null)
            {
                if (abuser.story.traits.HasTrait(TraitDefOf.Asexual)) return 0f;

                bool isGay = abuser.story.traits.HasTrait(TraitDefOf.Gay);
                bool isBisexual = abuser.story.traits.HasTrait(TraitDefOf.Bisexual);

                if (isGay)
                {
                    if (abuser.gender != victim.gender) return 0f;
                }
                else if (!isBisexual) // Straight (default)
                {
                    if (abuser.gender == victim.gender) return 0f;
                }
            }

            // Return attractiveness from abuser's perspective
            return DatingManager.CalculateAttractiveness(abuser, victim);
        }
    }
}
