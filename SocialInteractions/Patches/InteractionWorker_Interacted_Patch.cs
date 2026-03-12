using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    [HarmonyPatch(typeof(InteractionWorker), "Interacted")]
    public static class InteractionWorker_Interacted_Patch
    {
        public static void Postfix(InteractionWorker __instance, Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, string letterText, string letterLabel, LetterDef letterDef, LookTargets lookTargets)
        {
            // SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Interacted_Patch.Postfix called. pawnsStopOnInteraction: {0}", SocialInteractions.Settings.pawnsStopOnInteraction));

            // Check if the recipient is a child and the interaction is an insult, and misbehavior is enabled
            if (recipient != null && recipient.RaceProps.Humanlike && ChildrenMisbehaviorManager.IsChild(recipient) && SocialInteractions.Settings.enableChildrenMisbehavior)
            {
                // Get the interaction definition from the __instance parameter
                InteractionDef interactionDef = __instance.interaction;

                if (interactionDef != null)
                {
                    // Check if this is an insult interaction
                    if (IsInsultInteraction(interactionDef))
                    {
                        SLog.Message(string.Format("[SocialInteractions] Child {0} received insult from {1}: {2}",
                            recipient.LabelShort, initiator.LabelShort, interactionDef.defName));

                        // Give the child a chance to go cry to their parent about being insulted
                        TryStartCryingToParent(recipient, initiator, interactionDef);
                    }
                }
            }

            // Only handle interactions when pawns stop on interaction (stopping interactions)
            if (SocialInteractions.Settings.pawnsStopOnInteraction)
            {
                // SLog.Message("[SocialInteractions] pawnsStopOnInteraction is true, proceeding with job creation.");

                // Get the interaction definition from the __instance parameter
                InteractionDef interactionDef = __instance.interaction;
                // SLog.Message(string.Format("[SocialInteractions] InteractionDef retrieved: {0}", interactionDef != null ? interactionDef.defName : "NULL"));

                if (interactionDef != null)
                {
                    // SLog.Message(string.Format("[SocialInteractions] Checking if interaction should be handled: {0}", interactionDef.defName));

                    if ((interactionDef == InteractionDefOf.Chitchat && SocialInteractions.Settings.enableChitchat) ||
                        (interactionDef == InteractionDefOf.RomanceAttempt && SocialInteractions.Settings.enableRomanceAttempt) ||
                        (interactionDef == InteractionDefOf.DeepTalk && SocialInteractions.Settings.enableDeepTalk) ||
                        (interactionDef == InteractionDefOf.Insult && SocialInteractions.Settings.enableInsult) ||
                        (interactionDef == InteractionDefOf.MarriageProposal && SocialInteractions.Settings.enableMarriageProposal) ||
                        (interactionDef == InteractionDefOf.Reassure && SocialInteractions.Settings.enableReassure) ||
                        (interactionDef == InteractionDefOf.DisturbingChat && SocialInteractions.Settings.enableDisturbingChat) ||
                        (interactionDef.defName == "KindWords" && SocialInteractions.Settings.enableKindWordsInteractions) ||
                        (interactionDef == SI_InteractionDefOf.ChildAnnoying && SocialInteractions.Settings.enableChildrenMisbehavior) ||
                        (interactionDef.defName == "Flirt" && SocialInteractions.Settings.enableFlirt) ||
                        (interactionDef.defName == "VRE_FlirtingAttempt" && SocialInteractions.Settings.enableFlirt) ||
                        (interactionDef.defName == "Slight" && SocialInteractions.Settings.enableSlight) ||
                        (interactionDef.defName == "IncestuousFlirt" && SocialInteractions.Settings.enableIncestuousFlirt) ||
                        (interactionDef.defName == "Rapport" && SocialInteractions.Settings.enableRapport) ||
                        (interactionDef == InteractionDefOf.RecruitAttempt && SocialInteractions.Settings.enableRecruitAttempt) ||
                        (interactionDef.defName == "ReduceResistance" && SocialInteractions.Settings.enableReduceResistance) ||
                        (interactionDef == InteractionDefOf.ReduceWill && SocialInteractions.Settings.enableReduceWill) ||
                        ((interactionDef.defName == "EnslaveAttempt" || (InteractionDefOf.EnslaveAttempt != null && interactionDef == InteractionDefOf.EnslaveAttempt)) && SocialInteractions.Settings.enableEnslaveAttempt))
                    {
                        // SLog.Message(string.Format("[SocialInteractions] Interaction {0} matches criteria, checking if LLM interaction is enabled.", interactionDef.defName));

                        // Create a PlayLogEntry_Interaction to get the social log message
                        PlayLogEntry_Interaction entry = new PlayLogEntry_Interaction(interactionDef, initiator, recipient, extraSentencePacks);
                        string subject = SocialInteractions.RemoveRichTextTags(entry.ToGameStringFromPOV(initiator));

                        // Check if LLM interaction is enabled for this interaction type
                        bool isLlmEnabled = SocialInteractions.IsLlmInteractionEnabled(interactionDef);

                        if (isLlmEnabled)
                        {
                            // For LLM-enabled interactions, check if we can generate a prompt
                            string prompt = SocialInteractions.GenerateDeepTalkPrompt(initiator, recipient, interactionDef, subject);

                            // Check if either pawn is on a date OR doing a specialized activity.
                            bool eitherOnDate = DatingManager.IsOnDate(initiator) || (recipient != null && DatingManager.IsOnDate(recipient));
                            
                            bool eitherDoingSpecializedJob = (initiator.CurJobDef != null && (initiator.CurJobDef.defName == "PesterPrisoner" || initiator.CurJobDef.defName == "PesterPrisonerPartner" || initiator.CurJobDef.defName == "AbusiveThreesome" || initiator.CurJobDef.defName == "AbusiveThreesomeParticipant" || initiator.CurJobDef.defName == "SocialRelaxDate" || initiator.CurJobDef.defName == "DateLovin")) ||
                                                             (recipient != null && recipient.CurJobDef != null && (recipient.CurJobDef.defName == "PesterPrisoner" || recipient.CurJobDef.defName == "PesterPrisonerPartner" || recipient.CurJobDef.defName == "AbusiveThreesome" || recipient.CurJobDef.defName == "AbusiveThreesomeParticipant" || recipient.CurJobDef.defName == "SocialRelaxDate" || recipient.CurJobDef.defName == "DateLovin"));

                            if (eitherOnDate || eitherDoingSpecializedJob)
                            {
                                // Show bubble only, no jobs
                                string formattedSubject = SpeechBubbleManager.FormatSpeakerName(initiator, subject);
                                SpeechBubbleManager.ShowDefaultBubble(initiator, formattedSubject);
                                // Handle the interaction without stopping
                                SocialInteractions.HandleNonStoppingInteraction(initiator, recipient, interactionDef, subject, true);
                                return;
                            }

                            // Check if LLM is busy and if we should prevent spam
                            if (SocialInteractions.Settings.preventSpam && SpeechBubbleManager.IsLlmCurrentlyBusy())
                            {
                                // SLog.Message(string.Format("[SocialInteractions] Interaction {0} - LLM is busy and preventSpam is true, showing default bubble without creating jobs.", interactionDef.defName));
                                // Show default bubble when LLM is busy and we're preventing spam
                                if (!string.IsNullOrEmpty(subject))
                                {
                                    SpeechBubbleManager.ShowDefaultBubble(initiator, subject);
                                }
                                return;
                            }

                            // Only create jobs if we can generate a prompt (i.e., an actual LLM request will be sent)
                            if (!string.IsNullOrEmpty(prompt))
                            {
                                SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Interacted_Patch. Interaction {0} has a valid prompt, creating jobs.", interactionDef.defName));

                                // For LLM-enabled interactions, format the text with rich text formatting
                                string formattedSubject = SpeechBubbleManager.FormatSpeakerName(initiator, subject);
                                SpeechBubbleManager.ShowDefaultBubble(initiator, formattedSubject);

                                Job_HaveDeepTalk initiatorJob = new Job_HaveDeepTalk(DefDatabase<JobDef>.GetNamed("HaveDeepTalk"), recipient);
                                initiatorJob.interactionDef = interactionDef;
                                initiatorJob.subject = subject;
                                initiator.jobs.TryTakeOrderedJob(initiatorJob, JobTag.Misc);

                                Job recipientJob = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("BeTalkedTo"), initiator);
                                recipient.jobs.TryTakeOrderedJob(recipientJob, JobTag.Misc);

                                // SLog.Message("[SocialInteractions] InteractionWorker_Interacted_Patch. Jobs created successfully.");
                            }
                            else
                            {
                                SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Interacted_Patch. Interaction {0} does not have a valid prompt, showing default bubble without creating jobs.", interactionDef.defName));
                                // For non-LLM interactions, show the default text
                                SpeechBubbleManager.ShowDefaultBubble(initiator, subject);
                            }
                        }
                        else
                        {
                            // SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Interacted_Patch. Interaction {0} is not LLM-enabled, showing default bubble without creating jobs.", interactionDef.defName));
                            // For non-LLM interactions, show the default text
                            SpeechBubbleManager.ShowDefaultBubble(initiator, subject);
                        }
                    }
                    else
                    {
                        // SLog.Message(string.Format("[SocialInteractions] InteractionWorker_Interacted_Patch. Interaction {0} does not match criteria.", interactionDef.defName));
                    }
                }
                else
                {
                    SLog.Warning("[SocialInteractions] InteractionWorker_Interacted_Patch. InteractionDef is null, skipping job creation.");
                }
            }
            else
            {
                // SLog.Message("[SocialInteractions] InteractionWorker_Interacted_Patch. pawnsStopOnInteraction is false, using default behavior.");
            }
        }

        private static bool IsInsultInteraction(InteractionDef interactionDef)
        {
            // Check if this is any type of insult interaction
            return interactionDef == InteractionDefOf.Insult ||
                   interactionDef == SI_InteractionDefOf.EnhancedInsult ||
                   interactionDef.defName.Contains("Insult"); // Generic check for insult-related interactions
        }

        private static void TryStartCryingToParent(Pawn child, Pawn insulter, InteractionDef interactionDef)
        {
            // Give the child a chance to go cry to their parent (for now, let's say 70% chance)
            if (Rand.Value < 0.7f) // 70% chance for now, can be configurable
            {
                // Find the child's parent or most liked pawn
                Pawn parent = FindParentOrMostLikedPawn(child);

                if (parent != null && parent != insulter) // Don't go to the insulter
                {
                    // Create the job for the child to go cry to the parent
                    Job cryJob = JobMaker.MakeJob(SI_JobDefOf.ChildGoCryToParent, parent);
                    cryJob.count = 0; // 0 = insult-related distress
                    child.jobs.TryTakeOrderedJob(cryJob);

                    SLog.Message(string.Format("[SocialInteractions] TryStartCryingToParent: Child {0} is going to cry to parent {1} after being insulted by {2}",
                        child.LabelShort, parent.LabelShort, insulter.LabelShort));
                }
                else if (parent == null)
                {
                    SLog.Message(string.Format("[SocialInteractions] TryStartCryingToParent: Child {0} has no parent to cry to after being insulted", child.LabelShort));
                }
                else
                {
                    SLog.Message(string.Format("[SocialInteractions] TryStartCryingToParent: Child {0} cannot cry to insulter {1}", child.LabelShort, parent.LabelShort));
                }
            }
        }

        private static Pawn FindParentOrMostLikedPawn(Pawn child)
        {
            if (child.relations == null)
            {
                return null;
            }

            // First, look for parents
            foreach (Pawn potentialParent in child.Map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (potentialParent != null && !potentialParent.Dead && potentialParent.Spawned)
                {
                    if (child.relations.DirectRelationExists(PawnRelationDefOf.Parent, potentialParent))
                    {
                        return potentialParent;
                    }
                }
            }

            // If no parents found, look for the most liked pawn (highest opinion of the child)
            Pawn mostLiked = null;
            int highestOpinion = int.MinValue;

            foreach (Pawn potentialPawn in child.Map.mapPawns.FreeColonistsAndPrisoners)
            {
                if (potentialPawn != null && !potentialPawn.Dead && potentialPawn.Spawned && potentialPawn != child)
                {
                    int opinion = (child.relations != null) ? child.relations.OpinionOf(potentialPawn) : 0;
                    if (opinion > highestOpinion)
                    {
                        highestOpinion = opinion;
                        mostLiked = potentialPawn;
                    }
                }
            }

            return mostLiked;
        }
    }
}