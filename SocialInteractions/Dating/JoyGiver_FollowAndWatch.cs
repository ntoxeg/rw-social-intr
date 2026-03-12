using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;

namespace SocialInteractions
{
    public class JoyGiver_FollowAndWatch : JoyGiver
    {
        // This TryGiveJob is for the initial job assignment, which is not used in this context.
        // The relevant TryGiveJob is the one that takes a Thing as a parameter.
        public override Job TryGiveJob(Pawn pawn)
        {
            return null;
        }

        // Overload to try and give a job to join an existing joy activity
        public Job TryGiveJob(Pawn pawn, Thing joyThing)
        {
            if (pawn == null || joyThing == null) return null;

            Building joyBuilding = joyThing as Building;
            if (joyBuilding == null) return null;

            // Find the JoyGiverDef associated with the joyBuilding's joyKind
            JoyGiverDef joyGiverDef = DefDatabase<JoyGiverDef>.AllDefs.FirstOrDefault(jg => jg.joyKind == joyBuilding.def.building.joyKind);

            if (joyGiverDef == null || joyGiverDef.jobDef == null) return null;

            // Check if the joy activity allows multiple participants
            if (joyGiverDef.jobDef.joyMaxParticipants <= 1) return null;

            // Check if the pawn can reserve and reach the joyThing
            if (!pawn.CanReserveAndReach(joyBuilding, PathEndMode.InteractionCell, Danger.None)) return null;

            // Create a job for the pawn to join the joy activity
            Job job = JobMaker.MakeJob(joyGiverDef.jobDef, joyBuilding);
            return job;
        }
    }
}