using HarmonyLib;
using RimWorld;
using Verse;

namespace SocialInteractions
{
    /// <summary>
    /// Harmony patch for QualityUtility.SendCraftNotification to trigger monologue
    /// when a pawn crafts a masterwork or legendary quality item.
    /// </summary>
    [HarmonyPatch(typeof(QualityUtility), "SendCraftNotification")]
    public static class QualityUtility_SendCraftNotification_Patch
    {
        public static void Postfix(Thing thing, Pawn worker)
        {
            // Check if feature and LLM interactions are enabled
            if (!SocialInteractions.Settings.enableMasterworkMonologue || 
                !SocialInteractions.Settings.llmInteractionsEnabled)
            {
                return;
            }
            
            if (worker == null || thing == null)
            {
                return;
            }
            
            // Get quality component
            ThingWithComps thingWithComps = thing as ThingWithComps;
            CompQuality compQuality = thingWithComps != null ? thingWithComps.GetComp<CompQuality>() : null;
            if (compQuality == null)
            {
                return;
            }
            
            // Only trigger for masterwork or legendary quality
            if (compQuality.Quality < QualityCategory.Masterwork)
            {
                return;
            }
            
            string quality = compQuality.Quality.GetLabel();
            string itemName = thing.LabelShort;
            string subject;
            
            // Check if item has CompArt for rich description (sculptures, paintings, etc.)
            CompArt compArt = thing.TryGetComp<CompArt>();
            if (compArt != null && compArt.Active)
            {
                string artTitle = compArt.Title;
                string artDescription = compArt.GenerateImageDescription();
                
                // Rich subject with art details for bragging
                subject = string.Format(
                    "just finished crafting a {0} quality {1} titled \"{2}\". The artwork depicts: {3}",
                    quality, itemName, artTitle, artDescription);
                
                SLog.Message(string.Format("[SocialInteractions] Masterwork art created by {0}: {1} - \"{2}\"", 
                    worker.LabelShort, itemName, artTitle));
            }
            else
            {
                // Simple subject for non-art items (weapons, apparel, etc.)
                subject = string.Format("just finished crafting a {0} quality {1}", quality, itemName);
                
                SLog.Message(string.Format("[SocialInteractions] Masterwork item created by {0}: {1} ({2})", 
                    worker.LabelShort, itemName, quality));
            }
            
            // Trigger the monologue - using "masterpiece" as the topic
            SocialInteractions.HandleMonologue(worker, subject, false, "masterpiece");
        }
    }
}
