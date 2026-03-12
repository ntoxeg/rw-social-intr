using System;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse.AI;
using UnityEngine;

namespace SocialInteractions
{
    public enum DateStage
    {
        Joy,
        Lovin,
        Finished
    }

    public class Date : IExposable
    {
        public Pawn Initiator;
        public Pawn Partner;
        public DateStage Stage;
        public int StageTransitionTick; // Track when the stage transition happened
        public bool ReachedLovinStage; // Track whether the date reached the lovin stage
        public bool IsThreewayAction; // Flag to indicate if this is a 3p action

        public Date()
        {
            // Default constructor for deserialization
        }

        public Date(Pawn initiator, Pawn partner)
        {
            this.Initiator = initiator;
            this.Partner = partner;
            this.Stage = DateStage.Joy;
            this.StageTransitionTick = 0;
            this.ReachedLovinStage = false;
            this.IsThreewayAction = false;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref Initiator, "initiator");
            Scribe_References.Look(ref Partner, "partner");
            Scribe_Values.Look(ref Stage, "stage", DateStage.Joy);
            Scribe_Values.Look(ref StageTransitionTick, "stageTransitionTick", 0);
            Scribe_Values.Look(ref ReachedLovinStage, "reachedLovinStage", false);
            Scribe_Values.Look(ref IsThreewayAction, "isThreewayAction", false);
        }
    }

    public static class DatingManager
    {
        private static List<Date> dates = new List<Date>();
        private static readonly object datesLock = new object();
        private static Dictionary<int, int> dateCooldowns = new Dictionary<int, int>();
        // private const int DateCooldownTicks = 300; // 5 min (now configurable in settings)

        // Property to track if a date stage was advanced by a job (prevents double advancement)
        private static bool _wasDateStageAdvancedByJob = false;
        public static bool WasDateStageAdvancedByJob { get { return _wasDateStageAdvancedByJob; } set { _wasDateStageAdvancedByJob = value; } }

        /// <summary>
        /// Gets a copy of all current dates (thread-safe)
        /// </summary>
        /// <returns>A list of all current dates</returns>
        public static List<Date> GetAllDates()
        {
            lock (datesLock)
            {
                return new List<Date>(dates);
            }
        }

        /// <summary>
        /// Expose data for serialization/deserialization
        /// </summary>
        public static void ExposeData()
        {
            Scribe_Collections.Look(ref dates, "dates", LookMode.Deep);
            Scribe_Collections.Look(ref dateCooldowns, "dateCooldowns", LookMode.Value, LookMode.Value);
        }

        public static void StartDate(Pawn initiator, Pawn partner)
        {
            lock (datesLock)
            {
                if (initiator == null || partner == null) return;
                
                // Fail-safe: Don't start a date if either is already on one
                if (IsOnDate(initiator))
                {
                    SLog.Warning(string.Format("[SocialInteractions] StartDate: Aborting. Initiator {0} is already on a date.", initiator.LabelShort));
                    return;
                }
                if (IsOnDate(partner))
                {
                    SLog.Warning(string.Format("[SocialInteractions] StartDate: Aborting. Partner {0} is already on a date.", partner.LabelShort));
                    return;
                }

                if (initiator.health != null) initiator.health.AddHediff(SI_HediffDefOf.OnDate);
                // SLog.Message(string.Format("[SocialInteractions] StartDate: Applied OnDate hediff to initiator {0}.", initiator.LabelShort));
                
                if (partner.health != null) partner.health.AddHediff(SI_HediffDefOf.OnDate);
                // SLog.Message(string.Format("[SocialInteractions] StartDate: Applied OnDate hediff to partner {0}.", partner.LabelShort));

                // Add to active dates
                dates.Add(new Date(initiator, partner));
                SLog.Message(string.Format("[SocialInteractions] StartDate: Date started between {0} and {1}.", initiator.LabelShort, partner.LabelShort));
            }
        }

        public static void RejectDate(Pawn initiator, Pawn partner)
        {
            // Remove the OnDate hediff if it was added
            HediffDef onDateHediffDef = HediffDef.Named("OnDate");
            if (onDateHediffDef != null)
            {
                if (initiator != null && initiator.health != null)
                {
                    Hediff initiatorHediff = initiator.health.hediffSet.GetFirstHediffOfDef(onDateHediffDef);
                    if (initiatorHediff != null)
                    {
                        initiator.health.RemoveHediff(initiatorHediff);
                    }
                }
                
                if (partner != null && partner.health != null)
                {
                    Hediff partnerHediff = partner.health.hediffSet.GetFirstHediffOfDef(onDateHediffDef);
                    if (partnerHediff != null)
                    {
                        partner.health.RemoveHediff(partnerHediff);
                    }
                }
            }
            
            // Add cooldown to prevent immediate re-invitation
            if (initiator != null && partner != null)
            {
                int expiryTick = Find.TickManager.TicksGame + SocialInteractions.Settings.dateCooldownTicks;
                dateCooldowns[initiator.thingIDNumber] = expiryTick;
                dateCooldowns[partner.thingIDNumber] = expiryTick;
            }
        }

        public static void EndDate(Date date)
        {
            if (date == null) return;
            string initiatorLabel = (date.Initiator != null) ? date.Initiator.LabelShort : "NULL";
            string partnerLabel = (date.Partner != null) ? date.Partner.LabelShort : "NULL";
            SLog.Message(string.Format("[SocialInteractions] EndDate called for date between {0} and {1}", initiatorLabel, partnerLabel));
            lock (datesLock)
            {
                if (date == null) 
                {
                    SLog.Warning("[SocialInteractions] DatingManager.EndDate called with null date.");
                    return; 
                }

                // Add null checks for initiator and partner
                if (date.Initiator == null && date.Partner == null)
                {
                    SLog.Warning("[SocialInteractions] DatingManager.EndDate called with date that has null initiator and partner.");
                    return;
                }

                // Post-lovin LLM call is now handled in JobDriver_DateLovin.cs

                // Remove the date from the list first to prevent race conditions
                if (!dates.Remove(date))
                {
                    // If the date was already removed, do nothing further.
                    return;
                }

                // Add cooldown for non-null pawns
                int expiryTick = Find.TickManager.TicksGame + SocialInteractions.Settings.dateCooldownTicks;
                if (date.Initiator != null)
                    dateCooldowns[date.Initiator.thingIDNumber] = expiryTick;
                if (date.Partner != null)
                    dateCooldowns[date.Partner.thingIDNumber] = expiryTick;

                // Explicitly end both pawns' jobs if they're still on DateLovin jobs
                // But only if they're not already being ended by the waitForPartnerToil timeout
                JobDef dateLovinJobDef = SI_JobDefOf.DateLovin;
                if (date.Initiator != null && date.Initiator.jobs != null && date.Initiator.CurJobDef == dateLovinJobDef)
                {
                    try
                    {
                        // Check if the job driver is still the DateLovin driver
                        if (date.Initiator.jobs.curDriver is JobDriver_DateLovin)
                        {
                            // Don't end the job immediately, let it finish naturally
                            // Instead, we'll mark that the date has ended and let the job handle it
                        }
                        else
                        {
                            // If the job driver has changed, just handle it
                        }
                    }
                    catch (Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] Exception checking DateLovin job for initiator {0}: {1}", initiatorLabel, ex.Message));
                    }
                }
                if (date.Partner != null && date.Partner.jobs != null && date.Partner.CurJobDef == dateLovinJobDef)
                {
                    try
                    {
                        // Check if the job driver is still the DateLovin driver
                        if (date.Partner.jobs.curDriver is JobDriver_DateLovin)
                        {
                            // Don't end the job immediately, let it finish naturally
                            // Instead, we'll mark that the date has ended and let the job handle it
                        }
                        else
                        {
                            // If the job driver has changed, just handle it
                        }
                    }
                    catch (Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] Exception checking DateLovin job for partner {0}: {1}", partnerLabel, ex.Message));
                    }
                }

                // Remove hediffs
                HediffDef onDateDef = HediffDef.Named("OnDate");
                if (onDateDef != null)
                {
                    if (date.Initiator != null && date.Initiator.health != null && date.Initiator.health.hediffSet != null)
                    {
                        try
                        {
                            Hediff hediffInitiator = date.Initiator.health.hediffSet.GetFirstHediffOfDef(onDateDef);
                            if (hediffInitiator != null) 
                            {
                                date.Initiator.health.RemoveHediff(hediffInitiator);
                                SLog.Message(string.Format("[SocialInteractions] EndDate: Removed OnDate hediff from initiator {0}.", initiatorLabel));
                            }
                        }
                        catch (Exception ex)
                        {
                            SLog.Warning(string.Format("[SocialInteractions] Exception removing OnDate hediff from initiator {0}: {1}", initiatorLabel, ex.Message));
                        }
                    }

                    if (date.Partner != null && date.Partner.health != null && date.Partner.health.hediffSet != null)
                    {
                        try
                        {
                            Hediff hediffPartner = date.Partner.health.hediffSet.GetFirstHediffOfDef(onDateDef);
                            if (hediffPartner != null) 
                            {
                                date.Partner.health.RemoveHediff(hediffPartner);
                                SLog.Message(string.Format("[SocialInteractions] EndDate: Removed OnDate hediff from partner {0}.", partnerLabel));
                            }
                        }
                        catch (Exception ex)
                        {
                            SLog.Warning(string.Format("[SocialInteractions] Exception removing OnDate hediff from partner {0}: {1}", partnerLabel, ex.Message));
                        }
                    }
                }
                
                // --- Also remove SI_Naked hediffs to ensure clean state ---
                HediffDef siNakedDef = HediffDef.Named("SI_Naked");
                if (siNakedDef != null)
                {
                    // Remove from initiator
                    if (date.Initiator != null && date.Initiator.health != null && date.Initiator.health.hediffSet != null)
                    {
                        try
                        {
                            Hediff nakedHediffInitiator = date.Initiator.health.hediffSet.GetFirstHediffOfDef(siNakedDef);
                            if (nakedHediffInitiator != null) 
                            {
                                date.Initiator.health.RemoveHediff(nakedHediffInitiator);
                            }
                        }
                        catch (Exception ex)
                        {
                            SLog.Warning(string.Format("[SocialInteractions] Exception removing SI_Naked hediff from initiator {0} in EndDate: {1}", initiatorLabel, ex.Message));
                        }
                    }

                    // Remove from partner
                    if (date.Partner != null && date.Partner.health != null && date.Partner.health.hediffSet != null)
                    {
                        try
                        {
                            Hediff nakedHediffPartner = date.Partner.health.hediffSet.GetFirstHediffOfDef(siNakedDef);
                            if (nakedHediffPartner != null) 
                            {
                            }
                        }
                        catch (Exception ex)
                        {
                            SLog.Warning(string.Format("[SocialInteractions] Exception removing SI_Naked hediff from partner {0} in EndDate: {1}", partnerLabel, ex.Message));
                        }
                    }
                }
                // --- End Also remove SI_Naked hediffs ---
                
                // Also end any FollowAndWatch jobs
                JobDef followAndWatchJobDef = SI_JobDefOf.FollowAndWatchInitiator;
                if (date.Initiator != null && date.Initiator.jobs != null && date.Initiator.CurJobDef == followAndWatchJobDef)
                {
                    try
                    {
                        date.Initiator.jobs.EndCurrentJob(JobCondition.Succeeded);
                    }
                    catch (Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] Exception ending FollowAndWatch job for initiator {0}: {1}", initiatorLabel, ex.Message));
                    }
                }
                if (date.Partner != null && date.Partner.jobs != null && date.Partner.CurJobDef == followAndWatchJobDef)
                {
                    try
                    {
                        date.Partner.jobs.EndCurrentJob(JobCondition.Succeeded);
                    }
                    catch (Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] Exception ending FollowAndWatch job for partner {0}: {1}", partnerLabel, ex.Message));
                    }
                }
            }
        }

        private static Date GetDateWith_Unlocked(Pawn pawn)
        {
            Date foundDate = dates.FirstOrDefault(d => d.Initiator == pawn || d.Partner == pawn);
            // Reduce log spam by commenting out this message
            // SLog.Message(string.Format("[SocialInteractions] GetDateWith_Unlocked: Found date for pawn {0}: {1}", 
            //     pawn != null ? pawn.LabelShort : "NULL", 
            //     foundDate != null ? "YES" : "NO"));
            return foundDate;
        }

        public static bool IsOnDate(Pawn pawn)
        {
            if (pawn == null) 
            {
                return false;
            }
            
            if (pawn.health == null || pawn.health.hediffSet == null) 
            {
                return false;
            }
            
            HediffDef onDateDef = SI_HediffDefOf.OnDate;
            if (onDateDef == null) 
            {
                // Fallback attempt if DefOf fails
                onDateDef = HediffDef.Named("OnDate");
            }

            if (onDateDef == null)
            {
                return false;
            }
            
            bool hasHediff = pawn.health.hediffSet.HasHediff(onDateDef);
            return hasHediff;
        }

        public static Date GetDateWith(Pawn pawn)
        {
            lock (datesLock)
            {
                return GetDateWith_Unlocked(pawn);
            }
        }

                public static Pawn GetPartnerOfDateWith(Pawn pawn)
        {
            if (pawn == null) return null;

            lock (datesLock)
            {
                foreach (Date date in dates)
                {
                    if (date.Initiator == pawn)
                    {
                        // Reduce log spam by commenting out these messages
                        // SLog.Message(string.Format("[SocialInteractions] GetPartnerOfDateWith: Found partner {0} for initiator {1}", 
                        //     date.Partner != null ? date.Partner.LabelShort : "NULL", 
                        //     pawn.LabelShort != null ? pawn.LabelShort : "NULL"));
                        return date.Partner;
                    }
                    else if (date.Partner == pawn)
                    {
                        // Reduce log spam by commenting out these messages
                        // SLog.Message(string.Format("[SocialInteractions] GetPartnerOfDateWith: Found initiator {0} for partner {1}", 
                        //     date.Initiator != null ? date.Initiator.LabelShort : "NULL", 
                        //     pawn.LabelShort != null ? pawn.LabelShort : "NULL"));
                        return date.Initiator;
                    }
                }
            }
            // Reduce log spam by commenting out this message
            // SLog.Message(string.Format("[SocialInteractions] GetPartnerOfDateWith: No date found for pawn {0}", 
            //     pawn.LabelShort != null ? pawn.LabelShort : "NULL"));
            return null;
        }

        public static Pawn GetInitiatorOfDateWith(Pawn pawn)
        {
            lock (datesLock)
            {
                if (pawn == null) return null;
                Date date = GetDateWith_Unlocked(pawn);
                if (date != null)
                {
                    return date.Initiator;
                }
                return null;
            }
        }

        public static bool IsOnDateCooldown(Pawn pawn)
        {
            if (pawn == null) return true;
            lock (datesLock)
            {
                int expiryTick;
                if (dateCooldowns.TryGetValue(pawn.thingIDNumber, out expiryTick))
                {
                    bool onCooldown = Find.TickManager.TicksGame < expiryTick;
                    if (onCooldown)
                    {
                        return true;
                    }
                    else
                    {
                        dateCooldowns.Remove(pawn.thingIDNumber);
                        return false;
                    }
                }
                return false;
            }
        }

        public static void CleanupExpiredDateCooldowns()
        {
            lock (datesLock)
            {
                List<int> expiredKeys = new List<int>();
                int currentTick = Find.TickManager.TicksGame;
                
                foreach (var kvp in dateCooldowns)
                {
                    if (currentTick >= kvp.Value)
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }
                
                foreach (int key in expiredKeys)
                {
                    dateCooldowns.Remove(key);
                }
            }
        }

        public static void CheckForStuckDates(Map map)
        {
            if (map == null || map.mapPawns == null) return;
            
            // Check for stuck dates more frequently - every 60 ticks (1 second) instead of every 180 ticks
            if (Current.Game.tickManager.TicksGame % 60 != 0) return;
            
            JobDef dateLovinJobDef = SI_JobDefOf.DateLovin;
            JobDef goOnDateJobDef = SI_JobDefOf.GoOnDate;
            JobDef waitMaintainPostureJobDef = JobDefOf.Wait_MaintainPosture; // Add this for cheating events
            
            // Get a snapshot of all pawns to avoid modification during iteration
            List<Pawn> allPawns = new List<Pawn>(map.mapPawns.AllPawns);
            
            foreach (Pawn pawn in allPawns)
            {
                if (IsOnDate(pawn))
                {
                    Pawn initiator = GetInitiatorOfDateWith(pawn);
                    Date date = GetDateWith(pawn);
                    
                    // If we can't find a valid initiator or date, end the date
                    if (initiator == null || date == null)
                    {
                        if (date != null)
                        {
                            EndDate(date);
                        }
                        continue;
                    }
                    
                    // Check for critical interruptions that should end the date immediately
                    bool initiatorCritical = (initiator == null || initiator.Dead || initiator.Downed || initiator.Drafted || 
                                              initiator.InMentalState || !initiator.Spawned || initiator.Destroyed);
                    bool partnerCritical = (date.Partner == null || date.Partner.Dead || date.Partner.Downed || date.Partner.Drafted || 
                                            date.Partner.InMentalState || !date.Partner.Spawned || date.Partner.Destroyed);
                    
                    if (initiatorCritical || partnerCritical)
                    {
                        SLog.Message(string.Format("[SocialInteractions] CheckForStuckDates: Ending date due to critical interruption. initiatorCritical: {0}, partnerCritical: {1}", initiatorCritical, partnerCritical));
                        EndDate(date);
                        continue;
                    }
                    
                    // Handle different date stages differently
                    if (date.Stage == DateStage.Joy)
                    {
                        // For Joy stage, check if the initiator is doing a joy job
                        bool isDoingJoyJob = false;
                        if (initiator != null && initiator.CurJob != null)
                        {
                            // Check if the job is a joy job
                            foreach (JoyGiverDef joyGiver in DefDatabase<JoyGiverDef>.AllDefs)
                            {
                                if (joyGiver.jobDef == initiator.CurJob.def)
                                {
                                    isDoingJoyJob = true;
                                    break;
                                }
                            }
                        }
                        
                        // If the initiator is doing a joy job, DateLovin job, GoOnDate job, or Wait_MaintainPosture job, the date is not stuck
                        // Also, if the initiator is on a path to a joy job or DateLovin job, the date is not stuck
                        if (initiator != null && initiator.jobs != null && initiator.CurJob != null)
                        {
                            if (isDoingJoyJob || initiator.CurJobDef == dateLovinJobDef || initiator.CurJobDef == goOnDateJobDef || 
                                initiator.CurJobDef == waitMaintainPostureJobDef || initiator.CurJobDef == SI_JobDefOf.SocialRelaxDate ||
                                initiator.CurJobDef == SI_JobDefOf.PesterPrisoner || initiator.CurJobDef == SI_JobDefOf.AbusiveThreesome) // Include PesterPrisoner and AbusiveThreesome
                            {
                                // Date is not stuck
                                continue;
                            }
                            
                            // Check if the initiator is pathing to a joy job, DateLovin job, or SocialRelaxDate
                            if (initiator.pather != null && initiator.pather.curPath != null && !initiator.pather.curPath.NodesLeftCount.Equals(0))
                            {
                                // Initiator is still pathing, so the date is not stuck
                                continue;
                            }
                        }
                        
                        // If we can't find a valid initiator or the initiator is not doing a joy job, DateLovin job, GoOnDate job, Wait_MaintainPosture job, or SocialRelaxDate,
                        // and they're not pathing to one, advance the date
                        if (initiator == null || initiator.jobs == null || initiator.CurJob == null || 
                            (!isDoingJoyJob && initiator.CurJobDef.defName != "DateLovin" && initiator.CurJobDef.defName != "GoOnDate" && 
                             initiator.CurJobDef.defName != "Wait_MaintainPosture" && initiator.CurJobDef.defName != "SocialRelaxDate" &&
                             initiator.CurJobDef.defName != "PesterPrisoner" && initiator.CurJobDef.defName != "AbusiveThreesome"))
                        {
                            SLog.Message(string.Format("[SocialInteractions] CheckForStuckDates: Advancing date for {0}. isDoingJoyJob: {1}, CurJobDef: {2}", 
                                initiator != null ? initiator.LabelShort : "NULL", 
                                isDoingJoyJob, 
                                (initiator != null && initiator.CurJob != null) ? initiator.CurJob.def.defName : "NULL"));
                            AdvanceDateStage(pawn);
                        }
                    }
                    else if (date.Stage == DateStage.Lovin)
                    {
                        // For Lovin stage, check if both pawns are doing DateLovin jobs or temporary jobs like LayDown
                        // If this is a 3p action, don't end the date
                        if (date.IsThreewayAction)
                        {
                            // For 3p actions, we don't want to end the date automatically
                            // The 3p action will handle ending the date when appropriate
                            continue;
                        }
                        
                        bool initiatorInValidJob = (initiator != null && initiator.jobs != null && initiator.CurJob != null) && 
                            (initiator.CurJobDef == dateLovinJobDef || initiator.CurJobDef == waitMaintainPostureJobDef || 
                             initiator.CurJobDef == JobDefOf.LayDown || initiator.CurJobDef == SI_JobDefOf.AbusiveThreesome);
                        bool partnerInValidJob = (date.Partner != null && date.Partner.jobs != null && date.Partner.CurJob != null) && 
                            (date.Partner.CurJobDef == dateLovinJobDef || date.Partner.CurJobDef == waitMaintainPostureJobDef || 
                             date.Partner.CurJobDef == JobDefOf.LayDown || date.Partner.CurJobDef == SI_JobDefOf.AbusiveThreesome);
                        
                        // If either pawn is not in a valid job for the Lovin stage, end the date immediately
                        if (!initiatorInValidJob || !partnerInValidJob)
                        {
                            SLog.Message(string.Format("[SocialInteractions] CheckForStuckDates: Ending date in Lovin stage. initiatorInValidJob: {0}, partnerInValidJob: {1}", initiatorInValidJob, partnerInValidJob));
                            EndDate(date);
                        }
                    }
                }
            }
        }

        public static void AdvanceDateStage(Pawn pawn)
        {
            // Reduce log spam by commenting out this message
            // SLog.Message(string.Format("[SocialInteractions] DatingManager.AdvanceDateStage called for pawn {0}", 
            //     pawn != null ? (pawn.LabelShort != null ? pawn.LabelShort : "NULL") : "NULL"));
            
            lock (datesLock)
            {
                if (pawn == null) 
                {
                    // Reduce log spam by commenting out this message
                    // SLog.Message("[SocialInteractions] DatingManager.AdvanceDateStage: pawn is null, returning");
                    return;
                }
                
                Date date = GetDateWith_Unlocked(pawn);
                if (date != null)
                {
                    if (date.Stage == DateStage.Finished) 
                    {
                        // Reduce log spam by commenting out this message
                        // SLog.Message(string.Format("[SocialInteractions] AdvanceDateStage: Date for {0} and {1} is already finished. No action taken.", 
                        //     date.Initiator != null ? (date.Initiator.LabelShort != null ? date.Initiator.LabelShort : "NULL") : "NULL", 
                        //     date.Partner != null ? (date.Partner.LabelShort != null ? date.Partner.LabelShort : "NULL") : "NULL"));
                        return;
                    }

                    date.Stage++;
                    // Reset the stage transition tick when advancing the stage
                    date.StageTransitionTick = 0;
                    SLog.Message(string.Format("[SocialInteractions] AdvanceDateStage: Advancing date stage for {0} and {1}. New stage: {2}", 
                        date.Initiator != null ? (date.Initiator.LabelShort != null ? date.Initiator.LabelShort : "NULL") : "NULL", 
                        date.Partner != null ? (date.Partner.LabelShort != null ? date.Partner.LabelShort : "NULL") : "NULL", 
                        date.Stage));
                    HandleDateStage(date);
                }
                else
                {
                    // Reduce log spam by commenting out this message
                    // SLog.Warning(string.Format("[SocialInteractions] AdvanceDateStage: Called for pawn {0} who is not on a date.", 
                    //     pawn.LabelShort != null ? pawn.LabelShort : "NULL"));
                }
            }
        }

        /// <summary>
        /// Calculates the chance the date goes badly based on the lowest mood of the two pawns.
        /// Lower mood = higher chance of bad date.
        /// </summary>
        private static float CalculateBadDateChance(Date date)
        {
            if (date == null || date.Initiator == null || date.Partner == null)
                return 0f;

            float initiatorMood = 1.0f;
            float partnerMood = 1.0f;

            // Get initiator mood safely
            if (date.Initiator.needs != null && date.Initiator.needs.mood != null)
            {
                initiatorMood = date.Initiator.needs.mood.CurLevelPercentage;
            }

            // Get partner mood safely
            if (date.Partner.needs != null && date.Partner.needs.mood != null)
            {
                partnerMood = date.Partner.needs.mood.CurLevelPercentage;
            }

            // Use the lowest mood between the two pawns
            float lowestMood = Mathf.Min(initiatorMood, partnerMood);

            // Calculate bad date chance: lower mood = higher chance
            // At 0% mood: 50% chance of bad date
            // At 50% mood: 25% chance of bad date
            // At 100% mood: 0% chance of bad date
            float badDateChance = (1.0f - lowestMood) * 0.5f;

            return badDateChance;
        }

        private static void HandleDateStage(Date date)
        {
            switch (date.Stage)
            {
                case DateStage.Lovin:
                    // Set the stage transition tick when transitioning to Lovin stage
                    date.StageTransitionTick = Current.Game.tickManager.TicksGame;
                    // Note: We don't set ReachedLovinStage = true here because the transition might fail
                    // Instead, we'll set it in TransitionToLovin when the transition is successful
                    TransitionToLovin(date);
                    break;
                case DateStage.Finished:
                    // First check if the date went badly based on mood
                    float badDateChance = CalculateBadDateChance(date);
                    bool dateWentBadly = Rand.Chance(badDateChance);

                    if (dateWentBadly && date.Initiator != null && date.Partner != null && 
                        !date.Initiator.Dead && !date.Partner.Dead)
                    {
                        // Date went badly - apply debuff thoughts to both pawns
                        SLog.Message(string.Format("[SocialInteractions] Date between {0} and {1} went badly (chance was {2:P0})",
                            date.Initiator.LabelShort, date.Partner.LabelShort, badDateChance));

                        // Apply DateWentBadly thought to initiator
                        if (date.Initiator.needs != null && date.Initiator.needs.mood != null && 
                            date.Initiator.needs.mood.thoughts != null && date.Initiator.needs.mood.thoughts.memories != null)
                        {
                            ThoughtDef badDateThought = SI_ThoughtDefOf.DateWentBadly;
                            if (badDateThought != null)
                            {
                                var thought = (Thought_Memory)ThoughtMaker.MakeThought(badDateThought);
                                thought.otherPawn = date.Partner;
                                date.Initiator.needs.mood.thoughts.memories.TryGainMemory(thought, null);
                            }
                        }

                        // Apply DateWentBadly thought to partner
                        if (date.Partner.needs != null && date.Partner.needs.mood != null && 
                            date.Partner.needs.mood.thoughts != null && date.Partner.needs.mood.thoughts.memories != null)
                        {
                            ThoughtDef badDateThought = SI_ThoughtDefOf.DateWentBadly;
                            if (badDateThought != null)
                            {
                                var thought = (Thought_Memory)ThoughtMaker.MakeThought(badDateThought);
                                thought.otherPawn = date.Initiator;
                                date.Partner.needs.mood.thoughts.memories.TryGainMemory(thought, null);
                            }
                        }

                        // Trigger LLM interaction for bad date
                        SocialInteractions.HandleNonStoppingInteraction(date.Initiator, date.Partner, SI_InteractionDefOf.DateLovin, 
                            SpeechBubbleManager.GetDateWentBadlySubject(date.Initiator, date.Partner), true);

                        // End the date without giving positive buffs
                        EndDate(date);
                        break;
                    }

                    // Date went well - continue with normal positive outcomes
                    // Give "Got some lovin" thoughts to both pawns only if the date actually reached the lovin stage
                    if (date.Initiator != null && date.Partner != null && date.ReachedLovinStage)
                    {
                        // Give thought to initiator
                        if (date.Initiator.needs != null && date.Initiator.needs.mood != null && date.Initiator.needs.mood.thoughts != null && date.Initiator.needs.mood.thoughts.memories != null)
                        {
                            var thought = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.GotSomeLovin);
                            thought.otherPawn = date.Partner;
                            date.Initiator.needs.mood.thoughts.memories.TryGainMemory(thought, null);
                        }

                        // Give thought to partner
                        if (date.Partner.needs != null && date.Partner.needs.mood != null && date.Partner.needs.mood.thoughts != null && date.Partner.needs.mood.thoughts.memories != null)
                        {
                            var thought = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.GotSomeLovin);
                            thought.otherPawn = date.Initiator;
                            date.Partner.needs.mood.thoughts.memories.TryGainMemory(thought, null);
                        }
                    }
                    
                    // Handle pregnancy only if the date actually reached the lovin stage
                    if (ModsConfig.BiotechActive && date.Initiator != null && date.Partner != null && date.ReachedLovinStage)
                    {
                        Pawn malePawn = ((date.Initiator.gender == Gender.Male) ? date.Initiator : ((date.Partner.gender == Gender.Male) ? date.Partner : null));
                        Pawn femalePawn = ((date.Initiator.gender == Gender.Female) ? date.Initiator : ((date.Partner.gender == Gender.Female) ? date.Partner : null));
                        
                        if (malePawn != null && femalePawn != null)
                        {
                            // Use the same pregnancy chance as vanilla lovin
                            float pregnancyChance = 0.05f;
                            
                            if (Rand.Chance(pregnancyChance * PregnancyUtility.PregnancyChanceForPartners(femalePawn, malePawn)))
                            {
                                bool success;
                                GeneSet inheritedGeneSet = PregnancyUtility.GetInheritedGeneSet(malePawn, femalePawn, out success);
                                if (success)
                                {
                                    Hediff_Pregnant hediff_Pregnant = (Hediff_Pregnant)HediffMaker.MakeHediff(HediffDefOf.PregnantHuman, femalePawn);
                                    hediff_Pregnant.SetParents(null, malePawn, inheritedGeneSet);
                                    femalePawn.health.AddHediff(hediff_Pregnant);
                                }
                                else if (PawnUtility.ShouldSendNotificationAbout(malePawn) || PawnUtility.ShouldSendNotificationAbout(femalePawn))
                                {
                                    Messages.Message("MessagePregnancyFailed".Translate(malePawn.Named("FATHER"), femalePawn.Named("MOTHER")) + ": " + "CombinedGenesExceedMetabolismLimits".Translate(), new LookTargets(malePawn, femalePawn), MessageTypeDefOf.NegativeEvent);
                                }
                            }
                        }
                    }
                    
                    // Make post-lovin LLM call only if the date actually reached the lovin stage
                    if (SocialInteractions.Settings.enableLovin && date.Initiator != null && date.Partner != null && date.ReachedLovinStage)
                    {
                        SocialInteractions.HandleNonStoppingInteraction(date.Initiator, date.Partner, SI_InteractionDefOf.DateLovin, 
                            SpeechBubbleManager.GetPostDateLovinSubject(date.Initiator, date.Partner), true);
                    }

                    // Apply positive social thoughts for successful date completion (for both lovin' and non-lovin' dates)
                    // Only apply if both pawns are valid and not dead
                    if (date.Initiator != null && date.Partner != null && 
                        !date.Initiator.Dead && !date.Partner.Dead)
                    {
                        // Apply positive thoughts to both pawns
                        if (date.Initiator.needs != null && date.Initiator.needs.mood != null && 
                            date.Initiator.needs.mood.thoughts != null && date.Initiator.needs.mood.thoughts.memories != null)
                        {
                            ThoughtDef enjoyedDateThought = SI_ThoughtDefOf.EnjoyedDateWith;
                            if (enjoyedDateThought != null)
                            {
                                var thought = (Thought_Memory)ThoughtMaker.MakeThought(enjoyedDateThought);
                                thought.otherPawn = date.Partner;
                                date.Initiator.needs.mood.thoughts.memories.TryGainMemory(thought, null);
                            }
                        }
                        
                        if (date.Partner.needs != null && date.Partner.needs.mood != null && 
                            date.Partner.needs.mood.thoughts != null && date.Partner.needs.mood.thoughts.memories != null)
                        {
                            ThoughtDef enjoyedDateThought = SI_ThoughtDefOf.EnjoyedDateWith;
                            if (enjoyedDateThought != null)
                            {
                                var thought = (Thought_Memory)ThoughtMaker.MakeThought(enjoyedDateThought);
                                thought.otherPawn = date.Initiator;
                                date.Partner.needs.mood.thoughts.memories.TryGainMemory(thought, null);
                            }
                        }
                    }
                    
                    EndDate(date);
                    break;
            }
        }

        private static void TransitionToLovin(Date date)
        {
            if (date.Initiator == null || date.Partner == null)
            {
                date.Stage = DateStage.Finished;
                HandleDateStage(date);
                return;
            }

            // Ensure the partner's FollowAndWatch job is properly ended before starting the lovin job
            if (date.Partner != null && date.Partner.jobs != null)
            {
                // End any existing FollowAndWatch job
                if (date.Partner.CurJobDef == SI_JobDefOf.FollowAndWatchInitiator)
                {
                    date.Partner.jobs.EndCurrentJob(JobCondition.Succeeded);
                }
                // Also check the job queue for any pending FollowAndWatch jobs
                else if (date.Partner.jobs.jobQueue != null)
                {
                    // Look for any queued FollowAndWatch jobs and clear them by ending the current job
                    // This is a simpler approach that should work with the available methods
                    bool hasQueuedFollowJob = false;
                    foreach (QueuedJob queuedJob in date.Partner.jobs.jobQueue)
                    {
                        if (queuedJob.job != null && queuedJob.job.def == SI_JobDefOf.FollowAndWatchInitiator)
                        {
                            hasQueuedFollowJob = true;
                            break;
                        }
                    }
                    
                    if (hasQueuedFollowJob)
                    {
                        // End the current job to clear the queue
                        date.Partner.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    }
                }
            }

            Building_Bed bed = FindSuitableBedForLovin(date.Initiator, date.Partner);
            if (bed != null)
            {
                // Check if the bed is too far from the initiator
                IntVec3 finalPosition = bed.Position;

                if (date.Initiator != null && date.Initiator.Spawned && bed != null && bed.Spawned)
                {
                    float distanceToBed = (date.Initiator.Position - bed.Position).LengthHorizontal;
                    if (distanceToBed > SocialInteractions.Settings.maxDistanceToLovinSpot)
                    {
                        // Bed is too far, use a random spot near the initiator instead
                        // Find a random valid position near the initiator (within 5 cells)
                        finalPosition = GetRandomValidPositionNear(date.Initiator, 5);
                    }
                }

                // End any existing jobs that might interfere
                if (date.Initiator != null && date.Initiator.jobs != null) date.Initiator.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
                if (date.Partner != null && date.Partner.jobs != null) date.Partner.jobs.EndCurrentJob(JobCondition.InterruptForced, false);

                // Create jobs without reserving the bed - just use its position
                // This allows spouses to potentially catch them in the act
                Job lovinJobInitiator = JobMaker.MakeJob(SI_JobDefOf.DateLovin, date.Partner, finalPosition);
                // Set the job as player-forced to give it higher priority
                lovinJobInitiator.playerForced = true;
                
                date.Initiator.jobs.StartJob(lovinJobInitiator, JobCondition.InterruptForced);

                Job lovinJobPartner = JobMaker.MakeJob(SI_JobDefOf.DateLovin, date.Initiator, finalPosition);
                // Set the job as player-forced to give it higher priority
                lovinJobPartner.playerForced = true;
                
                // For the partner, we want to make sure the job isn't interrupted by other jobs

                // Enqueue the lovin job and then end the current job.
                // This makes the game immediately start our queued job without a chance for the pawn to find other work.
                date.Partner.jobs.ClearQueuedJobs();
                date.Partner.jobs.jobQueue.EnqueueFirst(lovinJobPartner);
                date.Partner.jobs.EndCurrentJob(JobCondition.InterruptForced);

                // Mark that the date successfully reached the lovin stage
                date.ReachedLovinStage = true;

                // Start the LLM interaction for date lovin, skipping spam protection since we're already in a date
                // Only if lovin interactions are enabled in settings
                if (SocialInteractions.Settings.enableLovin)
                {
                    SocialInteractions.HandleNonStoppingInteraction(date.Initiator, date.Partner, SI_InteractionDefOf.DateLovin, SpeechBubbleManager.GetDateLovinSubject(date.Initiator, date.Partner), true);
                }
            }
            else
            {
                date.Stage = DateStage.Finished;
                HandleDateStage(date);
            }
        }

        public static float CalculateDateCompatibility(Pawn pawn1, Pawn pawn2)
        {
            // Start with a base compatibility factor
            float compatibility = 1.0f;

            // Check sexual compatibility first - this is a make-or-break factor
            float sexualCompatibility = CalculateSexualCompatibility(pawn1, pawn2);
            if (sexualCompatibility <= 0f)
            {
                // If they're not sexually compatible, they won't progress to lovin'
                return 0f;
            }
            
            // Apply sexual compatibility as a multiplier
            compatibility *= sexualCompatibility;

            // Add opinion-based compatibility (mutual respect is important for dates)
            float opinion1 = pawn1.relations.OpinionOf(pawn2);
            float opinion2 = pawn2.relations.OpinionOf(pawn1);
            float opinionCompatibility = Mathf.InverseLerp(-100f, 100f, (opinion1 + opinion2) / 2f);
            compatibility *= Mathf.Lerp(0.5f, 1.5f, opinionCompatibility);

            // Consider personality trait compatibility
            if (pawn1.story != null && pawn1.story.traits != null && pawn2.story != null && pawn2.story.traits != null)
            {
                // Positive traits that might increase compatibility
                int positiveTraitMatches = 0;
                int negativeTraitConflicts = 0;

                // Check for matching positive traits
                if (pawn1.story.traits.HasTrait(TraitDefOf.Kind) && pawn2.story.traits.HasTrait(TraitDefOf.Kind))
                    positiveTraitMatches++;
                if (pawn1.story.traits.HasTrait(TraitDefOf.Joyous) && pawn2.story.traits.HasTrait(TraitDefOf.Joyous))
                    positiveTraitMatches++;
                
                // Check for Masochist trait (may be from a mod)
                TraitDef masochistTrait = DefDatabase<TraitDef>.GetNamed("Masochist", false);
                if (masochistTrait != null)
                {
                    if (pawn1.story.traits.HasTrait(TraitDefOf.Brawler) && pawn2.story.traits.HasTrait(masochistTrait))
                        positiveTraitMatches++;
                    if (pawn1.story.traits.HasTrait(masochistTrait) && pawn2.story.traits.HasTrait(TraitDefOf.Brawler))
                        positiveTraitMatches++;
                }

                // Check for conflicting traits
                if (pawn1.story.traits.HasTrait(TraitDefOf.Kind) && pawn2.story.traits.HasTrait(TraitDefOf.Psychopath))
                    negativeTraitConflicts++;
                if (pawn1.story.traits.HasTrait(TraitDefOf.Psychopath) && pawn2.story.traits.HasTrait(TraitDefOf.Kind))
                    negativeTraitConflicts++;
                if (pawn1.story.traits.HasTrait(TraitDefOf.Brawler) && pawn2.story.traits.HasTrait(TraitDefOf.Wimp))
                    negativeTraitConflicts++;

                // Adjust compatibility based on trait matches/conflicts
                compatibility *= (1.0f + positiveTraitMatches * 0.1f); // Up to 1.2x for positive matches
                compatibility *= (1.0f - negativeTraitConflicts * 0.15f); // Down to 0.7x for conflicts
            }

            // Consider age compatibility (similar ages might be more compatible for dates)
            float ageDifference = Math.Abs(pawn1.ageTracker.AgeBiologicalYearsFloat - pawn2.ageTracker.AgeBiologicalYearsFloat);
            float ageCompatibility = Mathf.InverseLerp(20f, 0f, ageDifference); // More compatible with less age difference
            compatibility *= Mathf.Lerp(0.8f, 1.2f, ageCompatibility);

            // Check for blood relations and apply a heavy penalty if they exist
            // In RimWorld, blood relations can happen but should be heavily discouraged
            bool areBloodRelated = pawn1.GetRelations(pawn2).Any(relation => relation.familyByBloodRelation);
            if (areBloodRelated)
            {
                // Apply a heavy penalty for blood relations
                compatibility *= 0.1f; // 90% reduction in compatibility
            }

            // Check for libido compatibility
            if (ModsConfig.BiotechActive)
            {
                // Check if both pawns have genes
                if (pawn1.genes != null && pawn2.genes != null)
                {
                    // Check for libido genes
                    bool pawn1HasHighLibido = pawn1.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Libido_High", false));
                    bool pawn1HasLowLibido = pawn1.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Libido_Low", false));
                    bool pawn2HasHighLibido = pawn2.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Libido_High", false));
                    bool pawn2HasLowLibido = pawn2.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Libido_Low", false));

                    // Apply bonuses/penalties based on libido compatibility
                    if (pawn1HasHighLibido && pawn2HasHighLibido)
                    {
                        // Both have high libido - bonus
                        compatibility *= 1.5f; // 50% bonus
                    }
                    else if (pawn1HasLowLibido && pawn2HasLowLibido)
                    {
                        // Both have low libido - high penalty
                        compatibility *= 0.7f; // 30% penalty
                    }
                    else if ((pawn1HasHighLibido && pawn2HasLowLibido) || (pawn1HasLowLibido && pawn2HasHighLibido))
                    {
                        // Mismatched libido - penalty
                        compatibility *= 0.9f; // 10% penalty
                    }
                }
            }

            // Ensure compatibility stays within reasonable bounds
            compatibility = Mathf.Clamp(compatibility, 0.1f, 3.0f);

            return compatibility;
        }

        // Helper method to calculate sexual compatibility based on vanilla RimWorld logic
        public static float CalculateSexualCompatibility(Pawn pawn1, Pawn pawn2)
        {
            // Check if they're the same pawn
            if (pawn1 == pawn2)
            {
                return 0f;
            }

            // Check if both pawns are humanlike - only humanlike pawns can have romantic relationships
            if (!pawn1.RaceProps.Humanlike || !pawn2.RaceProps.Humanlike)
            {
                return 0f;
            }

            // Check traits for sexual compatibility
            if (pawn1.story != null && pawn1.story.traits != null && pawn2.story != null && pawn2.story.traits != null)
            {
                // Asexual pawns won't engage in lovin'
                if (pawn1.story.traits.HasTrait(TraitDefOf.Asexual) || pawn2.story.traits.HasTrait(TraitDefOf.Asexual))
                {
                    return 0f;
                }

                // Check gender compatibility based on traits
                // For any pawn that is NOT bisexual, their gender preference must be satisfied
                bool pawn1IsGay = pawn1.story.traits.HasTrait(TraitDefOf.Gay);
                bool pawn2IsGay = pawn2.story.traits.HasTrait(TraitDefOf.Gay);
                bool pawn1IsBisexual = pawn1.story.traits.HasTrait(TraitDefOf.Bisexual);
                bool pawn2IsBisexual = pawn2.story.traits.HasTrait(TraitDefOf.Bisexual);

                // If pawn1 is NOT bisexual, their orientation must be satisfied
                if (!pawn1IsBisexual)
                {
                    if (pawn1IsGay)
                    {
                        // pawn1 is gay, so they need same gender as pawn2
                        if (pawn1.gender != pawn2.gender)
                        {
                            return 0f;
                        }
                    }
                    else // pawn1 is straight
                    {
                        // pawn1 is straight, so they need different gender from pawn2
                        if (pawn1.gender == pawn2.gender)
                        {
                            return 0f;
                        }
                    }
                }

                // If pawn2 is NOT bisexual, their orientation must be satisfied
                if (!pawn2IsBisexual)
                {
                    if (pawn2IsGay)
                    {
                        // pawn2 is gay, so they need same gender as pawn1
                        if (pawn1.gender != pawn2.gender)
                        {
                            return 0f;
                        }
                    }
                    else // pawn2 is straight
                    {
                        // pawn2 is straight, so they need different gender from pawn1
                        if (pawn1.gender == pawn2.gender)
                        {
                            return 0f;
                        }
                    }
                }
                // If both are bisexual, any gender combination is acceptable
            }

            // Age check (both must be at least 16)
			if (pawn1.ageTracker.AgeBiologicalYearsFloat < 16f || pawn2.ageTracker.AgeBiologicalYearsFloat < 16f)
            {
                return 0f;
            }

             // If all checks pass, return a positive compatibility factor based on attractiveness
            float pawn1Attractiveness = CalculateAttractiveness(pawn1, pawn2);
            float pawn2Attractiveness = CalculateAttractiveness(pawn2, pawn1);
            
            // Average the attractiveness factors
            return (pawn1Attractiveness + pawn2Attractiveness) / 2f;
        }

        // Helper method to calculate attractiveness factor (similar to vanilla PrettinessFactor)
        public static float CalculateAttractiveness(Pawn observer, Pawn target)
        {
            float beauty = 0f;
            if (target.RaceProps.Humanlike)
            {
                beauty = target.GetStatValue(StatDefOf.PawnBeauty);
            }
			
			// beauty can be from -3 to +3 so we keep the factor between 0.5 and 1.5
			return 1f + (beauty / 6f);
        }

        // Helper method to find a random valid position near a pawn
        public static IntVec3 GetRandomValidPositionNear(Pawn pawn, int maxDistance)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return IntVec3.Invalid;
            }

            // Try up to 20 times to find a valid position
            for (int i = 0; i < 20; i++)
            {
                // Generate random offset within the max distance
                int offsetX = Rand.RangeInclusive(-maxDistance, maxDistance);
                int offsetZ = Rand.RangeInclusive(-maxDistance, maxDistance);
                
                // Calculate the potential position
                IntVec3 potentialPosition = new IntVec3(pawn.Position.x + offsetX, pawn.Position.y, pawn.Position.z + offsetZ);
                
                // Check if the position is valid
                if (potentialPosition.IsValid && potentialPosition.InBounds(pawn.Map) && 
                    potentialPosition.Walkable(pawn.Map) && 
                    !potentialPosition.Impassable(pawn.Map) &&
                    pawn.CanReserveAndReach(potentialPosition, PathEndMode.OnCell, Danger.None))
                {
                    return potentialPosition;
                }
            }
            
            // If we couldn't find a valid position, return the pawn's current position
            return pawn.Position;
        }

        // Helper method to find a suitable bed for lovin'
        public static Building_Bed FindSuitableBedForLovin(Pawn initiator, Pawn partner)
        {
            if (initiator == null || initiator.Map == null || partner == null) 
            {
                return null;
            }

            // Social compatibility and probability check
            float baseChance = SocialInteractions.Settings.baseLovinChance;
            float initiatorMood = 1.0f;
            float partnerMood = 1.0f;
            if (initiator.needs != null && initiator.needs.mood != null)
                initiatorMood = initiator.needs.mood.CurLevel;
            if (partner.needs != null && partner.needs.mood != null)
                partnerMood = partner.needs.mood.CurLevel;

            float moodFactor = (initiatorMood + partnerMood) / 2f;
            float dateCompatibility = CalculateDateCompatibility(initiator, partner);
            float rawChance = baseChance * moodFactor * dateCompatibility;
            float finalChance = Sigmoid(rawChance);

            if (!Rand.Chance(finalChance))
            {
                return null;
            }

            var allBeds = initiator.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().ToList();

            var potentialBeds = allBeds.Where(bed =>
                {
                    if (bed == null || bed.Destroyed || !bed.Spawned) return false;
                    if (!initiator.CanReserveAndReach(bed, PathEndMode.InteractionCell, Danger.None)) return false;
                    if (!partner.CanReserveAndReach(bed, PathEndMode.InteractionCell, Danger.None)) return false;
                    return true;
                });

            // Prioritize owned beds
            Building_Bed ownedBed = potentialBeds.FirstOrDefault(b => b.OwnersForReading.Contains(initiator));
            if (ownedBed != null) 
            {
                return ownedBed;
            }

            // Fallback to any bed
            Building_Bed anyBed = potentialBeds.FirstOrDefault();
            if (anyBed != null) 
            {
                return anyBed;
            }

            return null;
        }

        private static float Sigmoid(float x)
        {
            return x / (1f + Mathf.Abs(x));
        }
    }
}