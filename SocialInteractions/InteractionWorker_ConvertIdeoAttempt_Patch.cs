using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(InteractionWorker_ConvertIdeoAttempt), "Interacted")]
    public static class InteractionWorker_ConvertIdeoAttempt_Patch
    {
        public static void Postfix(InteractionWorker_ConvertIdeoAttempt __instance, Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks)
        {
            if (SocialInteractions.Settings.verboseLogging)
            {
                SLog.Message(string.Format("[SocialInteractions] InteractionWorker_ConvertIdeoAttempt_Patch called. Initiator: {0}, Recipient: {1}", 
                    initiator?.LabelShort ?? "null", recipient?.LabelShort ?? "null"));
            }

            if (!SocialInteractions.Settings.enableIdeologyConversionInteractions || !SocialInteractions.Settings.llmInteractionsEnabled)
            {
                if (SocialInteractions.Settings.verboseLogging)
                {
                    SLog.Message("[SocialInteractions] Conversion interaction disabled in settings.");
                }
                return;
            }

            if (initiator == null || recipient == null || extraSentencePacks == null)
            {
                if (SocialInteractions.Settings.verboseLogging)
                {
                    SLog.Message("[SocialInteractions] Initiator, recipient, or extraSentencePacks is null.");
                }
                return;
            }

            // Determine the outcome based on the added sentence packs
            string outcome = "unknown";
            if (extraSentencePacks.Contains(RulePackDefOf.Sentence_ConvertIdeoAttemptSuccess))
            {
                outcome = "success";
            }
            else if (extraSentencePacks.Contains(RulePackDefOf.Sentence_ConvertIdeoAttemptFail))
            {
                outcome = "fail";
            }
            else if (extraSentencePacks.Contains(RulePackDefOf.Sentence_ConvertIdeoAttemptFailResentment))
            {
                outcome = "fail_resentment";
            }
            else if (extraSentencePacks.Contains(RulePackDefOf.Sentence_ConvertIdeoAttemptFailSocialFight))
            {
                outcome = "fail_social_fight";
            }

            if (SocialInteractions.Settings.verboseLogging)
            {
                SLog.Message(string.Format("[SocialInteractions] Conversion outcome: {0}", outcome));
            }

            // If we couldn't determine outcome from sentence packs (shouldn't happen if logic matches vanilla), just return
            if (outcome == "unknown")
            {
                return;
            }

            // Construct the subject for the LLM prompt - make it more descriptive and useful for LLM
            string subject = string.Format("A conversation between {0} and {1} about ideologies. {0} is attempting to convert {1} to their ideology. The attempt {2}.",
                initiator.LabelShort, recipient.LabelShort, outcome == "success" ? "succeeds" : "fails (" + outcome + ")");

            // Trigger the LLM interaction
            // We use HandleNonStoppingInteraction because this interaction happens instantly and doesn't have a sustained job like Deep Talk
            // But we want it to be treated as a social interaction
            
            // Create a temporary interaction def or use a generic one if needed, but HandleInteraction expects one.
            // We can pass null and let GenerateDeepTalkPrompt handle it if we modified it to accept null, 
            // but HandleInteraction might rely on it.
            // Let's check HandleInteraction in SocialInteractions.cs.
            
            // Actually, looking at SocialInteractions.cs, HandleInteraction takes an InteractionDef.
            // We should probably use a dummy def or the actual interaction def if we can get it.
            // InteractionWorker doesn't know its own Def usually.
            // We can use InteractionDefOf.Chitchat as a placeholder or create a custom one.
            // Or we can use HandleMonologue if we just want the initiator to speak, but conversion is a dialogue.
            
            // Use the proper ideology conversion interaction def
            SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, InteractionDefOf.ConvertIdeoAttempt, subject);
        }
    }
}
