using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using System;
using System.Reflection;

namespace SocialInteractions
{
    [HarmonyPatch]
    public static class CharacterCardUtility_AddFlavorTextButton_Patch
    {
        public static MethodBase TargetMethod()
        {
            System.Type[] parameters = new System.Type[] { typeof(Rect), typeof(Pawn), typeof(Action), typeof(Rect), typeof(bool) };
            return typeof(CharacterCardUtility).GetMethod("DrawCharacterCard", parameters);
        }

        public static void Postfix(Rect rect, Pawn pawn, Action randomizeCallback, Rect creationRect, bool showName)
        {
            try
            {
                // Only add the button for colonists that are not dead
                if (pawn == null || pawn.IsColonist != true || pawn.Dead || !showName)
                {
                    return;
                }

                // Calculate position similar to other buttons in the character card
                // The buttons are positioned from right to left in the original code
                float baseX = PawnCardSize(pawn).x; // This matches the original base position
                float buttonSpacing = 40f; // Same as original
                
                // The original code logic for button positions:
                // float num = PawnCardSize(pawn).x - 85f;
                // - Banish button (-40f if applicable)
                // - Rename button (-40f if applicable) 
                // - Title button (-40f if applicable)
                // - Execute button (if applicable)
                
                float num = baseX - 85f; // Start from the same position as original code
                
                // Replicate the original logic for determining positions
                if (pawn.IsFreeColonist && pawn.Spawned && !pawn.IsQuestLodger())
                {
                    num -= buttonSpacing; // Banish button
                }

                // Position the button under rename
                Rect bioButtonRect = new Rect(num, 45f, 30f, 30f);
                
                // Tooltip for the bio button
                TooltipHandler.TipRegion(bioButtonRect, "SocialInteractions_EditBioButtonTooltip".Translate());

                // Draw the button with "Bio" text
                if (Widgets.ButtonText(bioButtonRect, "SocialInteractions_EditBioButtonLabel".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_EditPawnFlavorText(pawn));
                }
            }
            catch (System.Exception ex)
            {
                SLog.Error(string.Format("[SocialInteractions] Error in CharacterCardUtility_AddFlavorTextButton_Patch: {0}", ex.Message));
            }
        }

        // Copy of the PawnCardSize method from CharacterCardUtility
        private static Vector2 PawnCardSize(Pawn pawn)
        {
            Vector2 result = new Vector2(500f, 560f);
            if (pawn.RaceProps.Humanlike)
            {
                result.y += 15f;
            }
            return result;
        }
    }
}