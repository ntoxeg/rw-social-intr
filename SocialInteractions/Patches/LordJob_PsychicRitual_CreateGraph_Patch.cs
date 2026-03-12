using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using HarmonyLib;
using Verse;
using Verse.AI.Group;
using UnityEngine;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(LordJob_PsychicRitual), "CreateGraph")]
    public static class LordJob_PsychicRitual_CreateGraph_Patch
    {
        public static void Postfix(LordJob_PsychicRitual __instance, StateGraph __result)
        {
            try
            {
                // Check if this is a psychic ritual that should have monologues
                if (__instance == null || __instance.def == null || __instance.assignments == null)
                {
                    SLog.Message("[SocialInteractions] LordJob_PsychicRitual_CreateGraph_Patch: Null instance, def, or assignments");
                    return;
                }

                // Get the invoker (main participant) of the ritual
                // Try to get the InvokerRole property if it exists (for InvocationCircle rituals)
                PsychicRitualRoleDef invokerRole = null;
                if (__instance.def is PsychicRitualDef_InvocationCircle)
                {
                    PsychicRitualDef_InvocationCircle invocationCircle = (PsychicRitualDef_InvocationCircle)__instance.def;
                    invokerRole = invocationCircle.InvokerRole;
                }
                
                Pawn invoker = null;
                if (invokerRole != null)
                {
                    invoker = __instance.assignments.FirstAssignedPawn(invokerRole);
                }
                
                // If we couldn't get the invoker through the role, try to get the first assigned pawn
                if (invoker == null && __instance.assignments.AssignedPawnCount > 0)
                {
                    invoker = __instance.assignments.AllAssignedPawns.FirstOrDefault();
                }
                
                if (invoker == null)
                {
                    SLog.Message("[SocialInteractions] LordJob_PsychicRitual_CreateGraph_Patch: No invoker found");
                    return;
                }

                SLog.Message(string.Format("[SocialInteractions] LordJob_PsychicRitual_CreateGraph_Patch: Found invoker {0} for ritual {1}", invoker.Name, __instance.def.defName));

                // Generate a subject for the monologue based on the ritual type
                string subject = GenerateRitualSubject(__instance.def, invoker);
                
                // Trigger a monologue for the invoker
                SocialInteractions.HandleMonologue(invoker, subject, true, "speech");
            }
            catch (Exception ex)
            {
                SLog.Error(string.Format("[SocialInteractions] LordJob_PsychicRitual_CreateGraph_Patch error: {0}", ex));
            }
        }

        private static string GenerateRitualSubject(PsychicRitualDef def, Pawn invoker)
        {
            // Generate a subject based on the ritual type
            string ritualName = def.label;
            return string.Format("is performing the psychic ritual '{0}'", ritualName);
        }
    }
}