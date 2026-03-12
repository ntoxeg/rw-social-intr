using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;
using System.Linq;

namespace SocialInteractions
{
    public enum NegotiatedRaidOutcome
    {
        Undefined,
        CriticalSuccess, // Leave immediately
        Positive,        // Agreed to leave (maybe with conditions), logic is to leave peacefully
        Neutral,         // No change (assault)
        Failure          // Attack immediately
    }

    public class LordJob_NegotiatedRaid : LordJob
    {
        private NegotiatedRaidOutcome outcome;
        private Faction faction;
        private IntVec3 gatherSpot = IntVec3.Invalid;
        private int originalGoodwill = -100;
        
        // Settings
        private int lingerDurationTicks = 5000;
        
        public LordJob_NegotiatedRaid() 
        { 
        }

        public LordJob_NegotiatedRaid(Faction faction, NegotiatedRaidOutcome outcome, int originalGoodwill = -100)
        {
            this.faction = faction;
            this.outcome = outcome;
            this.originalGoodwill = originalGoodwill;
            this.lingerDurationTicks = Rand.Range(5000, 30000); // 2h to 12h
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref outcome, "outcome");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref gatherSpot, "gatherSpot");
            Scribe_Values.Look(ref lingerDurationTicks, "lingerDurationTicks", 5000);
            Scribe_Values.Look(ref originalGoodwill, "originalGoodwill", -100);
        }
        
        public override bool AddFleeToil
        {
            get { return false; } // We handle flee manually in fallback graph
        }

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();
            
            if (outcome == NegotiatedRaidOutcome.Positive)
            {
                // Positive: Travel to gather spot -> Linger/Loiter/Plunder -> Steal/Exit
                
                // 1. Travel to Smart Linger Spot
                IntVec3 lingerSpot = gatherSpot;
                if (!lingerSpot.IsValid) lingerSpot = GetSmartLingerSpot(this.Map);
                
                LordToil_Travel travelToil = new LordToil_Travel(lingerSpot);
                graph.AddToil(travelToil);
                
                // 2. Linger (Wander/Eat/Use Tables)
                // Use DefendPoint but with duty that allows wandering/eating
                LordToil_DefendPoint lingerToil = new LordToil_DefendPoint(lingerSpot, 28f); 
                graph.AddToil(lingerToil);
                
                // 3. Exit (Steal is handled by opportunistic behavior or we can add explicit Steal toil)
                // Explicit Steal toil ensures they try to take stuff before leaving
                LordToil_Plunder stealToil = new LordToil_Plunder();
                graph.AddToil(stealToil);
                
                LordToil_ExitMap exitToil = new LordToil_ExitMap(LocomotionUrgency.Jog, true, true);
                graph.AddToil(exitToil);
                
                // Transitions
                
                // Travel -> Linger (Upon Reaching Dest)
                Transition travelToLinger = new Transition(travelToil, lingerToil);
                travelToLinger.AddTrigger(new Trigger_PawnHarmed(0.5f, true, null)); // Safety trigger
                travelToLinger.AddTrigger(new Trigger_Memo("TravelArrived"));
                graph.AddTransition(travelToLinger);
                
                // Linger -> Steal (After Duration)
                Transition lingerToSteal = new Transition(lingerToil, stealToil);
                lingerToSteal.AddTrigger(new Trigger_TicksPassed(lingerDurationTicks));
                lingerToSteal.AddTrigger(new Trigger_PawnHarmed(0.5f, true, null)); // If harmed, start stealing/leaving? No, assault handles this via global trigger.
                lingerToSteal.AddPreAction(new TransitionAction_Message("Raiders are done loitering and will now plunder before leaving.", MessageTypeDefOf.NeutralEvent));
                graph.AddTransition(lingerToSteal);
                
                // Steal -> Exit (When done or full)
                // LordToil_StealCover usually transitions itself, but let's add timeout/completion
                Transition stealToExit = new Transition(stealToil, exitToil);
                stealToExit.AddTrigger(new Trigger_TicksPassed(10000)); // Increased from 5000
                graph.AddTransition(stealToExit);
                
                // GLOBAL Aggression Trigger: If attacked, switch to Assault
                LordToil_AssaultColony assaultToil = new LordToil_AssaultColony();
                graph.AddToil(assaultToil);
                
                Transition toAssault = new Transition(travelToil, assaultToil);
                toAssault.AddSource(lingerToil);
                toAssault.AddSource(stealToil);
                toAssault.AddSource(exitToil); // Even if leaving, if attacked, fight back?
                
                // Trigger if any pawn in the lord is harmed by Player
                // We need Trigger_PawnHarmed.
                // Trigger_PawnHarmed: chance=1, involveFaction=true -> signals simple harm.
                // We can check if damage info instigator is player in code, or use simple harm response.
                toAssault.AddTrigger(new Trigger_PawnHarmed(1f, false, this.faction)); 
                toAssault.AddPreAction(new TransitionAction_Message("Raiders are fighting back!", MessageTypeDefOf.NegativeEvent));
                toAssault.AddPreAction(new TransitionAction_WakeAll());
                graph.AddTransition(toAssault);

                // Assault -> Exit (Flee)
                Transition fleaTrig = new Transition(assaultToil, exitToil);
                fleaTrig.AddTrigger(new Trigger_FractionPawnsLost(0.5f));
                graph.AddTransition(fleaTrig);

                graph.StartingToil = travelToil;
                return graph;
            }

            // Failure/Neutral (Aggressive)
            
            // Just provide a basic Assault graph for safety if somehow this job is active.
            LordToil_AssaultColony assaultToilDefault = new LordToil_AssaultColony();
            graph.AddToil(assaultToilDefault);
            
            LordToil_ExitMap exitToilDefault = new LordToil_ExitMap(LocomotionUrgency.Jog, true, true);
            graph.AddToil(exitToilDefault);
            
            Transition fleeTrigDefault = new Transition(assaultToilDefault, exitToilDefault);
            fleeTrigDefault.AddTrigger(new Trigger_FractionPawnsLost(0.5f));
            graph.AddTransition(fleeTrigDefault);
            
            graph.StartingToil = assaultToilDefault;
            
            return graph;
        }
        
        private IntVec3 GetSmartLingerSpot(Map map)
        {
            if (map == null) return IntVec3.Invalid;

            // 1. Try to find a Gather Spot (Table/Party Spot)
            Building gatherBuilding = null;
            // Check building defName since property is not easily accessible
            if (map.listerBuildings.allBuildingsColonist.Where(b => 
                b.def.defName.Contains("Table") || 
                b.def.defName == "PartySpot" || 
                b.def.defName == "MarriageSpot"
            ).TryRandomElement(out gatherBuilding))
            {
                return gatherBuilding.Position;
            }

            // 2. Try to find a Colonist Bed (Sapper favorite)
            // allBuildingsColonist implies Faction check. just check if it's a bed.
            Building bed = null;
            if (map.listerBuildings.allBuildingsColonist.Where(b => b is Building_Bed).TryRandomElement(out bed))
            {
                return bed.Position;
            }

            // 3. Fallback to center/base location
            IntVec3 result;
            if (CellFinder.TryFindRandomCellNear(map.Center, map, 30, (c) => c.Standable(map) && !c.Fogged(map), out result))
            {
                return result;
            }
            
            return CellFinder.RandomClosewalkCellNear(map.Center, map, 20);
        }
        
        public void Notify_RaiderHarmed(Pawn victim, DamageInfo dinfo)
        {
            // Called by custom patch to ensure instant response
            if (outcome == NegotiatedRaidOutcome.Positive && dinfo.Instigator != null && dinfo.Instigator.Faction == Faction.OfPlayer)
            {
                if (this.faction != null && !this.faction.HostileTo(Faction.OfPlayer))
                {
                    SLog.Message("[NegotiatedRaid] Colonist attacked raider! Breaking deal and restoring hostility instantly.");
                    RaidOutcomeUtility.CheckAndRestoreHostility(this.faction, this.Map, this.originalGoodwill, this.lord);
                }
            }
        }
        
        public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
        {
            base.Notify_PawnLost(p, condition);
        }

        public override void Cleanup()
        {
            base.Cleanup();
            // Re-check hostility when the lord is gone.
            RaidOutcomeUtility.CheckAndRestoreHostility(this.faction, this.Map, this.originalGoodwill);
        }
    }
    
    public class LordToil_SafeTravel : LordToil
    {
        private IntVec3 dest;
        public LordToil_SafeTravel(IntVec3 dest)
        {
            this.dest = dest;
        }

        public override void UpdateAllDuties()
        {
             for (int i = 0; i < lord.ownedPawns.Count; i++)
             {
                 Pawn p = lord.ownedPawns[i];
                 if (p == null || p.mindState == null) continue;
                 p.mindState.duty = new PawnDuty(DutyDefOf.TravelOrWait, dest, -1f);
             }
        }
    }

    public class LordToil_DoAssault : LordToil
    {
        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn p = lord.ownedPawns[i];
                if (p == null || p.mindState == null) continue;
                
                p.mindState.duty = new PawnDuty(DutyDefOf.AssaultColony);
                
                // Ensure no lingering negotiation hediffs prevent hostility
                if (p.health != null)
                {
                    Hediff hediff = p.health.hediffSet.GetFirstHediffOfDef(SI_HediffDefOf.SI_Negotiating);
                    if (hediff != null)
                    {
                        p.health.RemoveHediff(hediff);
                    }
                }
            }
        }
    }
    
    public static class RaidOutcomeUtility
    {
        public static void ApplyRaidOutcome(Lord lord, NegotiatedRaidOutcome outcome)
        {
            if (lord == null) return;
            
            if (outcome == NegotiatedRaidOutcome.Neutral)
            {
                SLog.Message("[RaidOutcome] Neutral outcome: Raiders will continue their original attack pattern.");
                return;
            }
            
            Map map = lord.Map;
            Faction faction = lord.faction;
            
            // Debug logging
            SLog.Message("[RaidOutcome] Applying outcome " + outcome + " to Lord " + lord.loadID);
            SLog.Message("[RaidOutcome] Original Lord has " + lord.ownedPawns.Count + " pawns");
            
            // Gather ALL valid pawns of this faction on the map to ensure we catch split groups
            List<Pawn> pawns = new List<Pawn>();
            if (faction != null && map != null)
            {
                List<Pawn> factionPawns = map.mapPawns.SpawnedPawnsInFaction(faction);
                foreach (Pawn p in factionPawns)
                {
                    if (p != null && !p.Dead && !p.Downed && !p.IsPrisoner && !p.IsSlave)
                    {
                        pawns.Add(p);
                    }
                }
            }
            // Fallback to lord pawns if something failed with map lookup (unlikely)
            if (pawns.Count == 0 && lord.ownedPawns != null)
            {
                pawns.AddRange(lord.ownedPawns);
            }
            
            SLog.Message("[RaidOutcome] Gathered " + pawns.Count + " total pawns for new Lord");

            // Clean up hediffs
            foreach (Pawn p in pawns)
            {
                if (p != null && p.health != null)
                {
                     Hediff hediff = p.health.hediffSet.GetFirstHediffOfDef(SI_HediffDefOf.SI_Negotiating);
                     if (hediff != null)
                     {
                         p.health.RemoveHediff(hediff);
                     }
                }
            }

            // Remove existing Lords for all gathered pawns
            // Pawns might belong to different Lords (e.g. split raids). We must free them all.
            HashSet<Lord> lordsToRemove = new HashSet<Lord>();
            if (lord != null) lordsToRemove.Add(lord);
            
            foreach (Pawn p in pawns)
            {
                if (p.GetLord() != null)
                {
                    lordsToRemove.Add(p.GetLord());
                }
            }
            
            foreach (Lord l in lordsToRemove)
            {
                SLog.Message("[RaidOutcome] Removing old Lord " + l.loadID);
                l.lordManager.RemoveLord(l);
            }

            Lord newLord = null;
            if (outcome == NegotiatedRaidOutcome.CriticalSuccess)
            {
                 // Critical Success: Leave Immediately
                 Messages.Message("Negotiation Critical Success! Raiders are leaving.", MessageTypeDefOf.PositiveEvent);
                 LordJob_ExitMapBest exitJob = new LordJob_ExitMapBest(LocomotionUrgency.Jog, true, true);
                 newLord = LordMaker.MakeNewLord(faction, exitJob, map, pawns);
            }
            else if (outcome == NegotiatedRaidOutcome.Positive)
            {
                int originalGoodwill = -100;
                // Positive: Negotiation success, raiders leave peacefully.
                if (faction != null)
                {
                    // Save original goodwill
                    originalGoodwill = faction.GoodwillWith(Faction.OfPlayer);
                    SLog.Message("[RaidOutcome] Saved original goodwill: " + originalGoodwill);

                    // Force Neutrality so they don't get shot while loitering
                    // SetRelationDirect causes errors for goodwill factions, so we use goodwill math.
                    int currentGoodwill = faction.GoodwillWith(Faction.OfPlayer);
                    int needed = 0 - currentGoodwill;
                    if (needed != 0)
                    {
                        faction.TryAffectGoodwillWith(Faction.OfPlayer, needed, canSendMessage: false, canSendHostilityLetter: false);
                        SLog.Message("[RaidOutcome] Adjusted faction goodwill by " + needed + " to reach 0 (Neutral).");
                    }
                    
                    // Clear combat states for all pawns to prevent lingering aggression
                    foreach (Pawn p in pawns)
                    {
                        if (p.mindState != null)
                        {
                            p.mindState.enemyTarget = null;
                            // Resetting enemyTarget is crucial to stop immediate melee attacks
                        }
                    }
                }
                Messages.Message("Raiders agreed to a deal. They will hang around before leaving.", MessageTypeDefOf.PositiveEvent);
                
                // Use custom LordJob for "Loiter and Plunder" logic
                LordJob_NegotiatedRaid positiveJob = new LordJob_NegotiatedRaid(faction, NegotiatedRaidOutcome.Positive, originalGoodwill);
                newLord = LordMaker.MakeNewLord(faction, positiveJob, map, pawns);
                SLog.Message("[RaidOutcome] Created LordJob_NegotiatedRaid (Positive) for " + pawns.Count + " pawns with originalGoodwill " + originalGoodwill);
            }
             else if (outcome == NegotiatedRaidOutcome.Failure)
            {
                 // Failure: Attack
                 Messages.Message("Negotiation Failed! Raiders are attacking!", MessageTypeDefOf.NegativeEvent);
                 
                 // Use custom LordJob_NegotiatedRaid which has a simplified, aggressive Assault graph (no early stealing/leaving)
                 LordJob_NegotiatedRaid assaultJob = new LordJob_NegotiatedRaid(faction, NegotiatedRaidOutcome.Failure);
                 newLord = LordMaker.MakeNewLord(faction, assaultJob, map, pawns);
                 SLog.Message("[RaidOutcome] Created LordJob_NegotiatedRaid (Failure) for " + pawns.Count + " pawns");
            }
            
            // Force duty update
            if (newLord != null && newLord.CurLordToil != null)
            {
                newLord.CurLordToil.UpdateAllDuties();
                SLog.Message("[RaidOutcome] Duties updated. Forcing job interruptions...");

                // Force restart jobs to ensure they react to the new duty immediately
                foreach (Pawn p in pawns)
                {
                    if (p != null)
                    {
                        string jobDefName = (p.CurJobDef != null) ? p.CurJobDef.defName : "null";
                        string dutyDefName = (p.mindState != null && p.mindState.duty != null) ? p.mindState.duty.def.defName : "null";
                        SLog.Message(string.Format("[RaidOutcome] Pawn {0}: Job={1}, Duty={2}", p.LabelShort, jobDefName, dutyDefName));

                        if (p.jobs != null && p.CurJobDef != JobDefOf.AttackMelee && p.CurJobDef != JobDefOf.AttackStatic)
                        {
                            SLog.Message("[RaidOutcome] Interrupting job for " + p.LabelShort);
                            p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                        }
                    }
                }
            }
        }

        public static void CheckAndRestoreHostility(Faction faction, Map map, int originalGoodwill, Lord ignoreLord = null)
        {
            if (faction == null || map == null) return;
            
            // Check if current goodwill is "Negotiated Neutral" (around 0)
            int currentGoodwill = faction.GoodwillWith(Faction.OfPlayer);
            
            // If they are still neutralish (between -50 and 50) and we should restore their hostility
            if (currentGoodwill > -50)
            {
                // Check if ANY other lord of this faction with NegotiatedRaid exists on the map
                bool anotherNegotiatedLord = false;
                if (map.lordManager != null && map.lordManager.lords != null)
                {
                    foreach (Lord lord in map.lordManager.lords)
                    {
                        if (lord != null && lord != ignoreLord && lord.faction == faction && lord.LordJob is LordJob_NegotiatedRaid)
                        {
                            anotherNegotiatedLord = true;
                            break;
                        }
                    }
                }
                
                if (!anotherNegotiatedLord)
                {
                    // Restore to original goodwill
                    int needed = originalGoodwill - currentGoodwill;
                    if (needed != 0)
                    {
                        faction.TryAffectGoodwillWith(Faction.OfPlayer, needed, canSendMessage: false, canSendHostilityLetter: false);
                        SLog.Message("[RaidOutcome] Restored original hostility for " + faction.Name + " (Goodwill reset to " + originalGoodwill + ").");
                    }
                }
            }
        }
    }
}
