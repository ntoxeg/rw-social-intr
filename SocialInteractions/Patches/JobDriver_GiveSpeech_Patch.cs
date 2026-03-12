using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;
using System.Linq;
using System.Collections.Generic;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(RimWorld.JobDriver_GiveSpeech), "MakeNewToils")]
    public static class JobDriver_GiveSpeech_Patch
    {
        // Cooldown to prevent triggering monologue multiple times for the same speech
        private static Dictionary<Pawn, int> monologueCooldowns = new Dictionary<Pawn, int>();
        private const int MonologueCooldownTicks = 600; // 10 seconds

        public static void Postfix(RimWorld.JobDriver_GiveSpeech __instance)
        {
            // Cleanup expired cooldowns periodically
            if (Current.Game.tickManager.TicksGame % 1800 == 0)
            {
                CleanupExpiredCooldowns();
            }

            if (__instance.pawn == null || !__instance.pawn.IsColonistPlayerControlled)
            {
                return;
            }

            // Check if this pawn is on cooldown
            if (monologueCooldowns.ContainsKey(__instance.pawn) && Find.TickManager.TicksGame < monologueCooldowns[__instance.pawn])
            {
                return; // On cooldown, do nothing
            }

            // Put the pawn on cooldown
            monologueCooldowns[__instance.pawn] = Find.TickManager.TicksGame + MonologueCooldownTicks;

            string subject = "is about to give a speech";

            Lord lord = __instance.pawn.GetLord();
            if (lord != null)
            {
                LordJob_Ritual lordJob_Ritual = lord.LordJob as LordJob_Ritual;
                if (lordJob_Ritual != null)
                {
                    Precept_Ritual ritual = lordJob_Ritual.Ritual;
                    if (ritual != null)
                    {
                        // Log all roles and assigned pawns for debugging
                        foreach (var group in lordJob_Ritual.assignments.RoleGroups())
                        {
                            SLog.Message("[SocialInteractions] JobDriver_GiveSpeech_Patch: Ritual role ID: " + group.Key);
                            foreach (var role in group)
                            {
                                foreach (Pawn p in lordJob_Ritual.assignments.AssignedPawns(role))
                                {
                                    SLog.Message("[SocialInteractions] JobDriver_GiveSpeech_Patch: Pawn in role " + group.Key + ": " + p.Name.ToStringShort);
                                }
                            }
                        }

                        Pawn executioner = lordJob_Ritual.assignments.FirstAssignedPawn("executioner");
                        Pawn prisoner = lordJob_Ritual.assignments.FirstAssignedPawn("prisoner");

                        if (executioner != null && prisoner != null)
                        {
                            subject = "is giving a speech for the execution of " + prisoner.Name.ToStringShort;
                            SLog.Message("[SocialInteractions] JobDriver_GiveSpeech_Patch: Found executioner and prisoner, subject: " + subject);
                        }
                        else
                        {
                            // Log which pawns are null for debugging
                            if (executioner == null)
                                SLog.Message("[SocialInteractions] JobDriver_GiveSpeech_Patch: executioner is null");
                            if (prisoner == null)
                                SLog.Message("[SocialInteractions] JobDriver_GiveSpeech_Patch: prisoner is null");
                            
                            subject = "is giving a speech for the " + ritual.LabelCap + " ritual";
                            SLog.Message("[SocialInteractions] JobDriver_GiveSpeech_Patch: Using fallback subject: " + subject);
                        }
                    }
                }
            }

            SocialInteractions.HandleMonologue(__instance.pawn, subject, true, "speech");
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