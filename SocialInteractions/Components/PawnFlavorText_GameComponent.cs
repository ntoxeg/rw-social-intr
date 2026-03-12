using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SocialInteractions
{
    /// <summary>
    /// GameComponent to handle saving and loading of custom pawn flavor texts across the entire game
    /// </summary>
    public class PawnFlavorText_GameComponent : GameComponent
    {
        // Dictionary to store pawn flavor texts during gameplay
        private Dictionary<int, string> pawnFlavorTexts = new Dictionary<int, string>();

        public PawnFlavorText_GameComponent()
        {
        }

        public PawnFlavorText_GameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            
            // Expose the dictionary for saving/loading
            Scribe_Collections.Look(ref pawnFlavorTexts, "pawnFlavorTexts", LookMode.Value, LookMode.Value);
        }

        /// <summary>
        /// Gets the flavor text for a pawn
        /// </summary>
        public string GetFlavorText(int pawnId)
        {
            if (pawnFlavorTexts.ContainsKey(pawnId))
            {
                return pawnFlavorTexts[pawnId];
            }
            return string.Empty;
        }

        /// <summary>
        /// Sets the flavor text for a pawn
        /// </summary>
        public void SetFlavorText(int pawnId, string flavorText)
        {
            if (string.IsNullOrEmpty(flavorText))
            {
                pawnFlavorTexts.Remove(pawnId);
            }
            else
            {
                pawnFlavorTexts[pawnId] = flavorText;
            }
        }

        /// <summary>
        /// Called to sync with the static dictionary
        /// </summary>
        public void SyncWithStaticDictionary()
        {
            // Load data from this component to the static dictionary
            foreach (var kvp in pawnFlavorTexts)
            {
                SocialInteractions.PawnFlavorTexts[kvp.Key] = kvp.Value;
            }
            
            // Also save any data that might have been set in the static dictionary
            foreach (var kvp in SocialInteractions.PawnFlavorTexts)
            {
                pawnFlavorTexts[kvp.Key] = kvp.Value;
            }
        }
        
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            SyncWithStaticDictionary();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            SyncWithStaticDictionary();
        }
    }
}