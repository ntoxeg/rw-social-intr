using HarmonyLib;
using RimWorld;
using Verse;
using SocialInteractions;

namespace SocialInteractions.Patches
{
    [HarmonyPatch(typeof(Precept_RoleSingle), "Assign")]
    public static class Precept_RoleSingle_Patch
    {
        public static void Postfix(Precept_RoleSingle __instance, Pawn p)
        {
            // A role was assigned, not unassigned
            if (p == null || !p.IsColonistPlayerControlled)
            {
                return;
            }

            string subject = " has been assigned the role of " + __instance.LabelCap;

            // Call the monologue handler
            SocialInteractions.HandleMonologue(p, subject, true, "speech");
        }
    }
}
