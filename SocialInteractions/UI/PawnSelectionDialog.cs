using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace SocialInteractions
{
    public class PawnSelectionDialog : Window
    {
        private List<Pawn> pawns;
        private Vector2 scrollPosition = Vector2.zero;
        private const float EntryHeight = 40f;

        public PawnSelectionDialog(List<Pawn> pawns)
        {
            this.pawns = pawns;
            doCloseButton = false;
            doCloseX = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(350f, 500f);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30), "Select Voice for Colonist");

            // Calculate available space for pawn list
            float listY = 40f;
            float listHeight = inRect.height - 80f; // Leave space for buttons at bottom

            Rect listRect = new Rect(0, listY, inRect.width - 16f, listHeight);

            // Create scrollable list of pawns
            float contentHeight = pawns.Count * EntryHeight;
            Widgets.BeginScrollView(listRect, ref scrollPosition, new Rect(0, 0, listRect.width - 16f, contentHeight));

            try
            {
                float y = 0f;
                for (int i = 0; i < pawns.Count; i++)
                {
                    Rect entryRect = new Rect(0, y, listRect.width, EntryHeight);

                    // Get current voice assignment
                    var manager = Current.Game.GetComponent<VoiceAssignmentManager>();
                    string currentVoice = manager != null ? manager.GetVoiceForPawn(pawns[i]) : null;

                    // Draw pawn info
                    Text.Font = GameFont.Small;

                    string pawnLabel = pawns[i].Name != null ? pawns[i].Name.ToStringShort : "Unknown Pawn";
                    if (!string.IsNullOrEmpty(currentVoice))
                    {
                        pawnLabel += " (Voice: " + currentVoice + ")";
                    }

                    Widgets.Label(entryRect, pawnLabel);

                    // Add a small portrait if possible
                    // if (pawns[i].def != null && pawns[i].def.race != null && pawns[i].def.uiIcon != null)
                    // {
                    //     Rect portraitRect = new Rect(entryRect.width - 40f, y + 5f, 30f, 30f);
                    //     Widgets.DrawTextureFitted(portraitRect, pawns[i].def.uiIcon, 1f);
                    // }

                    // Make the entire entry clickable
                    if (Widgets.ButtonInvisible(entryRect))
                    {
                        // Store the selected pawn and close this dialog first
                        Pawn selectedPawn = pawns[i]; // Capture the pawn to avoid closure issues
                        Close();

                        // Use a small delay to ensure UI state is settled before opening new window
                        Verse.LongEventHandler.ExecuteWhenFinished(() => {
                            SocialInteractions.OpenVoiceSelectionDialog(selectedPawn);
                        });
                        return;
                    }

                    // Add a visual separator
                    Widgets.DrawLineHorizontal(0, y + EntryHeight, inRect.width);

                    y += EntryHeight;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }

            // Close button at bottom
            Rect closeButton = new Rect(10f, inRect.height - 40f, inRect.width - 20f, 30f);
            if (Widgets.ButtonText(closeButton, "Close"))
            {
                Close();
            }
        }
    }
}