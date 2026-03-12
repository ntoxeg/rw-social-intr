using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace SocialInteractions
{
    /// <summary>
    /// Patches and utilities for raid negotiation feature.
    /// </summary>
    public static class RaidNegotiationUtility
    {
        /// <summary>
        /// Find all raids on the map that can be negotiated with.
        /// </summary>
        public static IEnumerable<Lord> GetNegotiableRaids(Map map)
        {
            if (map == null || map.lordManager == null) yield break;
            
            foreach (Lord lord in map.lordManager.lords)
            {
                if (CanNegotiateWithRaid(lord))
                {
                    yield return lord;
                }
            }
        }
        
        /// <summary>
        /// Check if a specific raid Lord can be negotiated with.
        /// </summary>
        public static bool CanNegotiateWithRaid(Lord lord)
        {
            if (lord == null || lord.faction == null) return false;
            
            // Must be hostile to player
            if (!lord.faction.HostileTo(Faction.OfPlayer)) return false;
            
            // Must be humanlike faction that can speak
            if (!CanFactionNegotiate(lord.faction)) return false;
            
            // Must have living pawns
            if (lord.ownedPawns == null || lord.ownedPawns.Count == 0) return false;
            if (!lord.ownedPawns.Any(p => p != null && !p.Dead && !p.Downed)) return false;
            
            // Must be in staging phase (before attack started)
            // Check that no pawns have engaged in combat yet (no "guilty" equivalent)
            if (HasRaidStartedCombat(lord)) return false;
            
            // Must be an assault-type lord job
            if (!IsAssaultLordJob(lord.LordJob)) return false;
            
            return true;
        }
        
        /// <summary>
        /// Check if the faction can negotiate (humanlike, can speak).
        /// </summary>
        public static bool CanFactionNegotiate(Faction faction)
        {
            if (faction == null) return false;
            
            // Mechanoids can't negotiate
            if (faction.def == FactionDefOf.Mechanoid) return false;
            
            // Insects can't negotiate
            if (faction.def == FactionDefOf.Insect) return false;
            
            // Check if faction race is humanlike
            if (faction.def.basicMemberKind != null && 
                faction.def.basicMemberKind.race != null &&
                faction.def.basicMemberKind.race.race != null)
            {
                if (!faction.def.basicMemberKind.race.race.Humanlike) return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Check if the raid has already started combat.
        /// </summary>
        public static bool HasRaidStartedCombat(Lord lord)
        {
            if (lord == null || lord.ownedPawns == null) return false;
            
            foreach (Pawn pawn in lord.ownedPawns)
            {
                if (pawn == null || pawn.Dead) continue;
                
                // Check if pawn is in combat (attacking recently)
                if (pawn.mindState != null && pawn.mindState.lastAttackedTarget.IsValid)
                {
                    // If attacked something recently (within last 5 seconds)
                    if (Find.TickManager.TicksGame - pawn.mindState.lastAttackTargetTick < 300)
                    {
                        return true;
                    }
                }
                
                // Check if pawn has been attacked recently via meleeThreatHarmTick
                if (pawn.mindState != null)
                {
                    // If was attacked recently (within last 5 seconds)
                    if (Find.TickManager.TicksGame - pawn.mindState.lastMeleeThreatHarmTick < 300)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if the lord job is an assault-type job that can be negotiated.
        /// </summary>
        public static bool IsAssaultLordJob(LordJob job)
        {
            if (job == null) return false;
            
            string jobTypeName = job.GetType().Name;
            
            // Common assault lord jobs
            return jobTypeName.Contains("Assault") || 
                   jobTypeName.Contains("Stage") || 
                   jobTypeName.Contains("Raid");
        }
        
        /// <summary>
        /// Get the leader/negotiator of a raid group.
        /// </summary>
        public static Pawn GetRaidLeader(Lord lord)
        {
            if (lord == null || lord.ownedPawns == null || lord.ownedPawns.Count == 0) return null;
            
            // Find the pawn with highest social skill, or any valid pawn
            Pawn leader = null;
            int bestSocial = -1;
            
            foreach (Pawn pawn in lord.ownedPawns)
            {
                if (pawn == null || pawn.Dead || pawn.Downed) continue;
                if (!pawn.RaceProps.Humanlike) continue;
                
                int social = 0;
                if (pawn.skills != null)
                {
                    var socialSkill = pawn.skills.GetSkill(SkillDefOf.Social);
                    if (socialSkill != null)
                    {
                        social = socialSkill.Level;
                    }
                }
                
                if (social > bestSocial)
                {
                    bestSocial = social;
                    leader = pawn;
                }
            }
            
            // Fallback to any alive pawn
            if (leader == null)
            {
                leader = lord.ownedPawns.FirstOrDefault(p => p != null && !p.Dead && !p.Downed);
            }
            
            return leader;
        }
        
        /// <summary>
        /// Get a description of the raid for UI/prompts.
        /// </summary>
        public static string GetRaidDescription(Lord lord)
        {
            if (lord == null) return "Unknown raiders";
            
            int count = 0;
            if (lord.ownedPawns != null)
            {
                count = lord.ownedPawns.Count(p => p != null && !p.Dead);
            }
            string factionName = lord.faction != null ? lord.faction.Name : "Unknown faction";
            
            return string.Format("{0} raiders from {1}", count, factionName);
        }

        /// <summary>
        /// Check if a pawn is part of a negotiable raid.
        /// </summary>
        public static bool IsPartOfNegotiableRaid(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null) return false;
            
            Lord lord = pawn.GetLord();
            if (lord == null) return false;
            
            return CanNegotiateWithRaid(lord);
        }
    }
    
    /// <summary>
    /// Patch to make pawns with SI_Negotiating hediff be ignored by hostile factions.
    /// </summary>
    [HarmonyPatch(typeof(GenHostility), "HostileTo", new Type[] { typeof(Thing), typeof(Thing) })]
    public static class GenHostility_HostileTo_Patch
    {
        public static void Postfix(Thing a, Thing b, ref bool __result)
        {
            // If result is already false (not hostile), no need to check
            if (!__result) return;
            
            // Check if either thing is a pawn with the negotiating hediff
            Pawn pawnA = a as Pawn;
            Pawn pawnB = b as Pawn;
            
            // If pawn A has negotiating hediff and B is a raider, don't be hostile
            if (pawnA != null && HasNegotiatingHediff(pawnA))
            {
                // Check if pawn B is part of a raid that can be negotiated with
                if (pawnB != null && RaidNegotiationUtility.IsPartOfNegotiableRaid(pawnB))
                {
                    __result = false;
                    return;
                }
            }
            
            // If pawn B has negotiating hediff and A is a raider, don't be hostile
            if (pawnB != null && HasNegotiatingHediff(pawnB))
            {
                // Check if pawn A is part of a raid that can be negotiated with
                if (pawnA != null && RaidNegotiationUtility.IsPartOfNegotiableRaid(pawnA))
                {
                    __result = false;
                    return;
                }
            }

            // --- PROTECT THE ENTIRE RAID during loitering/plundering ---
            // If either pawn is part of a NegotiatedRaid Lord that is in loitering/plundering phase
            if (pawnA != null && IsInPeacefulNegotiationPhase(pawnA))
            {
                __result = false;
                return;
            }
            if (pawnB != null && IsInPeacefulNegotiationPhase(pawnB))
            {
                __result = false;
                return;
            }
        }
        
        private static bool HasNegotiatingHediff(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null) return false;
            return pawn.health.hediffSet.HasHediff(SI_HediffDefOf.SI_Negotiating);
        }

        private static bool IsInPeacefulNegotiationPhase(Pawn pawn)
        {
            if (pawn == null) return false;
            Lord lord = pawn.GetLord();
            if (lord == null || !(lord.LordJob is LordJob_NegotiatedRaid)) return false;

            // If they are in a peaceful toil (Travel, DefendPoint/Linger, Steal/Plunder), don't be hostile.
            // If the LordJob switches to Assault (e.g. if player attacks), this LordJob instance 
            // will still be NegotiatedRaid but the TOIL will be LordToil_AssaultColony.
            
            LordToil toil = lord.CurLordToil;
            if (toil == null) return false;

            // These are the peaceful toils in LordJob_NegotiatedRaid.CreateGraph()
            return toil is LordToil_Travel || 
                   toil is LordToil_DefendPoint || 
                   toil is LordToil_Plunder || 
                   toil is LordToil_StealCover || 
                   toil is LordToil_ExitMap; // Even while leaving quietly
        }
    }
    
    /// <summary>
    /// Patch JobDriver_HaveChatWith to detect when a player is negotiating with a raider.
    /// This sets the context and applies protection hediff during the approach.
    /// </summary>
    [HarmonyPatch(typeof(JobDriver_HaveChatWith), "TryMakePreToilReservations")]
    public static class JobDriver_HaveChatWith_Patch
    {
        public static void Postfix(JobDriver_HaveChatWith __instance, bool __result)
        {
            // If reservation failed, don't do anything
            if (!__result) return;
            
            Pawn pawn = __instance.pawn;
            Pawn target = __instance.job.GetTarget(TargetIndex.A).Thing as Pawn;
            
            if (pawn == null || target == null) return;
            
            // Check if target is part of a negotiable raid
            if (RaidNegotiationUtility.IsPartOfNegotiableRaid(target))
            {
                Lord raidLord = target.GetLord();
                
                // Set the active raid context
                RaidNegotiationContext.SetActiveRaid(pawn, raidLord);
                
                // Apply negotiating hediff immediately to protect from fire during approach
                if (pawn.health != null)
                {
                    pawn.health.AddHediff(SI_HediffDefOf.SI_Negotiating);
                }
                
                // Ensure hediff is removed when job finishes avoiding it getting stuck
                __instance.AddFinishAction(delegate
                {
                    if (pawn.health != null && pawn.health.hediffSet.HasHediff(SI_HediffDefOf.SI_Negotiating))
                    {
                        var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(SI_HediffDefOf.SI_Negotiating);
                        if (hediff != null)
                        {
                            pawn.health.RemoveHediff(hediff);
                        }
                    }
                    
                    // Also clear context if it wasn't cleared already
                    RaidNegotiationContext.ClearActiveRaid(pawn);
                });
                
                // Notify player (optional, but good feedback)
                Messages.Message("Attempting to negotiate with " + target.LabelShort + "...", pawn, MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }

    /// <summary>
    /// Static class to track active raid negotiation context.
    /// </summary>
    public static class RaidNegotiationContext
    {
        private static Dictionary<Pawn, Lord> activeRaidNegotiations = new Dictionary<Pawn, Lord>();
        
        public static void SetActiveRaid(Pawn negotiator, Lord raid)
        {
            if (negotiator == null) return;
            activeRaidNegotiations[negotiator] = raid;
        }
        
        public static Lord GetActiveRaid(Pawn negotiator)
        {
            if (negotiator == null) return null;
            Lord raid;
            if (activeRaidNegotiations.TryGetValue(negotiator, out raid))
            {
                return raid;
            }
            return null;
        }
        
        public static void ClearActiveRaid(Pawn negotiator)
        {
            if (negotiator == null) return;
            activeRaidNegotiations.Remove(negotiator);
        }
        
        public static bool HasActiveRaid(Pawn negotiator)
        {
            if (negotiator == null) return false;
            return activeRaidNegotiations.ContainsKey(negotiator);
        }
    }
}
