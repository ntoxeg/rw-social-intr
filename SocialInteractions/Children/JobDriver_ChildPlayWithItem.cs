using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JobDriver_ChildPlayWithItem : JobDriver
    {
        private const int BasePlayDuration = 1800; // 30 seconds in ticks
        private const float ItemDamageChance = 0.3f; // 30% chance of damaging the item during play
        public bool isPlaying = false;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Child should be able to reserve the item and the target location
            // Use the standardized reservation method
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, errorOnFailed: errorOnFailed) &&
                   pawn.Reserve(job.GetTarget(TargetIndex.B), job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail if the item disappears or becomes invalid
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOn(() => !job.GetTarget(TargetIndex.A).Thing.def.EverHaulable); // If item becomes unhauled

            // Go to the item and pick it up
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);

            // Manually pick up the item to avoid stack count issues with minified/special items
            Toil pickupToil = new Toil();
            pickupToil.initAction = delegate
            {
                Pawn actor = pickupToil.actor;
                Thing item = pickupToil.actor.CurJob.GetTarget(TargetIndex.A).Thing;

                if (item == null)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_ChildPlayWithItem: Item is null during pickup, ending job");
                    pickupToil.actor.jobs.EndCurrentJob(JobCondition.Incompletable, true);
                    return;
                }

                // Calculate how many to take (for stackable items)
                int takeNum = 1; // Always take just 1 for this behavior to avoid complexity
                if (item.def.stackLimit > 1 && item.stackCount > 1)
                {
                    takeNum = Mathf.Min(1, item.stackCount); // Take minimum of requested or available
                }

                // Actually pick up the item
                actor.carryTracker.TryStartCarry(item, takeNum);
            };
            pickupToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return pickupToil;

            // Go to the play location
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

            // First half: Play with the item (tossing it around)
            Toil firstHalfPlayToil = new Toil();
            firstHalfPlayToil.initAction = delegate
            {
                isPlaying = true;
                Pawn child = pawn;
                Thing item = (Thing)job.GetTarget(TargetIndex.A).Thing;
                
                if (item == null)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_ChildPlayWithItem: Item is null, ending job");
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Log the play session
                SLog.Message(string.Format("[SocialInteractions] Child {0} started playing with item {1}", 
                    child.LabelShort, item.Label));
            };
            
            firstHalfPlayToil.tickAction = delegate
            {
                // Animation is handled in JobDriver_ModifyCarriedThingDrawPos_Patch
            };
            
            firstHalfPlayToil.defaultCompleteMode = ToilCompleteMode.Delay;
            firstHalfPlayToil.defaultDuration = BasePlayDuration / 2; // Half the duration
            firstHalfPlayToil.socialMode = RandomSocialMode.Off;
            yield return firstHalfPlayToil;

            // Midpoint: Check if item breaks
            Toil midpointCheckToil = new Toil();
            midpointCheckToil.initAction = delegate
            {
                Pawn child = pawn;
                Thing item = (Thing)job.GetTarget(TargetIndex.A).Thing;
                
                if (item == null)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_ChildPlayWithItem: Item is null at midpoint, ending job");
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                bool itemBroke = false;

                // There's a chance the child will damage the item during play
                if (Rand.Value < ItemDamageChance)
                {
                    // Damage the item
                    ThingWithComps thingWithComps = item as ThingWithComps;
                    if (thingWithComps != null)
                    {
                        // Apply damage to the item
                        DamageInfo dinfo = new DamageInfo(DamageDefOf.Deterioration, item.MaxHitPoints / 4, 1f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown);
                        thingWithComps.TakeDamage(dinfo);

                        SLog.Message(string.Format("[SocialInteractions] Child {0} damaged item {1}",
                            child.LabelShort, item.Label));

                        // Show message to player about the damage
                        Messages.Message(string.Format("{0} (child) damaged {1} while playing!", child.LabelShort, item.Label),
                            new LookTargets(child, item), MessageTypeDefOf.NegativeEvent);

                        itemBroke = true;
                    }
                    else if (item.def.useHitPoints)
                    {
                        // Direct hit point damage for simple items
                        item.HitPoints = Mathf.Max(1, item.HitPoints - (item.MaxHitPoints / 4));

                        SLog.Message(string.Format("[SocialInteractions] Child {0} damaged item {1}",
                            child.LabelShort, item.Label));

                        // Show message to player about the damage
                        Messages.Message(string.Format("{0} (child) damaged {1} while playing!", child.LabelShort, item.Label),
                            new LookTargets(child, item), MessageTypeDefOf.NegativeEvent);

                        itemBroke = true;
                    }
                }

                // Create appropriate subject based on whether the item was damaged
                string subject;
                if (itemBroke)
                {
                    subject = string.Format("playing with {0} and accidentally broke it!", item.LabelCap);
                }
                else
                {
                    subject = string.Format("playing with {0} and it's fun!", item.LabelCap);
                }

                // Trigger LLM call at midpoint
                SocialInteractions.HandleMonologue(child, subject);

                // Store the result in job data for the next toil
                job.count = itemBroke ? 1 : 0;
            };
            midpointCheckToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return midpointCheckToil;

            // Conditional: Either flee or continue playing
            Toil conditionalToil = new Toil();
            conditionalToil.initAction = delegate
            {
                bool itemBroke = (job.count == 1);
                
                if (itemBroke)
                {
                    // Item broke - drop it and flee
                    Pawn child = pawn;
                    Thing item = child.carryTracker.CarriedThing;
                    
                    if (item != null)
                    {
                        // Drop the item
                        IntVec3 dropLocation = child.Position;
                        Thing droppedThing;
                        child.carryTracker.TryDropCarriedThing(dropLocation, ThingPlaceMode.Near, out droppedThing);
                        
                        SLog.Message(string.Format("[SocialInteractions] Child {0} dropped broken item {1} and is fleeing",
                            child.LabelShort, item.Label));
                    }
                    
                    isPlaying = false;
                    
                    // Add exclamation mote
                    MoteMaker.MakeColonistActionOverlay(pawn, ThingDefOf.Mote_ColonistFleeing);
                    
                    // Flee to a nearby location (not too far)
                    IntVec3 fleeDest = CellFinderLoose.GetFleeDest(pawn, new List<Thing>{item}, 20f);
                    if (fleeDest != IntVec3.Invalid)
                    {
                        Job fleeJob = JobMaker.MakeJob(JobDefOf.Goto, fleeDest);
                        fleeJob.locomotionUrgency = LocomotionUrgency.Sprint;
                        pawn.jobs.StartJob(fleeJob, JobCondition.InterruptForced);
                    }
                    else
                    {
                        EndJobWith(JobCondition.Succeeded);
                    }
                }
                else
                {
                    // Item didn't break - continue to second half of play
                    ReadyForNextToil();
                }
            };
            conditionalToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return conditionalToil;

            // Second half: Continue playing (only if item didn't break)
            Toil secondHalfPlayToil = new Toil();
            secondHalfPlayToil.tickAction = delegate
            {
                // Animation is handled in JobDriver_ModifyCarriedThingDrawPos_Patch
            };
            
            secondHalfPlayToil.AddFinishAction(() => isPlaying = false);
            
            secondHalfPlayToil.defaultCompleteMode = ToilCompleteMode.Delay;
            secondHalfPlayToil.defaultDuration = BasePlayDuration / 2; // Remaining half
            secondHalfPlayToil.socialMode = RandomSocialMode.Off;
            yield return secondHalfPlayToil;
            
            // Drop the item where the child is (only reached if item didn't break)
            Toil dropToil = new Toil();
            dropToil.initAction = delegate
            {
                Pawn child = pawn;
                Thing item = child.carryTracker.CarriedThing;
                
                if (item == null)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_ChildPlayWithItem: Item is null during drop, ending job");
                    return;
                }

                // Drop the item at the current location
                IntVec3 dropLocation = child.Position;

                if (dropLocation.IsValid && dropLocation.InBounds(Map))
                {
                    Thing droppedThing;
                    child.carryTracker.TryDropCarriedThing(dropLocation, ThingPlaceMode.Near, out droppedThing);

                    SLog.Message(string.Format("[SocialInteractions] Child {0} dropped item {1} at {2}",
                        child.LabelShort, droppedThing.Label, dropLocation));
                }
            };
            
            yield return dropToil;
        }
    }
}