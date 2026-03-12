using HarmonyLib;
using RimWorld;
using Verse;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(Game), "InitNewGame")]
    public static class Game_InitNewGame_Patch
    {
        public static void Postfix()
        {
            // Create and add the game component for handling pawn flavor text persistence
            Current.Game.components.Add(new PawnFlavorText_GameComponent());
            
            // Reset TTS Manager state on new game
            TTSManager.Initialize();
        }
    }
    
    [HarmonyPatch(typeof(Game), "LoadGame")]
    public static class Game_LoadGame_Patch
    {
        public static void Postfix()
        {
            // Ensure the game component exists when loading a game
            if (Current.Game.GetComponent<PawnFlavorText_GameComponent>() == null)
            {
                Current.Game.components.Add(new PawnFlavorText_GameComponent());
            }
            
            // Reset TTS Manager state on load game
            TTSManager.Initialize();
        }
    }
}