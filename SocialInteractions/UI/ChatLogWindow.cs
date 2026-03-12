using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace SocialInteractions
{
    // This class is deprecated since the chat log is now integrated into the history tab
    // Keeping it for compatibility but it's essentially a no-op
    public class ChatLogWindow : Window
    {
        public ChatLogWindow()
        {
            // This window is deprecated
        }

        public override Vector2 InitialSize => new Vector2(1f, 1f);

        public override void DoWindowContents(Rect inRect)
        {
            // This window is deprecated
            this.Close();
        }
    }
}