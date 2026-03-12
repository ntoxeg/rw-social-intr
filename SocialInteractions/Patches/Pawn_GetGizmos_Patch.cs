using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Pawn_GetGizmos_Patch
    {
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            // Only add the gizmo for colonists that are not downed, not drafted, and not in a mental state
            // Also only add it if the manual chat setting is enabled
            if (!__instance.IsColonistPlayerControlled || __instance.Downed || __instance.Drafted || __instance.InMentalState || 
                !SocialInteractions.Settings.enableManualChat)
            {
                return;
            }

            // Create a list from the existing gizmos
            List<Gizmo> gizmos = __result.ToList();

            // Add our custom "Have Chat With" gizmo
            Command_Action chatCommand = new Command_Action
            {
                defaultLabel = "Negotiate",
                defaultDesc = "Negotiate with another pawn",
                action = delegate
                {
                    // Start the target selection process
                    TargetingParameters targetingParams = new TargetingParameters();
                    targetingParams.canTargetPawns = true;
                    targetingParams.canTargetBuildings = false;
                    targetingParams.canTargetItems = false;
                    targetingParams.validator = (TargetInfo target) => 
                    {
                        Pawn targetPawn = target.Thing as Pawn;
                        if (targetPawn == null || targetPawn == __instance || !targetPawn.Spawned || targetPawn.Dead || targetPawn.Name == null || targetPawn.Name.Numerical)
                        {
                            return false;
                        }

                        // Check cooldown
                        if (Current.Game != null)
                        {
                            var comp = Current.Game.GetComponent<NegotiationCooldown_GameComponent>();
                            if (comp != null)
                            {
                                string reason;
                                if (comp.IsOnCooldown(targetPawn, targetPawn.Faction, out reason))
                                {
                                    // We can't easily show a reason in the validator return, but we can log it or show a message if they click.
                                    // Actually, validator prevents the click from even resolving. 
                                    // For better UX, we'll allow the click but check again in the action.
                                    return true; 
                                }
                            }
                        }
                        return true;
                    };
                    
                    Find.Targeter.BeginTargeting(targetingParams, delegate (LocalTargetInfo target)
                    {
                        Pawn targetPawn = target.Thing as Pawn;
                        if (targetPawn != null && targetPawn != __instance)
                        {
                            // Check cooldown again to provide user feedback
                            if (Current.Game != null)
                            {
                                var comp = Current.Game.GetComponent<NegotiationCooldown_GameComponent>();
                                if (comp != null)
                                {
                                    string reason;
                                    if (comp.IsOnCooldown(targetPawn, targetPawn.Faction, out reason))
                                    {
                                        Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                                        return;
                                    }
                                }
                            }

                            // Create and start the job
                            Job job = JobMaker.MakeJob(SI_JobDefOf.HaveChatWith, targetPawn);
                            __instance.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                        }
                    }, __instance);
                },
                hotKey = KeyBindingDefOf.Misc1
            };

            // Try to load the attack icon specifically
            Texture2D icon = ContentFinder<Texture2D>.Get("Things/Mote/SpeechSymbols/Speech", false);
            if (icon != null)
            {
                chatCommand.icon = icon;
            }

            gizmos.Add(chatCommand);

            // Update the result with our modified list
            __result = gizmos;
        }
    }
}