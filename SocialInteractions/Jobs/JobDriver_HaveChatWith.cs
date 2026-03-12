using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;
using UnityEngine;

namespace SocialInteractions
{
    public class JobDriver_HaveChatWith : JobDriver
    {
        private TargetIndex TargetInd = TargetIndex.A;
        private int chatDuration = 1800; // 30 seconds of chatting
        private int followCheckInterval = 60; // Check every second to follow target

        private bool isNegotiationMode = false; // Track if we're in negotiation mode

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetInd), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetInd);
            
            // Go to the target
            yield return Toils_Goto.GotoThing(TargetInd, PathEndMode.Touch);
            
            // Chat toil - behavior differs based on settings
            Toil chatToil = ToilMaker.MakeToil("ChatToil");
            chatToil.initAction = delegate
            {
                Pawn target = (Pawn)job.GetTarget(TargetInd).Thing;
                if (target != null)
                {
                    // Check if negotiation mode is enabled and target is humanlike (animals can't negotiate)
                    if (SocialInteractions.Settings.enableManualChat && 
                        SocialInteractions.Settings.llmInteractionsEnabled && 
                        SocialInteractions.Settings.enableInteractiveNegotiation &&
                        target.RaceProps.Humanlike)
                    {
                        // Open negotation dialog
                        isNegotiationMode = true;
                        
                        // We need to execute this on the main thread
                        Dialog_PawnNegotiation dialog = new Dialog_PawnNegotiation(pawn, target);
                        Find.WindowStack.Add(dialog);
                    }
                    else if (SocialInteractions.Settings.enableManualChat && SocialInteractions.Settings.llmInteractionsEnabled)
                    {
                        // Determine subject based on context
                        string subject = "Having a casual chat";
                        
                        // Detect context
                        if (RaidNegotiationContext.HasActiveRaid(pawn))
                        {
                            subject = "Negotiating with raiders for a peaceful resolution";
                        }
                        else if (target.MentalStateDef == MentalStateDefOf.SocialFighting)
                        {
                            subject = "Attempting to break up the social fight through negotiation";
                        }
                        else if (target.InMentalState)
                        {
                            subject = string.Format("Attempting to snap {0} out of their {1} through negotiation", target.LabelShort, target.MentalStateDef.label);
                        }
                        else if (target.TraderKind != null)
                        {
                            subject = "Negotiating a trade deal for better prices";
                        }
                        else
                        {
                            Lord lord = target.GetLord();
                            if (lord != null)
                            {
                                if (lord.LordJob is LordJob_TradeWithColony)
                                {
                                    subject = "Negotiating a trade deal for better prices";
                                }
                                else if (lord.LordJob != null)
                                {
                                    string jobName = lord.LordJob.GetType().Name;
                                    if (jobName.Contains("Visit") || jobName.Contains("Refugee") || jobName.Contains("Guest") || jobName.Contains("Traveler"))
                                    {
                                        subject = "Negotiating with a visitor for improved goodwill between the two factions";
                                    }
                                }
                            }
                        }

                        // Fallback for animals/non-humans or when interactive negotiation is disabled: Use LLM bubbles without the dialog
                        SocialInteractions.HandleNonStoppingInteraction(pawn, target, SI_InteractionDefOf.ManualChat, subject, true, true);
                        isNegotiationMode = false;
                    }
                    else
                    {
                        // Fallback to default interaction (No LLM)
                        string text = string.Format("{0} chats with {1}.", pawn.LabelShort, target.LabelShort);
                        SpeechBubbleManager.ShowDefaultBubble(pawn, text);
                        isNegotiationMode = false;
                    }
                }
            };
            chatToil.tickAction = delegate
            {
                // Check if we should end early (dialog was closed)
                if (isNegotiationMode)
                {
                    // Check if dialog is still open
                    bool dialogOpen = Find.WindowStack.IsOpen<Dialog_PawnNegotiation>();
                    if (!dialogOpen)
                    {
                        // Dialog was closed, end the job
                        ReadyForNextToil();
                        return;
                    }
                }
                
                // Follow the target if they move
                // Previously was checked only for LLM requests, but we want it for manual chat too
                Pawn target = (Pawn)job.GetTarget(TargetInd).Thing;
                if (target != null && !target.Dead && target.Spawned)
                {
                    // Check if we should update the path to follow the target
                    if (pawn.IsHashIntervalTick(followCheckInterval))
                    {
                        // If the target has moved significantly, update our path to follow them
                        float distance = (pawn.Position - target.Position).LengthHorizontal;
                        if (distance > 2f)
                        {
                            // Update path to follow target
                            pawn.pather.StartPath(target, PathEndMode.Touch);
                        }
                    }
                }
            };
            chatToil.defaultCompleteMode = ToilCompleteMode.Delay;
            chatToil.defaultDuration = chatDuration; // 30 seconds
            yield return chatToil;
            
            // End the job with outcome notification
            Toil finishToil = ToilMaker.MakeToil("FinishToil");
            finishToil.initAction = delegate
            {
                if (!isNegotiationMode)
                {
                   // Skill check based on Social level
                   // Success: 0% at level 0, rising to 20% at level 20
                   // Failure: 50% at level 0, falling to 0% at level 20
                   // Neutral: The rest
                   
                   Pawn target = (Pawn)job.GetTarget(TargetInd).Thing;
                   if (target != null)
                   {
                       // Roll for outcome using centralized logic
                       NegotiationOutcome outcome = NegotiationManager.RollSkillBasedOutcome(pawn);
                       
                       // Detect context
                       Lord raidContext = RaidNegotiationContext.GetActiveRaid(pawn);
                       bool isTradeContext = target.TraderKind != null;
                       bool isVisitorContext = false;
                       
                       Lord lord = target.GetLord();
                       if (!isTradeContext && lord != null)
                       {
                           if (lord.LordJob is LordJob_TradeWithColony)
                           {
                               isTradeContext = true;
                           }
                           else if (lord.LordJob != null)
                           {
                               string jobName = lord.LordJob.GetType().Name;
                               if (jobName.Contains("Visit") || jobName.Contains("Refugee") || jobName.Contains("Guest") || jobName.Contains("Traveler"))
                               {
                                   isVisitorContext = true;
                               }
                           }
                       }

                       // Apply outcome using centralized logic
                       NegotiationManager.ApplyUniversalOutcome(pawn, target, outcome, raidContext, isTradeContext, isVisitorContext);
                       
                       // Skill level for messages
                       int socialLevel = pawn.skills != null ? pawn.skills.GetSkill(SkillDefOf.Social).Level : 0;
                       
                       // Feedback messages
                       if (outcome == NegotiationOutcome.CriticalSuccess)
                       {
                           Messages.Message("Negotiation CRITICAL Success: " + pawn.LabelShort + " masterfully handled " + target.LabelShort + " (Social Skill " + socialLevel + ")", pawn, MessageTypeDefOf.PositiveEvent);
                       }
                       else if (outcome == NegotiationOutcome.Positive)
                       {
                           Messages.Message("Negotiation Success: " + pawn.LabelShort + " convinced " + target.LabelShort + " (Social Skill " + socialLevel + ")", pawn, MessageTypeDefOf.PositiveEvent);
                       }
                       else if (outcome == NegotiationOutcome.Negative)
                       {
                           Messages.Message("Negotiation Failed: " + pawn.LabelShort + " failed to convince " + target.LabelShort + " (Social Skill " + socialLevel + ")", pawn, MessageTypeDefOf.NegativeEvent);
                       }
                       else
                       {
                           Messages.Message("Negotiation Neutral: " + pawn.LabelShort + " and " + target.LabelShort + " chatted without reaching a conclusion.", pawn, MessageTypeDefOf.NeutralEvent);
                       }
                   }
                }
            };
            yield return finishToil;
        }
    }
}