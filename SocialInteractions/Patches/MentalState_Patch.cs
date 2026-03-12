using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(Verse.AI.MentalStateHandler), "TryStartMentalState")]
    public static class MentalState_Patch
    {
        // Cooldown to prevent triggering monologue multiple times for the same event
        private static Dictionary<Pawn, int> monologueCooldowns = new Dictionary<Pawn, int>();
        private const int MonologueCooldownTicks = 600; // 10 seconds

        public static void Postfix(Verse.AI.MentalStateHandler __instance, bool __result, MentalStateDef stateDef, string reason = null)
        {
            // Exclude SocialFighting as it is usually preceded by a dialogue interaction
            if (stateDef == MentalStateDefOf.SocialFighting)
            {
                return;
            }

            // Cleanup expired cooldowns periodically
            if (Current.Game != null && Current.Game.tickManager != null && Current.Game.tickManager.TicksGame % 1800 == 0)
            {
                CleanupExpiredCooldowns();
            }

            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();

            // Only trigger if the mental state was successfully started and the pawn is a player colonist
            if (!__result || pawn == null || !pawn.IsColonist)
            {
                return;
            }

            // Check if this pawn is on cooldown
            if (monologueCooldowns.ContainsKey(pawn) && Find.TickManager.TicksGame < monologueCooldowns[pawn])
            {
                return; // On cooldown, do nothing
            }

            // Put the pawn on cooldown
            monologueCooldowns[pawn] = Find.TickManager.TicksGame + MonologueCooldownTicks;

            // The subject of the monologue will be the label of the mental state (e.g., "Berserk", "Sad wander")
            // Include the reason if available
            string subject;
            if (!string.IsNullOrEmpty(reason))
            {
                subject = " is experiencing " + stateDef.LabelCap + " because " + reason;
            }
            else
            {
                subject = " is experiencing " + stateDef.LabelCap;
            }

            // Call the monologue handler
            SocialInteractions.HandleMonologue(pawn, subject, true, "monologue");
        }

        private static void CleanupExpiredCooldowns()
        {
            List<Pawn> pawnsToRemove = new List<Pawn>();
            foreach (var entry in monologueCooldowns)
            {
                if (Find.TickManager.TicksGame >= entry.Value)
                {
                    pawnsToRemove.Add(entry.Key);
                }
            }

            foreach (Pawn p in pawnsToRemove)
            {
                monologueCooldowns.Remove(p);
            }
        }
    }
}
