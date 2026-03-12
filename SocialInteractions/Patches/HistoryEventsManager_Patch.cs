using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(HistoryEventsManager), "RecordEvent")]
    public static class HistoryEventsManager_Patch
    {
        // Cooldown to prevent triggering monologue multiple times for the same event
        private static Dictionary<Pawn, int> monologueCooldowns = new Dictionary<Pawn, int>();
        private const int MonologueCooldownTicks = 600; // 10 seconds

        public static void Postfix(HistoryEvent historyEvent)
        {
            // Cleanup expired cooldowns periodically
            if (Current.Game.tickManager.TicksGame % 1800 == 0)
            {
                CleanupExpiredCooldowns();
            }

            // We only care about the Bonded event for now
            if (historyEvent.def != HistoryEventDefOf.Bonded)
            {
                return;
            }

            Pawn doer = historyEvent.args.GetArg<Pawn>(HistoryEventArgsNames.Doer);
            if (doer == null || !doer.IsColonistPlayerControlled)
            {
                return;
            }

            // Check if this pawn is on cooldown
            if (monologueCooldowns.ContainsKey(doer) && Find.TickManager.TicksGame < monologueCooldowns[doer])
            {
                return; // On cooldown, do nothing
            }

            // Put the pawn on cooldown
            monologueCooldowns[doer] = Find.TickManager.TicksGame + MonologueCooldownTicks;

            string subject = " bonded with an animal";

            SocialInteractions.HandleMonologue(doer, subject, false, "monologue");
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