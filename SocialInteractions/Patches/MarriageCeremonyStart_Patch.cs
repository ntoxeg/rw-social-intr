using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using System.Reflection;

namespace SocialInteractions
{
    // Patch the transition from gathering to actual ceremony to trigger LLM once at the beginning
    [HarmonyPatch(typeof(LordJob_Joinable_MarriageCeremony), "CreateGraph")]
    public static class LordJob_Joinable_MarriageCeremony_CreateGraph_Patch
    {
        static void Postfix(LordJob_Joinable_MarriageCeremony __instance, StateGraph __result)
        {
            // Find the transition that leads to the marriage ceremony (from gathering to actual vows)
            // Looking at the decompiled code structure, we're looking for the transition that has a
            // TransitionAction_Message with "MessageMarriageCeremonyStarts"
            foreach (var transition in __result.transitions)
            {
                // Check if this transition targets the marriage ceremony toil
                if (transition.target is LordToil_MarriageCeremony)
                {
                    SLog.Message("[SocialInteractions] Found transition to LordToil_MarriageCeremony");
                    
                    // This is the transition from party to marriage ceremony - add our LLM trigger
                    transition.AddPreAction(new TransitionAction_Custom(() =>
                    {
                        TriggerMarriageCeremonyLLM(__instance);
                    }));
                    // We don't break here because there might be multiple transitions leading to the ceremony
                    // (e.g. from different states), though usually there's just one main one.
                    // But to be safe against multiple triggers, we might want to ensure we only add it once per instance runtime,
                    // but since this is CreateGraph, it runs once when the LordJob is created.
                }
            }
        }

        private static void TriggerMarriageCeremonyLLM(LordJob_Joinable_MarriageCeremony lordJob)
        {
            // Check if LLM is enabled for marriage ceremonies
            if (!SocialInteractions.IsLlmMarriageCeremonyEnabled())
            {
                return;
            }

            // Check if LLM is busy and if we should prevent spam
            if (SocialInteractions.Settings.preventSpam && SpeechBubbleManager.IsLlmCurrentlyBusy())
            {
                return;
            }

            // Get the pawns involved in the ceremony
            var firstPawn = lordJob.firstPawn;
            var secondPawn = lordJob.secondPawn;

            if (firstPawn == null || secondPawn == null)
            {
                return;
            }

            SLog.Message(string.Format("[SocialInteractions] Marriage ceremony beginning detected for {0} and {1}", 
                firstPawn.LabelShort, secondPawn.LabelShort));

            // Create a subject for the marriage ceremony based on the pawn names and context
            string subject = string.Format("{0} and {1} are exchanging vows in their marriage ceremony.", 
                firstPawn.LabelShort, secondPawn.LabelShort);

            // Handle the interaction using the same approach as other LLM interactions
            SocialInteractions.HandleNonStoppingInteraction(firstPawn, secondPawn, null, subject, true, true);
        }
    }
}