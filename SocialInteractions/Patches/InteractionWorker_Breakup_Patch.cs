using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;
using System.Text;
using System.Reflection;

namespace SocialInteractions
{
    // Using AccessTools.Method to get the MethodInfo since InteractionWorker_Breakup might not be publicly accessible
    [HarmonyPatch]
    public static class InteractionWorker_Breakup_Patch
    {
        public static MethodBase TargetMethod()
        {
            // Attempt to get the InteractionWorker_Breakup type and its Interacted method
            // Try multiple possible assembly names for the RimWorld assembly
            string[] assemblyNames = {
                "Assembly-CSharp",
                "Assembly-CSharp-firstpass"
            };
            
            System.Type interactionWorkerBreakupType = null;
            foreach (string assemblyName in assemblyNames)
            {
                string fullTypeName = string.Format("RimWorld.InteractionWorker_Breakup, {0}", assemblyName);
                interactionWorkerBreakupType = System.Type.GetType(fullTypeName);
                if (interactionWorkerBreakupType != null)
                {
                    SLog.Message(string.Format("[SocialInteractions] Found InteractionWorker_Breakup in {0}", assemblyName));
                    break;
                }
            }
            
            if (interactionWorkerBreakupType == null)
            {
                SLog.Warning("[SocialInteractions] Could not find InteractionWorker_Breakup type. Breakup patch may not work.");
                return null;
            }
            
            MethodBase method = AccessTools.Method(interactionWorkerBreakupType, "Interacted");
            if (method == null)
            {
                SLog.Warning("[SocialInteractions] Could not find Interacted method in InteractionWorker_Breakup. Breakup patch may not work.");
                return null;
            }
            
            return method;
        }

        public static void Prefix(object __instance, Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, ref string letterText, ref string letterLabel, ref LetterDef letterDef, ref LookTargets lookTargets)
        {
            SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Breakup_Patch.Prefix called for {0} breaking up with {1}", initiator.LabelShort, recipient.LabelShort));

            // Check if LLM is enabled for breakups
            if (!SocialInteractions.IsLlmBreakupEnabled())
            {
                SLog.Message("[SocialInteractions] LLM breakups are disabled in settings, proceeding with default behavior.");
                return;
            }

            // Check if LLM is busy and if we should prevent spam
            if (SocialInteractions.Settings.preventSpam && SpeechBubbleManager.IsLlmCurrentlyBusy())
            {
                SLog.Message("[SocialInteractions] Breakup LLM is busy and preventSpam is true, showing default behavior only.");
                return;
            }

            // Check if we should generate LLM text for this breakup
            if (SocialInteractions.Settings.useLlmForBreakups)
            {
                SLog.Message("[SocialInteractions] Processing LLM breakup interaction...");
                
                // Create a subject for the breakup based on the pawn names and context
                string subject = string.Format("{0} is breaking up with {1}.", initiator.LabelShort, recipient.LabelShort);
                
                // Generate LLM prompt and handle interaction
                string prompt = SocialInteractions.GenerateDeepTalkPrompt(initiator, recipient, null, subject);
                
                if (!string.IsNullOrEmpty(prompt))
                {
                    SLog.Message("[SocialInteractions] Breakup has a valid prompt, creating LLM interaction.");
                    
                    // Handle the interaction using the same approach as other LLM interactions
                    int conversationId = SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, null, subject, true, true);
                    
                    // Format the subject with rich text for the default bubble
                    string formattedSubject = SpeechBubbleManager.FormatSpeakerName(initiator, "Breaking up...");
                    SpeechBubbleManager.ShowDefaultBubble(initiator, formattedSubject);
                }
                else
                {
                    SLog.Message("[SocialInteractions] Breakup does not have a valid prompt, proceeding with default behavior.");
                }
            }
            else
            {
                SLog.Message("[SocialInteractions] LLM for breakups is disabled, proceeding with default behavior but still showing default bubble.");
                
                // Even if LLM is disabled, we can still show a default bubble with modified text
                string defaultSubject = string.Format("I can't go on like this. We need to break up, {0}.", recipient.LabelShort);
                string formattedSubject = SpeechBubbleManager.FormatSpeakerName(initiator, defaultSubject);
                SpeechBubbleManager.ShowDefaultBubble(initiator, formattedSubject);
            }
        }
    }
}