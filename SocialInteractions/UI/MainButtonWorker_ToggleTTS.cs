using System;
using UnityEngine;
using Verse;
using RimWorld;

namespace SocialInteractions
{
    public class MainButtonWorker_ToggleTTS : MainButtonWorker
    {
        public override void Activate()
        {
            // Toggle mute state
            SocialInteractions.Settings.ttsMuted = !SocialInteractions.Settings.ttsMuted;
            
            if (SocialInteractions.Settings.ttsMuted)
            {
                TTSManager.Stop();
                Messages.Message("TTS Muted", MessageTypeDefOf.NeutralEvent, false);
            }
            else
            {
                Messages.Message("TTS Unmuted", MessageTypeDefOf.NeutralEvent, false);
            }
        }

        public override void DoButton(Rect rect)
        {
            // Custom draw logic - Draw background
            // Use Widgets.DrawAtlas or similar if we have textures, but for now just drawing a rect
            // MainButtons usually rely on the def.iconPath fetching a texture and MainButtonWorker.DoButton drawing it.
            // Since we override DoButton completely and our def has a placeholder icon, we should draw our own background.
            
            if (SocialInteractions.Settings.ttsMuted)
            {
                // Muted state: Red background
                Widgets.DrawRectFast(rect, new Color(0.6f, 0.2f, 0.2f)); 
                if (Mouse.IsOver(rect))
                {
                    Widgets.DrawHighlight(rect);
                }
            }
            else
            {
                 // Active state: Standard grey/black background of main buttons is usually implicit or drawn by the bar.
                 // We can force a dark grey background to make it solid.
                 Widgets.DrawRectFast(rect, new Color(0.15f, 0.15f, 0.15f));
                 if (Mouse.IsOver(rect))
                 {
                     Widgets.DrawHighlight(rect);
                 }
            }

            // Draw icon/label
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            
            string label = SocialInteractions.Settings.ttsMuted ? "TTS: OFF" : "TTS: ON";
            Widgets.Label(rect, label);
            
            Text.Anchor = TextAnchor.UpperLeft;

            // Handle click
            if (Widgets.ButtonInvisible(rect))
            {
                Activate();
            }
        }
    }
}
