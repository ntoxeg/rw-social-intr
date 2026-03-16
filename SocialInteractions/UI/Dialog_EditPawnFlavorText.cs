using RimWorld;
using UnityEngine;
using Verse;
using System.Threading.Tasks;
using SocialInteractions;

namespace SocialInteractions.UI
{
    /// <summary>
    /// Dialog window for editing a pawn's custom flavor text.
    /// Shows a read-only dossier (facts) at the top and an editable persona section below.
    /// </summary>
    public class Dialog_EditPawnFlavorText : Window
    {
        private Pawn pawn;
        private string flavorText;
        private string initialFlavorText;
        private string dossierText;
        private bool isGenerating = false;

        // Memory tab state
        private bool showingMemoryTab = false;
        private string memoryText = "";
        private bool isEditingMemory = false;
        private Vector2 memoryScrollPosition = Vector2.zero;

        private const float WindowWidth = 620f;
        private const float WindowHeight = 540f;
        private const float MyMargin = 17f;
        private const float ButtonHeight = 35f;
        private const float SectionSpacing = 10f;

        public override Vector2 InitialSize
        {
            get { return new Vector2(WindowWidth, WindowHeight); }
        }

        public Dialog_EditPawnFlavorText(Pawn pawn)
        {
            this.pawn = pawn;
            this.flavorText = SocialInteractions.GetPawnFlavorText(pawn);
            this.initialFlavorText = this.flavorText;
            this.dossierText = SocialInteractions.BuildDossier(pawn);
            
            if (SocialInteractions.Settings.Memory.enableMemorySystem)
            {
                this.memoryText = Services.Memory.GetMemory(pawn.thingIDNumber);
            }

            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float curY = MyMargin;

            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(MyMargin, curY, inRect.width - 2 * MyMargin, 30f),
                string.Format("SocialInteractions_EditBioFor".Translate(), pawn.Name.ToStringShort));
            curY += 35f;

            Text.Font = GameFont.Small;

            // Tab toggle
            if (SocialInteractions.Settings.Memory.enableMemorySystem)
            {
                float tabWidth = 120f;
                Rect bioTabRect = new Rect(MyMargin, curY, tabWidth, 30f);
                Rect memTabRect = new Rect(MyMargin + tabWidth + 10f, curY, tabWidth, 30f);

                GUI.color = !showingMemoryTab ? Color.white : Color.grey;
                if (Widgets.ButtonText(bioTabRect, "Bio"))
                {
                    showingMemoryTab = false;
                }

                GUI.color = showingMemoryTab ? Color.white : Color.grey;
                if (Widgets.ButtonText(memTabRect, "Memories"))
                {
                    showingMemoryTab = true;
                }
                GUI.color = Color.white;

                curY += 30f + SectionSpacing;
            }

            if (!showingMemoryTab)
            {
                DrawBioTab(inRect, curY);
            }
            else
            {
                DrawMemoriesTab(inRect, curY);
            }
        }

        private void DrawBioTab(Rect inRect, float startY)
        {
            float curY = startY;
            float btnY = inRect.height - ButtonHeight - MyMargin;
            float contentWidth = inRect.width - 2 * MyMargin;
            float contentHeight = btnY - curY - SectionSpacing;

            // Calculate dossier height
            float dossierHeight = Text.CalcHeight(dossierText, contentWidth - 10f) + 8f;
            // Cap dossier to about 40% of available space so persona always has room
            float maxDossierHeight = contentHeight * 0.4f;
            if (dossierHeight > maxDossierHeight)
                dossierHeight = maxDossierHeight;

            // Dossier section (read-only, rendered as a label in a subtle box)
            Rect dossierRect = new Rect(MyMargin, curY, contentWidth, dossierHeight);
            Widgets.DrawBoxSolid(dossierRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
            Widgets.DrawBox(dossierRect);

            // If dossier is taller than cap, use scroll view
            if (Text.CalcHeight(dossierText, contentWidth - 10f) + 8f > maxDossierHeight)
            {
                Rect dossierViewRect = new Rect(0f, 0f, contentWidth - 20f, Text.CalcHeight(dossierText, contentWidth - 26f) + 8f);
                Widgets.BeginScrollView(dossierRect, ref dossierScrollPosition, dossierViewRect);
                Widgets.Label(new Rect(5f, 4f, dossierViewRect.width, dossierViewRect.height), dossierText);
                Widgets.EndScrollView();
            }
            else
            {
                Widgets.Label(new Rect(dossierRect.x + 5f, dossierRect.y + 4f, contentWidth - 10f, dossierHeight), dossierText);
            }
            curY += dossierHeight + SectionSpacing;

            // Persona section header
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.8f, 0.6f);
            Widgets.Label(new Rect(MyMargin, curY, contentWidth, 20f), "=== PERSONA ===");
            GUI.color = Color.white;
            curY += 22f;

            // Editable text area for persona (personality + quirks)
            float personaHeight = btnY - curY - SectionSpacing;
            Rect textAreaRect = new Rect(MyMargin, curY, contentWidth, personaHeight);

            Rect outRect = textAreaRect;
            float calcHeight = Text.CalcHeight(flavorText, outRect.width - 16f) + 100f;
            float viewHeight = Mathf.Max(calcHeight, outRect.height);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            string newText = Widgets.TextArea(new Rect(0f, 0f, viewRect.width, viewHeight), flavorText);
            Widgets.EndScrollView();

            if (newText != flavorText)
            {
                flavorText = newText;
            }

            // Buttons
            float btnWidth = 130f;
            float btnSpacing = (inRect.width - (MyMargin * 2) - (btnWidth * 4)) / 3f;
            if (btnSpacing < 5f) btnSpacing = 5f;

            float currentX = MyMargin;

            // Cancel button
            Rect cancelButtonRect = new Rect(currentX, btnY, btnWidth, ButtonHeight);
            if (Widgets.ButtonText(cancelButtonRect, "SocialInteractions_Cancel".Translate()))
            {
                Close();
            }
            currentX += btnWidth + btnSpacing;

            // Auto-Generate button
            Rect autoGenButtonRect = new Rect(currentX, btnY, btnWidth, ButtonHeight);

            bool canGenerate = SocialInteractions.Settings.Features.llmInteractionsEnabled &&
                               !string.IsNullOrEmpty(SocialInteractions.Settings.Api.llmApiUrl);

            string autoGenLabel = isGenerating ? "SocialInteractions_AutoGenerateBioGenerating".Translate() : "SocialInteractions_AutoGenerateBio".Translate();

            GUI.color = canGenerate && !isGenerating ? Color.white : Color.grey;

            if (Widgets.ButtonText(autoGenButtonRect, autoGenLabel, true, false, canGenerate && !isGenerating))
            {
                if (canGenerate && !isGenerating)
                {
                    isGenerating = true;
                    Task.Run(async () =>
                    {
                        string result = await SocialInteractions.GenerateBioAsync(pawn);
                        LongEventHandler.ExecuteWhenFinished(() =>
                        {
                            if (!string.IsNullOrEmpty(result))
                            {
                                flavorText = result;
                            }
                            isGenerating = false;
                        });
                    });
                }
            }
            if (!canGenerate)
            {
                TooltipHandler.TipRegion(autoGenButtonRect, "SocialInteractions_AutoGenerateBioDisabledTooltip".Translate());
            }
            else
            {
                TooltipHandler.TipRegion(autoGenButtonRect, "SocialInteractions_AutoGenerateBioTooltip".Translate());
            }
            GUI.color = Color.white;
            currentX += btnWidth + btnSpacing;

            // Clear button
            Rect clearButtonRect = new Rect(currentX, btnY, btnWidth, ButtonHeight);
            if (Widgets.ButtonText(clearButtonRect, "SocialInteractions_Clear".Translate()))
            {
                flavorText = string.Empty;
                SocialInteractions.SetPawnFlavorText(pawn, string.Empty);
            }

            // Save button
            Rect doneButtonRect = new Rect(inRect.width - MyMargin - btnWidth, btnY, btnWidth, ButtonHeight);
            if (Widgets.ButtonText(doneButtonRect, "SocialInteractions_Save".Translate()))
            {
                SocialInteractions.SetPawnFlavorText(pawn, flavorText);
                Close();
            }
        }

        private void DrawMemoriesTab(Rect inRect, float startY)
        {
            float curY = startY;
            float btnY = inRect.height - ButtonHeight - MyMargin;
            float contentWidth = inRect.width - 2 * MyMargin;
            float contentHeight = btnY - curY - SectionSpacing;

            // Memory section header
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.8f, 0.6f);
            Widgets.Label(new Rect(MyMargin, curY, contentWidth, 20f), "=== MEMORIES ===");
            GUI.color = Color.white;
            curY += 22f;

            // Text area for memories
            float memoryAreaHeight = btnY - curY - SectionSpacing;
            Rect textAreaRect = new Rect(MyMargin, curY, contentWidth, memoryAreaHeight);

            Rect outRect = textAreaRect;
            float calcHeight = Text.CalcHeight(memoryText, outRect.width - 16f) + 100f;
            float viewHeight = Mathf.Max(calcHeight, outRect.height);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(outRect, ref memoryScrollPosition, viewRect);
            if (isEditingMemory)
            {
                string newText = Widgets.TextArea(new Rect(0f, 0f, viewRect.width, viewHeight), memoryText);
                if (newText != memoryText)
                {
                    memoryText = newText;
                }
            }
            else
            {
                Widgets.Label(new Rect(0f, 0f, viewRect.width, viewHeight), memoryText);
            }
            Widgets.EndScrollView();

            // Buttons
            float btnWidth = 130f;
            float btnSpacing = (inRect.width - (MyMargin * 2) - (btnWidth * 4)) / 3f;
            if (btnSpacing < 5f) btnSpacing = 5f;

            float currentX = MyMargin;

            // Cancel button
            Rect cancelButtonRect = new Rect(currentX, btnY, btnWidth, ButtonHeight);
            if (Widgets.ButtonText(cancelButtonRect, "SocialInteractions_Cancel".Translate()))
            {
                Close();
            }
            currentX += btnWidth + btnSpacing;

            // Edit button
            Rect editButtonRect = new Rect(currentX, btnY, btnWidth, ButtonHeight);
            if (Widgets.ButtonText(editButtonRect, isEditingMemory ? "Cancel Edit" : "Edit"))
            {
                if (isEditingMemory)
                {
                    // Revert changes
                    memoryText = Services.Memory.GetMemory(pawn.thingIDNumber);
                }
                isEditingMemory = !isEditingMemory;
            }
            currentX += btnWidth + btnSpacing;

            // Clear button
            Rect clearButtonRect = new Rect(currentX, btnY, btnWidth, ButtonHeight);
            if (Widgets.ButtonText(clearButtonRect, "SocialInteractions_Clear".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Are you sure you want to clear all memories for this pawn?",
                    () =>
                    {
                        Services.Memory.ClearMemory(pawn.thingIDNumber);
                        memoryText = "";
                        isEditingMemory = false;
                    }));
            }

            // Save button
            Rect doneButtonRect = new Rect(inRect.width - MyMargin - btnWidth, btnY, btnWidth, ButtonHeight);
            string saveLabel = isEditingMemory ? "Save Memory" : "SocialInteractions_Save".Translate();
            if (Widgets.ButtonText(doneButtonRect, saveLabel))
            {
                if (isEditingMemory)
                {
                    Services.Memory.SetMemory(pawn.thingIDNumber, memoryText);
                    isEditingMemory = false;
                }
                else
                {
                    SocialInteractions.SetPawnFlavorText(pawn, flavorText);
                    Close();
                }
            }
        }

        private Vector2 scrollPosition = Vector2.zero;
        private Vector2 dossierScrollPosition = Vector2.zero;

        public override void PostClose()
        {
            base.PostClose();
        }
    }
}
