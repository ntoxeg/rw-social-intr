using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using UnityEngine;

namespace SocialInteractions
{
    public class JobDriver_ChildSpyOnLovin : JobDriver
    {
        private const int WatchDuration = 1000; // ~16 seconds
        private const float DisruptionChance = 0.02f; // Chance per check to disrupt

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Child doesn't need to reserve the couple, just needs to be able to go near them
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            
            // Fail if the target is no longer doing Lovin'
            this.FailOn(() => 
            {
                Pawn target = job.GetTarget(TargetIndex.A).Thing as Pawn;
                if (target == null) return true;
                
                // Check if target is doing Lovin' (Vanilla or Modded)
                return !IsDoingLovin(target);
            });

            // Go to the watch spot
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

            // Watch toil
            Toil watchToil = new Toil();
            watchToil.initAction = delegate
            {
                SLog.Message(string.Format("[SocialInteractions] Child {0} started spying on {1}", pawn.LabelShort, job.GetTarget(TargetIndex.A).Thing.LabelShort));
                // Messages.Message(string.Format("{0} started spying on {1}!", pawn.LabelShort, job.GetTarget(TargetIndex.A).Thing.LabelShort), new LookTargets(pawn, job.GetTarget(TargetIndex.A).Thing), MessageTypeDefOf.NegativeEvent);
            };
            
            watchToil.tickAction = delegate
            {
                Pawn target = job.GetTarget(TargetIndex.A).Thing as Pawn;
                if (target == null) return;

                // Rotate to face the target
                pawn.rotationTracker.FaceTarget(target);

                // Periodically check for disruption (every 100 ticks ~ 1.6s)
                if (pawn.IsHashIntervalTick(100))
                {
                    // Chance to giggle or make noise
                    if (Rand.Value < 0.3f)
                    {
                        // MoteMaker.ThrowMetaIcon(pawn.Position, pawn.Map, ThingDefOf.Mote_IncapIcon); // Just a placeholder icon, maybe a speech bubble later
                    }

                    // Chance to be noticed/disrupt
                    if (Rand.Value < DisruptionChance)
                    {
                        DisruptCouple(target);
                        EndJobWith(JobCondition.Succeeded);
                    }
                }
            };
            
            watchToil.defaultCompleteMode = ToilCompleteMode.Delay;
            watchToil.defaultDuration = WatchDuration;
            watchToil.AddFinishAction(() => 
            {
                // If finished without disruption, maybe just get a memory
                if (pawn.needs != null && pawn.needs.mood != null)
                {
                    pawn.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildSpying, null);
                }
                
                // LLM Monologue about what they saw
                Pawn target = job.GetTarget(TargetIndex.A).Thing as Pawn;
                string partnerName = "";
                if (target != null && target.CurJob != null)
                {
                    SLog.Message(string.Format("[SocialInteractions] Target {0} has job {1}", target.LabelShort, target.CurJob.def.defName));
                    
                    // In vanilla Lovin, we need to find the other pawn
                    // TargetIndex.A could be either the partner (if target is initiator) or the initiator (if target is partner)
                    // TargetIndex.B is always the bed
                    Pawn partner = null;
                    
                    // Check TargetIndex.A - if it's a Pawn and not the target, it's the partner
                    Thing targetA = target.CurJob.GetTarget(TargetIndex.A).Thing;
                    if (targetA is Pawn && targetA != target)
                    {
                        partner = targetA as Pawn;
                        SLog.Message(string.Format("[SocialInteractions] Found partner at TargetIndex.A: {0}", partner.LabelShort));
                    }
                    else
                    {
                        // If TargetIndex.A is not a pawn or is the target itself, check the bed for other occupants
                        Thing targetB = target.CurJob.GetTarget(TargetIndex.B).Thing;
                        if (targetB is Building_Bed)
                        {
                            Building_Bed bed = targetB as Building_Bed;
                            foreach (Pawn occupant in bed.CurOccupants)
                            {
                                if (occupant != target && occupant.CurJob != null && 
                                    (occupant.CurJob.def == JobDefOf.Lovin || occupant.CurJob.def == SI_JobDefOf.DateLovin))
                                {
                                    partner = occupant;
                                    SLog.Message(string.Format("[SocialInteractions] Found partner in bed: {0}", partner.LabelShort));
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (partner != null)
                    {
                        partnerName = " with " + partner.LabelShort;
                    }
                    else
                    {
                        SLog.Message("[SocialInteractions] Could not find partner");
                    }
                }
                
                string subject = string.Format("peeping on {0} doing something naughty in bed{1}", target.LabelShort, partnerName);
                SocialInteractions.HandleMonologue(pawn, subject);
            });
            
            yield return watchToil;
        }

        private bool IsDoingLovin(Pawn p)
        {
            if (p == null || p.CurJob == null) return false;
            
            // Check for vanilla Lovin'
            if (p.CurJob.def == JobDefOf.Lovin) return true;
            
            // Check for modded DateLovin
            if (p.CurJob.def == SI_JobDefOf.DateLovin) return true;
            
            return false;
        }

        private void DisruptCouple(Pawn target)
        {
            SLog.Message(string.Format("[SocialInteractions] Child {0} disrupted {1}'s lovin'", pawn.LabelShort, target.LabelShort));

            // Get the partner
            Pawn partner = null;
            if (target.CurJob.def == JobDefOf.Lovin)
            {
                partner = target.CurJob.GetTarget(TargetIndex.A).Thing as Pawn; // In vanilla Lovin, TargetA is partner
            }
            else if (target.CurJob.def == SI_JobDefOf.DateLovin)
            {
                // In DateLovin, we need to find the partner. 
                // DateLovin uses TargetIndex.A for Partner
                partner = target.CurJob.GetTarget(TargetIndex.A).Thing as Pawn;
            }

            // Apply negative thoughts to both
            if (target.needs != null && target.needs.mood != null)
            {
                target.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildSpyingDisrupted, pawn);
            }

            if (partner != null && partner.needs != null && partner.needs.mood != null)
            {
                partner.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildSpyingDisrupted, pawn);
            }

            // Child gets "Spying" thought (maybe they find it funny they got caught?)
            if (pawn.needs != null && pawn.needs.mood != null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildSpying, null);
            }

            // Send message
            Messages.Message(string.Format("{0} was caught spying on {1} and {2}!", pawn.LabelShort, target.LabelShort, partner != null ? partner.LabelShort : "someone"), 
                new LookTargets(pawn, target), MessageTypeDefOf.NegativeEvent);

            // LLM Monologue
            string subject = string.Format("{0} got caught peeping on {1} doing the deed with {2}!", pawn.LabelShort, target.LabelShort, partner != null ? partner.LabelShort : "someone");
            SocialInteractions.HandleNonStoppingInteraction(pawn, target, SI_InteractionDefOf.ChildAnnoying, subject);

            // Maybe make the couple stop?
            target.jobs.EndCurrentJob(JobCondition.InterruptForced);
            if (partner != null)
            {
                partner.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            
            // Child flees!
            MoteMaker.MakeColonistActionOverlay(pawn, ThingDefOf.Mote_ColonistFleeing);
            IntVec3 fleeDest = CellFinderLoose.GetFleeDest(pawn, new List<Thing>{target}, 20f);
            if (fleeDest != IntVec3.Invalid)
            {
                Job fleeJob = JobMaker.MakeJob(JobDefOf.Goto, fleeDest);
                fleeJob.locomotionUrgency = LocomotionUrgency.Sprint;
                pawn.jobs.StartJob(fleeJob, JobCondition.InterruptForced);
            }
        }
    }
}
