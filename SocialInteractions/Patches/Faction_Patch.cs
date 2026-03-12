using HarmonyLib;
using RimWorld;
using Verse;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(Faction), "TryGenerateNewLeader")]
    public static class Faction_Patch
    {
        public static void Postfix(Faction __instance, bool __result)
        {
            // Only trigger if a new leader was successfully generated and it's the player's faction
            // Check if player faction exists before comparing to avoid errors during world generation
            if (!__result || !TryIsPlayerFaction(__instance))
            {
                return;
            }

            Pawn newLeader = __instance.leader;
            if (newLeader == null || !newLeader.IsColonistPlayerControlled)
            {
                return;
            }

            string subject = " has become the new leader";

            // Call the monologue handler
            SocialInteractions.HandleMonologue(newLeader, subject, true, "speech");
        }

        // Helper method to safely check if a faction is the player faction
        private static bool TryIsPlayerFaction(Faction faction)
        {
            try
            {
                // Check if the player faction is available
                if (Faction.OfPlayerSilentFail == null)
                {
                    return false;
                }

                return faction == Faction.OfPlayerSilentFail;
            }
            catch
            {
                // If there's any issue accessing the player faction, return false
                return false;
            }
        }
    }
}
