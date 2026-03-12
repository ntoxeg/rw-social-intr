using HarmonyLib;
using RimWorld;
using Verse;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(Precept_RoleMulti), "Assign")]
    public static class Precept_RoleMulti_Patch
    {
        public static void Postfix(Precept_RoleMulti __instance, Pawn p)
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
