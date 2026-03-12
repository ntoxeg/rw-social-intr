using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SocialInteractions
{
    /// <summary>
    /// Harmony patch for TaleRecorder.RecordTale to intercept birth events
    /// </summary>
    [HarmonyPatch(typeof(TaleRecorder), "RecordTale", new System.Type[] { typeof(TaleDef), typeof(object[]) })]
    public static class TaleRecorder_RecordTale_Patch
    {
        // Postfix to handle the birth event
        public static void Postfix(TaleDef def, object[] args)
        {
            // Check if this is a birth tale
            if (def == TaleDefOf.GaveBirth)
            {
                SLog.Message(string.Format("[SocialInteractions] TaleRecorder_RecordTale_Patch.Postfix called with TaleDef: {0}", def != null ? def.defName : "null"));
                // SLog.Message("[SocialInteractions] Detected GaveBirth tale");
                
                // Try to cast the arguments to pawns
                Pawn mother = null;
                Pawn baby = null;
                
                // The arguments are passed as an object array
                if (args != null && args.Length >= 2)
                {
                    // SLog.Message(string.Format("[SocialInteractions] args array has {0} elements", args.Length));
                    mother = args[0] as Pawn;
                    baby = args[1] as Pawn;
                    
                    // SLog.Message(string.Format("[SocialInteractions] Mother: {0}, Baby: {1}", 
                    //     mother != null ? mother.LabelShort : "null",
                    //     baby != null ? baby.LabelShort : "null"));
                }
                else
                {
                    SLog.Message("[SocialInteractions] args array is null or has less than 2 elements");
                }
                
                if (mother != null && baby != null)
                {
                    // SLog.Message("[SocialInteractions] Found mother and baby, looking for doctor");
                    
                    // Try to find the doctor who delivered the baby
                    Pawn doctor = FindDoctorWhoDeliveredBaby(mother);
                    
                    // SLog.Message(string.Format("[SocialInteractions] Found doctor: {0}", doctor != null ? doctor.LabelShort : "null"));
                    
                    // If we found a doctor and it's not the mother herself, trigger the LLM interaction
                    if (doctor != null && doctor != mother)
                    {
                        // Create a descriptive subject for the interaction
                        string subject = CreateBirthSubject(doctor, mother, baby);
                        
                        SLog.Message(string.Format("[SocialInteractions] Triggering LLM interaction between doctor {0} and mother {1} about {2}", 
                            doctor.LabelShort, mother.LabelShort, subject));
                        
                        // Trigger the LLM interaction between doctor and mother
                        SocialInteractions.HandleNonStoppingInteraction(doctor, mother, SI_InteractionDefOf.TendPatient, subject, true);
                    }
                    else
                    {
                        SLog.Message("[SocialInteractions] No doctor found or doctor is mother");
                    }
                }
                else
                {
                    SLog.Message("[SocialInteractions] Mother or baby is null");
                }
            }
        }
        
        // Helper method to create a descriptive subject for the birth event
        private static string CreateBirthSubject(Pawn doctor, Pawn mother, Pawn baby)
        {
            // Determine the baby's health status
            string healthStatus = "healthy";
            if (baby.health != null)
            {
                // Check if the baby has any serious health conditions
                Hediff illness = baby.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.InfantIllness);
                Hediff stillborn = baby.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Stillborn);
                
                if (stillborn != null)
                {
                    healthStatus = "stillborn";
                }
                else if (illness != null)
                {
                    healthStatus = "sick";
                }
            }
            
            // Get the baby's gender
            string gender = baby.gender.ToString().ToLower();
            
            // Create the subject
            return string.Format("{0} helped {1} give birth to a {2} {3} baby", 
                doctor.LabelShort, mother.LabelShort, healthStatus, gender);
        }
        
        // Helper method to find the doctor who delivered the baby
        private static Pawn FindDoctorWhoDeliveredBaby(Pawn mother)
        {
            // Check if there's a doctor nearby who has medical skills
            if (mother.Map != null && mother.Map.listerThings != null)
            {
                List<Thing> nearbyThings = mother.Map.listerThings.ThingsOfDef(ThingDefOf.Human);
                if (nearbyThings != null)
                {
                    Pawn bestCandidate = null;
                    int bestMedicalSkill = -1;
                    
                    foreach (Thing thing in nearbyThings)
                    {
                        Pawn pawn = thing as Pawn;
                        if (pawn != null && pawn != mother && pawn.Position.DistanceTo(mother.Position) <= 10f)
                        {
                            // Check if this pawn has medical skills
                            if (pawn.skills != null)
                            {
                                SkillRecord medicalSkill = pawn.skills.GetSkill(SkillDefOf.Medicine);
                                if (medicalSkill != null)
                                {
                                    // Prefer pawns with higher medical skills
                                    if (medicalSkill.Level > bestMedicalSkill)
                                    {
                                        bestMedicalSkill = medicalSkill.Level;
                                        bestCandidate = pawn;
                                    }
                                }
                            }
                            
                            // Also check current job
                            if (pawn.jobs != null && pawn.jobs.curJob != null)
                            {
                                // If they're currently doing a medical job, they're probably the doctor
                                if (pawn.jobs.curJob.def == JobDefOf.TendPatient || 
                                    pawn.jobs.curJob.def == JobDefOf.CarryToMomAfterBirth ||
                                    pawn.jobs.curJob.def.defName == "AssistInChildbirth")
                                {
                                    return pawn;
                                }
                            }
                        }
                    }
                    
                    // If we found a candidate with medical skills, return them
                    if (bestCandidate != null)
                    {
                        SLog.Message(string.Format("[SocialInteractions] Found doctor {0} with medical skill {1} for mother {2}", 
                            bestCandidate.LabelShort, bestMedicalSkill, mother.LabelShort));
                        return bestCandidate;
                    }
                }
            }
            
            // If we still haven't found a doctor, return null
            return null;
        }
    }
}