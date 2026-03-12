using HarmonyLib;
using RimWorld;
using Verse;

namespace SocialInteractions
{
    /// <summary>
    /// Harmony patch for InspirationHandler.TryStartInspiration to trigger monologue
    /// when a pawn receives an inspiration.
    /// </summary>
    [HarmonyPatch(typeof(InspirationHandler), "TryStartInspiration")]
    public static class InspirationHandler_TryStartInspiration_Patch
    {
        public static void Postfix(bool __result, InspirationHandler __instance, InspirationDef def)
        {
            // Check if feature and LLM interactions are enabled
            if (!SocialInteractions.Settings.enableInspirationMonologue || 
                !SocialInteractions.Settings.llmInteractionsEnabled)
            {
                return;
            }
            
            // Only trigger if inspiration was successfully started
            if (!__result)
            {
                return;
            }
            
            Pawn pawn = __instance.pawn;
            if (pawn == null)
            {
                return;
            }
            
            // Get inspiration type for the subject
            string inspirationType = def != null ? def.label : "something";
            string subject = string.Format("just received an inspiration: {0}", inspirationType);
            
            SLog.Message(string.Format("[SocialInteractions] Inspiration received by {0}: {1}", 
                pawn.LabelShort, inspirationType));
            
            // Trigger the monologue - using "inspiration" as the topic
            SocialInteractions.HandleMonologue(pawn, subject, false, "inspiration");
        }
    }
}
