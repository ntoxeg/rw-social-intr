using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace SocialInteractions
{
    public class VoiceSelectionDialog : Window
    {
        private Pawn targetPawn;
        private List<string> availableVoices;
        private string selectedVoice;
        private Vector2 scrollPosition = Vector2.zero;
        private const float EntryHeight = 24f;

        public VoiceSelectionDialog(Pawn pawn)
        {
            targetPawn = pawn;

            // Get available voices from the voice assignment manager
            var manager = Current.Game.GetComponent<VoiceAssignmentManager>();
            if (manager != null)
            {
                availableVoices = VoiceAssignmentManager.AvailableVoices.ToList();
            }
            else
            {
                availableVoices = new List<string>();
            }

            // Get currently assigned voice
            selectedVoice = manager != null ? manager.GetVoiceForPawn(targetPawn) : null;

            // Set window properties
            doCloseButton = false;
            doCloseX = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(400f, 500f);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            string pawnName = targetPawn.Name != null ? targetPawn.Name.ToStringShort : "Unknown Pawn";
            string title = "Select Voice for " + pawnName;
            Widgets.Label(new Rect(0, 0, inRect.width, 30), title);

            // Re-fetch available voices in case they changed since dialog opened
            var voiceManager = Current.Game.GetComponent<VoiceAssignmentManager>();
            if (voiceManager != null)
            {
                availableVoices = VoiceAssignmentManager.AvailableVoices.ToList();
                // Update selected voice in case it changed
                if (selectedVoice == null)
                {
                    selectedVoice = voiceManager.GetVoiceForPawn(targetPawn);
                }
            }

            // Calculate available space for voice list
            float buttonY = 40f;
            float listHeight = inRect.height - 80f; // Leave space for buttons at bottom

            Rect listRect = new Rect(0, buttonY, inRect.width - 16f, listHeight);

            // Create scrollable list of voices
            float contentHeight = availableVoices.Count * EntryHeight;
            Widgets.BeginScrollView(listRect, ref scrollPosition, new Rect(0, 0, listRect.width - 16f, contentHeight));

            try
            {
                float y = 0f;
                for (int i = 0; i < availableVoices.Count; i++)
                {
                    Rect entryRect = new Rect(0, y, listRect.width, EntryHeight);

                    // Highlight selected voice
                    if (availableVoices[i] == selectedVoice)
                    {
                        Widgets.DrawHighlight(entryRect);
                    }

                    // Draw voice name
                    Text.Font = GameFont.Small;
                    Widgets.Label(entryRect, availableVoices[i]);

                    // Make clickable
                    if (Widgets.ButtonInvisible(entryRect))
                    {
                        selectedVoice = availableVoices[i];
                    }

                    y += EntryHeight;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }

            // Apply and Cancel buttons
            float buttonWidth = (inRect.width - 20f) / 2f;
            Rect applyButton = new Rect(10f, inRect.height - 40f, buttonWidth, 30f);
            Rect cancelButton = new Rect(20f + buttonWidth, inRect.height - 40f, buttonWidth, 30f);

            if (Widgets.ButtonText(applyButton, "Apply"))
            {
                if (selectedVoice != null)
                {
                    // Assign the voice to the pawn
                    var manager = Current.Game.GetComponent<VoiceAssignmentManager>();
                    if (manager != null)
                    {
                        manager.SetVoiceForPawn(targetPawn, selectedVoice);
                        string assignedPawnName = targetPawn.Name != null ? targetPawn.Name.ToStringShort : "Unknown Pawn";
                        Messages.Message(string.Format("Voice assigned successfully to {0}.", assignedPawnName), MessageTypeDefOf.TaskCompletion);
                    }
                }
                Close();
            }

            if (Widgets.ButtonText(cancelButton, "Cancel"))
            {
                Close();
            }
        }
    }
}