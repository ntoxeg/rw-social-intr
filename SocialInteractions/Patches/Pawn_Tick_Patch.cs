using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(Pawn), "Tick")]
    public static class Pawn_Tick_Patch
    {
        // Dictionary to track cooldowns for caught cheaters to prevent repeated detections
        private static Dictionary<Pawn, int> caughtCheatersCooldowns = new Dictionary<Pawn, int>();
        
        public static void Postfix(Pawn __instance)
        {
            Pawn pawn = __instance;
            if (pawn == null || pawn.relations == null || pawn.Map == null)
            {
                return;
            }
            
            // Only check for cheating once per second (60 ticks) instead of every tick
            // Check from the perspective of pawns who are on a date
            if (pawn.IsHashIntervalTick(60))
            {
                // Check if this pawn is on a date
                if (DatingManager.IsOnDate(pawn))
                {
                    // Check if this pawn is currently engaged in the lovin activity
                    // We need to check if the pawn is actually doing the lovin, not just pathing to the spot
                    if (pawn.CurJobDef == SI_JobDefOf.DateLovin && pawn.jobs != null && pawn.jobs.curDriver != null)
                    {
                        // Check if the pawn is actually in the lovin toil, not just pathing to the spot
                        // The lovin toil is the second toil in the JobDriver_DateLovin
                        if (pawn.jobs.curDriver.CurToilIndex >= 2) // Index 0 is Goto, Index 1 is WaitForPartner, Index 2+ is Lovin
                        {
                            // Only proceed if the pawn is not on cooldown
                            if (!caughtCheatersCooldowns.ContainsKey(pawn) || Find.TickManager.TicksGame > caughtCheatersCooldowns[pawn])
                            {
                                // Get the partner this pawn is on a date with
                                Pawn datePartner = DatingManager.GetPartnerOfDateWith(pawn);
                                
                                // If we have a date partner, check if we're cheating
                                if (datePartner != null)
                                {
                                    // Check if this pawn has an official romantic partner
                                    Pawn officialPartner = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse);
                                    if (officialPartner == null)
                                    {
                                        officialPartner = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance);
                                    }
                                    if (officialPartner == null)
                                    {
                                        officialPartner = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover);
                                    }
                                    
                                    // If the pawn has an official partner and it's not the same as the date partner, we're cheating
                                    if (officialPartner != null && officialPartner != datePartner)
                                    {
                                        // Check if the official partner is nearby (within 5 tiles)
                                        if (officialPartner.Position.InHorDistOf(pawn.Position, 5f))
                                        {
                                            // Put the cheater on cooldown for 2 minutes (12000 ticks)
                                            caughtCheatersCooldowns[pawn] = Find.TickManager.TicksGame + 12000;
                                            
                                            // We found a cheater! The pawn is on a date with someone other than their official partner
                                            // and their official partner is nearby to witness it
                                            SLog.Message(string.Format(
                                                "[SocialInteractions] Cheating detected: {0} is on a date with {1} but is married to {2} who is nearby", 
                                                pawn.LabelShort, datePartner.LabelShort, officialPartner.LabelShort));
                                            
                                            // Register the interaction in the social log first
                                            Find.PlayLog.Add(new PlayLogEntry_Interaction(
                                                SI_InteractionDefOf.CaughtCheating, officialPartner, pawn, null));
                                            
                                            // Trigger the special cheating event
                                            HandleCheatingEvent(officialPartner, pawn, datePartner);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            // Periodically clean up the cooldown dictionary to prevent it from growing indefinitely
            if (pawn.IsHashIntervalTick(1800)) // Check every 30 seconds
            {
                // Create a list of pawns to remove
                List<Pawn> pawnsToRemove = new List<Pawn>();
                foreach (var entry in caughtCheatersCooldowns)
                {
                    // If the cooldown has expired, mark for removal
                    if (Find.TickManager.TicksGame > entry.Value)
                    {
                        pawnsToRemove.Add(entry.Key);
                    }
                }

                // Remove the expired entries
                foreach (Pawn p in pawnsToRemove)
                {
                    caughtCheatersCooldowns.Remove(p);
                }
            }
        }
        
        private static void HandleCheatingEvent(Pawn angryPartner, Pawn cheater, Pawn datePartner)
        {
            // Show a top screen notice message when cheating is discovered
            string message = string.Format("{0} caught {1} cheating with {2}!", 
                angryPartner.LabelShort, cheater.LabelShort, datePartner.LabelShort);
            Messages.Message(message, new LookTargets(angryPartner, cheater), MessageTypeDefOf.NegativeEvent);

            // Create the exclamation mote when the pawn catches their partner cheating
            try
            {
                MoteMaker.MakeColonistActionOverlay(angryPartner, ThingDefOf.Mote_ColonistFleeing);
            }
            catch (System.Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] HandleCheatingEvent: Exception while creating exclamation mote for pawn {0}: {1}", angryPartner.LabelShort, ex.Message));
            }

            // Store the date partner for the cheater so it can be retrieved later
            SocialInteractions.CheaterPartners[cheater.ThingID] = datePartner;

            // Ensure the angry partner goes to the cheater
            if (angryPartner.Spawned && cheater.Spawned && angryPartner.Map == cheater.Map)
            {
                // Create a job for the angry partner to go to the cheater
                Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, cheater);
                gotoJob.checkOverrideOnExpire = false;
                gotoJob.expiryInterval = 300; // 5 seconds
                gotoJob.collideWithPawns = true;
                
                // Start the job with InterruptForced to ensure it interrupts current activities
                angryPartner.jobs.StartJob(gotoJob, JobCondition.InterruptForced);
            }

            // Create a job to handle the interaction once the angry partner arrives
            Job followUpJob = JobMaker.MakeJob(SI_JobDefOf.CaughtCheatingInteraction, cheater);
            angryPartner.jobs.jobQueue.EnqueueFirst(followUpJob);
        }
        
        private static string GetRelationshipLabel(Pawn pawn, Pawn partner)
        {
            if (pawn.relations == null || partner == null)
                return "partner";
                
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Spouse, partner))
                return "spouse";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Fiance, partner))
                return "fiancee";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Lover, partner))
                return "lover";
                
            return "partner";
        }
        
        public static void HoldPawnInPlace(Pawn pawn, IntVec3 position)
        {
            if (pawn != null && pawn.jobs != null)
            {
                // Create a job that keeps the pawn in place
                Job holdJob = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture, position);
                holdJob.expiryInterval = 1800; // 30 seconds
                holdJob.canBashDoors = false;
                holdJob.canBashFences = false;
                holdJob.checkOverrideOnExpire = false;
                holdJob.playerForced = true; // Make it a forced job so it can interrupt other jobs
                
                // Start the job with InterruptForced to ensure it interrupts current activities
                pawn.jobs.StartJob(holdJob, JobCondition.InterruptForced);
            }
        }
    }
}