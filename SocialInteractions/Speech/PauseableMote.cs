using Verse;
using UnityEngine;
using System.Text.RegularExpressions;
using RimWorld;

namespace SocialInteractions
{
    /// <summary>
    /// A custom Mote that can be paused and have its display time extended.
    /// </summary>
    [StaticConstructorOnStartup]
    public class PauseableMote : MoteText
    {
        // The original duration the mote was supposed to display
        public float originalDuration;
        
        // Cached stripped text for performance
        private string cachedStrippedText = null;
        private string cachedOriginalText = null;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            
            // Set the override time before start fadeout to our original duration
            this.overrideTimeBeforeStartFadeout = originalDuration;
            
            // Set velocity like the base game does
            this.SetVelocity(Rand.Range(5, 35), Rand.Range(0.42f, 0.45f));
        }
        
        public override void DrawGUIOverlay()
        {
            // Check if we should hide motes
            if (Find.UIRoot != null && Find.UIRoot.HideMotes)
            {
                return;
            }
            
            // Calculate age and alpha
            float ageSecs = AgeSecs;
            float timeBeforeStartFadeout = TimeBeforeStartFadeout;
            float alpha = 1f;
            
            if (ageSecs >= timeBeforeStartFadeout)
            {
                float fadeOutTime = def.mote.fadeOutTime;
                if (fadeOutTime > 0f)
                {
                    alpha = 1f - (ageSecs - timeBeforeStartFadeout) / fadeOutTime;
                }
                else
                {
                    alpha = 0f;
                }
            }
            
            if (alpha <= 0f)
            {
                return;
            }
            
            // Apply alpha to color
            Color color = textColor;
            color.a *= alpha;
            
            // Draw the text with the selected rendering style
            if (!string.IsNullOrEmpty(text))
            {
                if (SocialInteractions.Settings.useBackgroundTextRendering)
                {
                    DrawTextWithBackground(color);
                }
                else
                {
                    // Draw the text with a drop shadow for better readability (original method)
                    DrawTextWithDropShadow(color);
                }
            }
        }
        
        // Method to draw text with drop shadow (original method)
        private void DrawTextWithDropShadow(Color color)
        {
            // Get stripped text (cached for performance)
            string strippedText = GetStrippedText(text);
            
            // Draw drop shadow (1px offset and in black) with higher opacity
            Color shadowColor = new Color(0f, 0f, 0f, color.a * 0.8f);
            Vector2 shadowOffset = new Vector2(0.03f, -0.03f);
            GenMapUI.DrawText(new Vector2(exactPosition.x + shadowOffset.x, exactPosition.z + shadowOffset.y), strippedText, shadowColor);
            
            // Draw the main text with rich text formatting
            GenMapUI.DrawText(new Vector2(exactPosition.x, exactPosition.z), text, color);
        }
        
        // Method to draw text with background (new method)
        private void DrawTextWithBackground(Color color)
        {
            // Convert world position to screen position
            Vector3 worldPos = new Vector3(exactPosition.x, 0f, exactPosition.z);
            Vector2 screenPos = Find.Camera.WorldToScreenPoint(worldPos) / Prefs.UIScale;
            screenPos.y = UI.screenHeight - screenPos.y;
            
            // Set font
            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            
            // Get stripped text for size calculation
            string strippedText = GetStrippedText(text);
            Vector2 textSize = Text.CalcSize(strippedText);
            
            // Calculate background rectangle with proper height for multiline text
            float extraWidth = (Prefs.DisableTinyText ? 6f : 4f);
            float extraHeight = (Prefs.DisableTinyText ? 4f : 2f); // Extra padding for top/bottom
            float bgHeight = textSize.y + extraHeight * 2f;
            Rect bgRect = new Rect(screenPos.x - textSize.x / 2f - extraWidth, screenPos.y - bgHeight / 2f, textSize.x + extraWidth * 2f, bgHeight);
            
            // Draw background using the same texture as the base game
            GUI.color = new Color(1f, 1f, 1f, color.a);
            GUI.DrawTexture(bgRect, TexUI.GrayTextBG);
            
            // Draw the main text with rich text formatting
            GUI.color = color;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(bgRect, text);
            
            // Reset GUI settings
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;
        }
        
        // Method to get stripped text with caching for performance
        private string GetStrippedText(string input)
        {
            // Return cached version if available and input hasn't changed
            if (cachedStrippedText != null && cachedOriginalText == input)
            {
                return cachedStrippedText;
            }
            
            // Cache the input and stripped version
            cachedOriginalText = input;
            cachedStrippedText = StripRichTextTags(input);
            return cachedStrippedText;
        }
        
        // Method to strip rich text tags from a string
        private string StripRichTextTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            
            // Remove all <color=...> and </color> tags
            return Regex.Replace(input, @"<color=.*?>|</color>", "", RegexOptions.IgnoreCase);
        }
    }
}