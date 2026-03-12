
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;
using System.Collections.Generic;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(LordJob_Joinable_Party), "CreateGraph")]
    public static class LordJob_Joinable_Party_CreateGraph_Patch
    {
        private static Dictionary<Lord, int> monologueCooldowns = new Dictionary<Lord, int>();
        private const int MonologueCooldownTicks = 600; // 10 seconds

        public static void Postfix(LordJob_Joinable_Party __instance, StateGraph __result)
        {
            if (Current.Game.tickManager.TicksGame % 1800 == 0)
            {
                CleanupExpiredCooldowns();
            }

            Lord lord = __instance.lord;
            if (lord == null)
            {
                SLog.Warning("Lord is null, cannot start monologue.");
                return;
            }

            if (monologueCooldowns.ContainsKey(lord) && Find.TickManager.TicksGame < monologueCooldowns[lord])
            {
                return; // On cooldown
            }

            monologueCooldowns[lord] = Find.TickManager.TicksGame + MonologueCooldownTicks;

            Pawn organizer = Traverse.Create(__instance).Field("organizer").GetValue<Pawn>();
            if (organizer == null || !organizer.IsColonistPlayerControlled)
            {
                return;
            }

            GatheringDef gatheringDef = Traverse.Create(__instance).Field("gatheringDef").GetValue<GatheringDef>();

            string subject = "is starting a party";
            if (gatheringDef != null && gatheringDef.defName.ToLower().Contains("concert"))
            {
                subject = "is starting a concert";
            }

            SocialInteractions.HandleMonologue(organizer, subject, true, "speech");
        }

        private static void CleanupExpiredCooldowns()
        {
            List<Lord> lordsToRemove = new List<Lord>();
            foreach (var entry in monologueCooldowns)
            {
                if (Find.TickManager.TicksGame >= entry.Value)
                {
                    lordsToRemove.Add(entry.Key);
                }
            }

            foreach (Lord l in lordsToRemove)
            {
                monologueCooldowns.Remove(l);
            }
        }
    }
}
