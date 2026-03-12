using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class Dating_MapComponent : MapComponent
    {
        // Dictionary to track when SI_Naked hediff was added to a pawn
        private Dictionary<Pawn, int> siNakedHediffAddedTicks = new Dictionary<Pawn, int>();
        
        public Dating_MapComponent(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            // Check for pawns with the SI_Naked hediff that are not in the lovin' job
            foreach (Pawn pawn in map.mapPawns.AllPawns)
            {
                if (pawn.health.hediffSet.HasHediff(HediffDef.Named("SI_Naked")))
                {
                    // Check if the pawn is on a date and in the Lovin stage
                    bool shouldKeepHediff = false;
                    if (DatingManager.IsOnDate(pawn))
                    {
                        Date date = DatingManager.GetDateWith(pawn);
                        if (date != null && date.Stage == DateStage.Lovin)
                        {
                            shouldKeepHediff = true;
                        }
                        // Also keep the hediff if this is a 3p action
                        else if (date != null && date.IsThreewayAction)
                        {
                            shouldKeepHediff = true;
                        }
                    }
                    
                    // Also keep the hediff if the pawn is in a JobDriver_CaughtCheating job
                    // This handles the 3p action scenario where the spouse joins in
                    if (!shouldKeepHediff && pawn.jobs != null && pawn.jobs.curDriver != null)
                    {
                        string driverName = pawn.jobs.curDriver.GetType().Name;
                        if (driverName == "JobDriver_CaughtCheating")
                        {
                            shouldKeepHediff = true;
                        }
                    }
                    
                    // Additional check for 3p action participants
                    // If a pawn has the SI_Naked hediff, they might be part of a 3p action
                    // even if they are not yet "on a date" in the traditional sense
                    if (!shouldKeepHediff)
                    {
                        // Check if there's a date happening nearby with IsThreewayAction set to true
                        foreach (Date date in DatingManager.GetAllDates())
                        {
                            if (date != null && date.IsThreewayAction)
                            {
                                // Check if this pawn is near one of the date participants
                                if ((date.Initiator != null && pawn.Position.DistanceTo(date.Initiator.Position) <= 10f) ||
                                    (date.Partner != null && pawn.Position.DistanceTo(date.Partner.Position) <= 10f))
                                {
                                    shouldKeepHediff = true;
                                    break;
                                }
                            }
                        }
                    }
                    
                    // Grace period for pawns with SI_Naked hediff
                    // If a pawn just got the hediff, give it a few ticks to get into the right job
                    if (!shouldKeepHediff)
                    {
                        int hediffAddedTick;
                        if (siNakedHediffAddedTicks.TryGetValue(pawn, out hediffAddedTick))
                        {
                            // Give 60 ticks (1 second) grace period
                            if (Find.TickManager.TicksGame - hediffAddedTick < 60)
                            {
                                shouldKeepHediff = true;
                            }
                        }
                    }
                    
                    // Only remove the hediff if the pawn is not on a date in the Lovin stage
                    // and is not currently in the DateLovin job or JobDriver_CaughtCheating job
                    // and is not part of a 3p action
                    // and the grace period has expired
                    if (!shouldKeepHediff && (pawn.jobs == null || pawn.jobs.curDriver == null || 
                       (pawn.jobs.curDriver.GetType().Name != "JobDriver_DateLovin" && 
                        pawn.jobs.curDriver.GetType().Name != "JobDriver_AbusiveThreesome" &&
                        pawn.jobs.curDriver.GetType().Name != "JobDriver_AbusiveThreesomeParticipant" &&
                        pawn.jobs.curDriver.GetType().Name != "JobDriver_PesterPrisoner" &&
                        pawn.jobs.curDriver.GetType().Name != "JobDriver_PesterPrisonerPartner" &&
                        pawn.jobs.curDriver.GetType().Name != "JobDriver_CaughtCheating")))
                    {
                        SLog.Message(string.Format("[SocialInteractions] Found pawn {0} with SI_Naked hediff but not in lovin' job. Removing hediff.", pawn.LabelShort));
                        Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("SI_Naked"));
                        if (hediff != null)
                        {
                            pawn.health.RemoveHediff(hediff);
                            // Remove from the dictionary when hediff is removed
                            siNakedHediffAddedTicks.Remove(pawn);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Records when a pawn was given the SI_Naked hediff.
        /// </summary>
        /// <param name="pawn">The pawn who received the hediff.</param>
        public void RecordSINakedHediffAdded(Pawn pawn)
        {
            siNakedHediffAddedTicks[pawn] = Find.TickManager.TicksGame;
            SLog.Message(string.Format("[SocialInteractions] Recorded SI_Naked hediff added to {0} at tick {1}.", pawn.LabelShort, Find.TickManager.TicksGame));
        }
    }
}