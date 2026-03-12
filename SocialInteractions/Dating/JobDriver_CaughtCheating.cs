using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using UnityEngine; // Added for Texture2D
using System; // Added for Exception

namespace SocialInteractions
{
    [StaticConstructorOnStartup]
    public class JobDriver_CaughtCheating : JobDriver
    {
        // Static field for the speech bubble icon
        private static readonly Texture2D moteIcon = ContentFinder<Texture2D>.Get("Things/Mote/SpeechSymbols/Speech");

        private Pawn Cheater
        {
            get
            {
                return (Pawn)job.targetA.Thing;
            }
        }

        private new int startTick = -1;
        private int conversationId = -1; // Store the conversation ID for this interaction
        private int ticksLeft; // For bounce animation, initialize to 0 by default

        // Use the settings value as minimum duration
        private int MinWaitDuration { get { return SocialInteractions.Settings.cheatingConfrontationTicks; } }

        public override Vector3 ForcedBodyOffset
        {
            get
            {
                if (pawn == null || ticksLeft <= 0)
                {
                    return Vector3.zero;
                }

                int totalTicks = SocialInteractions.Settings.dateLovinTicks;
                
                // Make sure we don't divide by zero
                if (totalTicks <= 0)
                {
                    return Vector3.zero;
                }

                // Calculate progress (0.0 to 1.0 as time passes)
                float progress = 1.0f - ((float)ticksLeft / totalTicks);

                // Calculate animation speed based on progress
                float animationSpeed = 1.0f;
                if (progress <= 0.90f)
                {
                    // Linear interpolation from 1.0 to 2.0
                    animationSpeed = 1.0f + (progress / 0.90f) * 1.0f;
                }
                else
                {
                    // Drop to 20% speed for the remaining time
                    animationSpeed = 0.3f;
                }
                
                // Calculate the base time parameter
                float baseTime = progress * 8.0f * (totalTicks / 60.0f);
                
                // Apply the animation speed to effectively change the frequency
                // To double the speed, we double the frequency (multiply time by speed)
                float adjustedTime = baseTime * animationSpeed;
                
                float num = Mathf.Sin(adjustedTime);
                float z = Mathf.Max(Mathf.Pow((num + 1f) * 0.5f, 2f) * 0.2f - 0.06f, 0f);
                return new Vector3(0f, 0f, z);
            }
        }

        private float EaseInOutQuad(float v)
        {
            if (!((double)v < 0.5))
            {
                return 1f - Mathf.Pow(-2f * v + 2f, 4f) / 2f;
            }
            return 8f * v * v * v * v;
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            SLog.Message("[SocialInteractions] JobDriver_CaughtCheating: TryMakePreToilReservations called.");
            return true;
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            startTick = Find.TickManager.TicksGame;
            SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Notify_Starting called for pawn {0} to confront cheater {1}. Start tick: {2}", 
                pawn != null ? pawn.LabelShort : "NULL", 
                Cheater != null ? Cheater.LabelShort : "NULL",
                startTick));
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            SLog.Message("[SocialInteractions] JobDriver_CaughtCheating: MakeNewToils called.");
            
            // Add a finish action to ensure the conversation is ended regardless of how the job ends
            this.AddFinishAction((condition) => {
                if (this.conversationId != -1) {
                    SpeechBubbleManager.EndConversation(this.conversationId);
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Ended conversation ID: {0} via finish action.", this.conversationId));
                    this.conversationId = -1;
                }
            });

            // Add null checks
            if (pawn == null || job == null || job.targetA.Thing == null)
            {
                SLog.Warning("[SocialInteractions] JobDriver_CaughtCheating: Pawn, job, or job.targetA.Thing is null, ending job.");
                yield break;
            }

            Pawn cheater = (Pawn)job.targetA.Thing;

            // Make sure we're still near the cheater
            if (!pawn.Position.InHorDistOf(cheater.Position, 5f))
            {
                SLog.Message("[SocialInteractions] JobDriver_CaughtCheating: Pawn is too far from cheater, ending job.");
                yield break;
            }

            SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Pawn {0} is near cheater {1}, proceeding with job.", 
                pawn.LabelShort, cheater.LabelShort));

            // Retrieve the date partner for the cheater
            Pawn partner = null;
            if (SocialInteractions.CheaterPartners.ContainsKey(cheater.ThingID))
            {
                partner = SocialInteractions.CheaterPartners[cheater.ThingID];
                SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Retrieved partner: {0}", partner.LabelShort));
            }
            else
            {
                SLog.Warning(string.Format("[SocialInteractions] JobDriver_CaughtCheating: No partner found for cheater {0}", cheater.LabelShort));
            }

            // Check if the angry spouse has free love or polygamy precepts and wants to join in a 3p action
            if (partner != null && ShouldInitiateThreewayAction(pawn, cheater, partner))
            {
                // Initiate 3p action instead of confrontation
                SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: {0} has free love/polygamy precepts, initiating 3p action.", pawn.LabelShort));
                
                // Instead of ending the date, modify it to include the spouse
                Date date = DatingManager.GetDateWith(cheater);
                if (date != null)
                {
                    SLog.Message("[SocialInteractions] JobDriver_CaughtCheating: Modifying date for 3p action.");
                    // Add the spouse to the date as a third participant
                    ConvertDateToThreeway(date, pawn, cheater, partner);
                }
                
                // Add naked hediff to the spouse who caught them
                AddNakedHediff(pawn);
                
                // Trigger the 3p LLM interaction
                this.conversationId = SocialInteractions.HandleThreewayLovinInteraction(pawn, cheater, partner);
                
                // Custom wait toil to wait for the 3p conversation to finish
                Toil threewayWaitToil = new Toil();
                threewayWaitToil.initAction = () => 
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: 3p wait toil initAction called for pawn {0}.", pawn.LabelShort));

                    // Start the bounce animation timer
                    ticksLeft = SocialInteractions.Settings.dateLovinTicks;

                    // Reset the lovin timers for the other two pawns to sync up
                    var cheaterDriver = cheater.jobs.curDriver as JobDriver_DateLovin;
                    if (cheaterDriver != null)
                    {
                        cheaterDriver.ticksLeft = SocialInteractions.Settings.dateLovinTicks;
                        SLog.Message(string.Format("[SocialInteractions] Reset lovin timer for cheater {0}.", cheater.LabelShort));
                    }
                    var partnerDriver = partner.jobs.curDriver as JobDriver_DateLovin;
                    if (partnerDriver != null)
                    {
                        partnerDriver.ticksLeft = SocialInteractions.Settings.dateLovinTicks;
                        SLog.Message(string.Format("[SocialInteractions] Reset lovin timer for partner {0}.", partner.LabelShort));
                    }

                    // Create the speech bubble mote when the pawn starts waiting
                    try
                    {
                        if (moteIcon != null)
                        {
                            MoteMaker.MakeSpeechBubble(pawn, moteIcon);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Exception while creating speech bubble for pawn {0}: {1}", pawn.LabelShort, ex.Message));
                    }
                };
                
                // Play the appropriate speech sound based on gender using Toil's method
                threewayWaitToil.PlaySustainerOrSound(() => (pawn.gender != Gender.Female) ? SoundDefOf.Speech_Leader_Male : SoundDefOf.Speech_Leader_Female, pawn.story.VoicePitchFactor);
                
                threewayWaitToil.tickAction = () => 
                {
                    // Decrement the bounce animation timer
                    if (ticksLeft > 0) ticksLeft--;

                    bool minDurationElapsed = ticksLeft <= 0;
                    
                    // Check if the specific conversation for this 3p interaction is still active or has pending speech bubbles
                    bool isConversationFinished = true;
                    try
                    {
                        // Check if this conversation is still active or has pending speech bubbles
                        if (this.conversationId != -1)
                        {
                            isConversationFinished = !SpeechBubbleManager.IsConversationActive(this.conversationId) && !SpeechBubbleManager.HasPendingSpeechBubbles(this.conversationId);
                        }
                    }
                    catch (Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Exception while checking 3p conversation status: {0}", ex.Message));
                    }
                    
                    // If the animation timer has run out, the action is finished.
                    if (ticksLeft <= 0)
                    {
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: 3p wait duration elapsed for pawn {0}.", pawn.LabelShort));
                        
                        // Add special thoughts for all involved pawns
                        ThoughtDef threewayLovinThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("ThreewayLovin");
                        if (threewayLovinThought != null)
                        {
                            pawn.needs.mood.thoughts.memories.TryGainMemory(threewayLovinThought, cheater);
                            cheater.needs.mood.thoughts.memories.TryGainMemory(threewayLovinThought, pawn);
                            partner.needs.mood.thoughts.memories.TryGainMemory(threewayLovinThought, pawn);
                        }
                        
                        // Remove the partner from the CheaterPartners dictionary
                        if (SocialInteractions.CheaterPartners.ContainsKey(cheater.ThingID))
                        {
                            SocialInteractions.CheaterPartners.Remove(cheater.ThingID);
                        }
                        
                        // Remove the SI_Naked hediff from the spouse (third participant)
                        RemoveNakedHediff(pawn);
                        
                        // End the conversation before ending the job
                        if (this.conversationId != -1)
                        {
                            SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Ending 3p conversation ID: {0}", this.conversationId));
                            SpeechBubbleManager.EndConversation(this.conversationId);
                            this.conversationId = -1;
                        }
                        
                        // End the job
                        pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                    }
                };
                threewayWaitToil.defaultCompleteMode = ToilCompleteMode.Never; // We'll complete it manually
                yield return threewayWaitToil;
                
                SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: MakeNewToils finished for pawn {0}.", pawn.LabelShort));
            }
            else
            {
                // Hold the cheater in place for the confrontation
                Pawn_Tick_Patch.HoldPawnInPlace(cheater, cheater.Position);

                // Normal confrontation path - create a BeTalkedTo job for the cheater to hold them in place during the conversation
                Job beTalkedToJob = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("BeTalkedTo"), pawn);
                cheater.jobs.TryTakeOrderedJob(beTalkedToJob, JobTag.Misc);
                
                // Trigger the LLM interaction with the partner and store the conversation ID
                conversationId = SocialInteractions.HandleCaughtCheatingInteraction(pawn, cheater, partner);
                
                // Make the partner flee immediately upon the spouse's arrival
                if (partner != null)
                {
                    InteractionWorker_CaughtCheating interactionWorker = new InteractionWorker_CaughtCheating();
                    interactionWorker.MakePartnerFleeImmediately(partner, pawn); // pawn is the angry spouse
                }
                
                // Custom wait toil to wait for the conversation to finish
                Toil waitToil = new Toil();
                waitToil.initAction = () => 
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Wait toil initAction called for pawn {0}. Start tick: {1}", 
                        pawn.LabelShort, startTick));

                    // Create the speech bubble mote when the pawn starts waiting (i.e., confronting)
                    try
                    {
                        if (moteIcon != null)
                        {
                            MoteMaker.MakeSpeechBubble(pawn, moteIcon);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Exception while creating speech bubble for pawn {0}: {1}", pawn.LabelShort, ex.Message));
                    }
                    
                    // Create the exclamation mote when the pawn catches their partner cheating
                    try
                    {
                        MoteMaker.MakeColonistActionOverlay(pawn, ThingDefOf.Mote_ColonistFleeing);
                    }
                    catch (System.Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Exception while creating exclamation mote for pawn {0}: {1}", pawn.LabelShort, ex.Message));
                    }
                };
            
            // Play the appropriate speech sound based on gender using Toil's method
                waitToil.PlaySustainerOrSound(() => (pawn.gender != Gender.Female) ? SoundDefOf.Speech_Leader_Male : SoundDefOf.Speech_Leader_Female, pawn.story.VoicePitchFactor);
                
                waitToil.tickAction = () => 
                {
                    // Check if the minimum wait duration has elapsed
                    int elapsedTicks = Find.TickManager.TicksGame - startTick;
                    bool minDurationElapsed = elapsedTicks >= MinWaitDuration;
                    
                    // Check if the specific conversation for this cheating interaction is still active or has pending speech bubbles
                    bool isConversationFinished = true;
                    try
                    {
                        // Check if this conversation is still active or has pending speech bubbles
                        if (conversationId != -1)
                        {
                            isConversationFinished = !SpeechBubbleManager.IsConversationActive(conversationId) && !SpeechBubbleManager.HasPendingSpeechBubbles(conversationId);
                        }
                    }
                    catch (Exception ex)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Exception while checking conversation status: {0}", ex.Message));
                    }
                    
                    // If minimum duration has elapsed and the conversation is finished, end the confrontation
                    if (minDurationElapsed && isConversationFinished)
                    {
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Wait duration elapsed for pawn {0}.", pawn.LabelShort));
                        
                        // Remove the partner from the CheaterPartners dictionary
                        Pawn cheaterPawn = (Pawn)job.targetA.Thing;
                        if (cheaterPawn != null && SocialInteractions.CheaterPartners.ContainsKey(cheaterPawn.ThingID))
                        {
                            SocialInteractions.CheaterPartners.Remove(cheaterPawn.ThingID);
                        }
                        
                        // End the date when the angry spouse arrives and the waiting period is over
                        Date date = DatingManager.GetDateWith(cheaterPawn);
                        if (date != null)
                        {
                            SLog.Message("[SocialInteractions] JobDriver_CaughtCheating: Ending date as angry spouse has arrived.");
                            DatingManager.EndDate(date);
                        }
                        else
                        {
                            // If we can't get the date from the cheater, try to get it from the partner
                            if (partner != null)
                            {
                                date = DatingManager.GetDateWith(partner);
                                if (date != null)
                                {
                                    SLog.Message("[SocialInteractions] JobDriver_CaughtCheating: Ending date as angry spouse has arrived (from partner).");
                                    DatingManager.EndDate(date);
                                }
                            }
                        }
                        
                        // Remove the OnDate hediff from the partner if they still have it
                        if (partner != null)
                        {
                            HediffDef onDateDef = HediffDef.Named("OnDate");
                            if (onDateDef != null)
                            {
                                try
                                {
                                    Hediff onDateHediff = partner.health.hediffSet.GetFirstHediffOfDef(onDateDef);
                                    if (onDateHediff != null)
                                    {
                                        SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Removing OnDate hediff from partner {0}.", partner.LabelShort));
                                        partner.health.RemoveHediff(onDateHediff);
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    SLog.Warning(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Exception removing OnDate hediff from partner {0}: {1}", partner.LabelShort, ex.Message));
                                }
                            }
                        }
                        
                        // Trigger fight logic
                        InteractionWorker_CaughtCheating interactionWorker = new InteractionWorker_CaughtCheating();
                        interactionWorker.TriggerFightLogic(pawn, cheaterPawn, partner); // Pass the partner we retrieved earlier
                        
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Fight logic triggered for pawn {0}.", pawn.LabelShort));
                        
                        // Check if a social fight was successfully started
                        if (pawn.mindState != null && 
                            pawn.mindState.mentalStateHandler != null &&
                            pawn.mindState.mentalStateHandler.InMentalState && 
                            pawn.mindState.mentalStateHandler.CurState.def == MentalStateDefOf.SocialFighting)
                        {
                            // A social fight was successfully started
                            SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Pawn {0} is in social fight, ending job to let mental state take over.", pawn.LabelShort));
                            // End the conversation before ending the job
                            if (this.conversationId != -1)
                            {
                                SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Ending conversation ID: {0}", this.conversationId));
                                SpeechBubbleManager.EndConversation(this.conversationId);
                                this.conversationId = -1;
                            }
                            // End the job and let the mental state handle the fighting
                            pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                        }
                        else
                        {
                            // If no fight was started, end the job
                            SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: No fight started for pawn {0}, ending job.", pawn.LabelShort));
                            // End the conversation before ending the job
                            if (this.conversationId != -1)
                            {
                                SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: Ending conversation ID: {0}", this.conversationId));
                                SpeechBubbleManager.EndConversation(this.conversationId);
                                this.conversationId = -1;
                            }
                            pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                        }
                    }
                };
                waitToil.defaultCompleteMode = ToilCompleteMode.Never; // We'll complete it manually or let mental state take over
                yield return waitToil;
                
                SLog.Message(string.Format("[SocialInteractions] JobDriver_CaughtCheating: MakeNewToils finished for pawn {0}.", pawn.LabelShort));
            }
        }
        
        private bool ShouldInitiateThreewayAction(Pawn spouse, Pawn cheater, Pawn partner)
        {
            SLog.Message(string.Format("[SocialInteractions] ShouldInitiateThreewayAction: Checking for {0}", spouse.LabelShort));
            
            // Check if Ideology is active
            if (!ModsConfig.IdeologyActive)
            {
                SLog.Message("[SocialInteractions] ShouldInitiateThreewayAction: Ideology not active");
                return false;
            }
            
            // Check if the spouse has an ideology
            if (spouse.Ideo == null)
            {
                SLog.Message(string.Format("[SocialInteractions] ShouldInitiateThreewayAction: {0} has no ideology", spouse.LabelShort));
                return false;
            }
            
            // Check for free love or polygamy precepts
            bool hasFreeLove = spouse.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("Lovin_FreeApproved"));
            bool hasPolygamy = spouse.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("SpouseCount_Male_Unlimited")) ||
                              spouse.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("SpouseCount_Female_Unlimited")) ||
                              spouse.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("SpouseCount_Male_MaxThree")) ||
                              spouse.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("SpouseCount_Female_MaxThree")) ||
                              spouse.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("SpouseCount_Male_MaxFour")) ||
                              spouse.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("SpouseCount_Female_MaxFour"));
            
            SLog.Message(string.Format("[SocialInteractions] ShouldInitiateThreewayAction: {0} has free love: {1}, polygamy: {2}", 
                spouse.LabelShort, hasFreeLove, hasPolygamy));
            
            bool hasFreeLoveOrPolygamy = hasFreeLove || hasPolygamy;
            
            if (!hasFreeLoveOrPolygamy)
            {
                SLog.Message(string.Format("[SocialInteractions] ShouldInitiateThreewayAction: {0} does not have free love or polygamy precepts", spouse.LabelShort));
                return false;
            }
            
            // 70% chance to initiate 3p action if they have the precepts
            bool shouldInitiate = Rand.Value < 0.99f;
            SLog.Message(string.Format("[SocialInteractions] ShouldInitiateThreewayAction: {0} roll: {1}, should initiate: {2}", 
                spouse.LabelShort, Rand.Value, shouldInitiate));
            
            return shouldInitiate;
        }
        
        private void ConvertDateToThreeway(Date date, Pawn spouse, Pawn cheater, Pawn partner)
        {
            // Mark the date as a 3p action
            date.IsThreewayAction = true;
            
            SLog.Message(string.Format("[SocialInteractions] ConvertDateToThreeway: Converting date between {0} and {1} to include {2}",
                cheater.LabelShort, partner.LabelShort, spouse.LabelShort));
                
            // For a 3p action, we don't need to start a new DateLovin job for the spouse.
            // We just need to make the spouse bounce around near the cheating couple.
            // The spouse already has the SI_Naked hediff and a "bounce next to" job from the caller.
            // The caller will also handle the LLM interaction and job waiting.
        }
        
        private void AddNakedHediff(Pawn pawn)
        {
            // Add the SI_Naked hediff to the pawn
            HediffDef nakedDef = HediffDef.Named("SI_Naked");
            if (nakedDef != null && pawn.health != null)
            {
                try
                {
                    Hediff nakedHediff = pawn.health.hediffSet.GetFirstHediffOfDef(nakedDef);
                    if (nakedHediff == null)
                    {
                        nakedHediff = HediffMaker.MakeHediff(nakedDef, pawn);
                        pawn.health.AddHediff(nakedHediff);
                        SLog.Message(string.Format("[SocialInteractions] Added SI_Naked hediff to {0}", pawn.LabelShort));
                        
                        // Record when the hediff was added
                        if (pawn.Map != null)
                        {
                            Dating_MapComponent mapComponent = pawn.Map.GetComponent<Dating_MapComponent>();
                            if (mapComponent != null)
                            {
                                mapComponent.RecordSINakedHediffAdded(pawn);
                            }
                        }
                    }
                    else
                    {
                        SLog.Message(string.Format("[SocialInteractions] {0} already has SI_Naked hediff", pawn.LabelShort));
                    }
                }
                catch (Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] Exception adding SI_Naked hediff to {0}: {1}", pawn.LabelShort, ex.Message));
                }
            }
        }
        
        private void MakePawnBounceNextTo(Pawn mover, Pawn target)
        {
            // Create a job for the mover to go to a position next to the target
            IntVec3 targetPosition = target.Position;
            
            // Find a valid position next to the target
            IntVec3 newPosition = targetPosition;
            if (target.Map != null)
            {
                // Try to find an adjacent cell
                for (int i = 0; i < 4; i++)
                {
                    IntVec3 adjCell = targetPosition + GenAdj.CardinalDirections[i];
                    if (adjCell.IsValid && adjCell.Walkable(target.Map))
                    {
                        newPosition = adjCell;
                        break;
                    }
                }
            }
            
            if (newPosition != targetPosition)
            {
                Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, newPosition);
                gotoJob.locomotionUrgency = LocomotionUrgency.Sprint;
                mover.jobs.TryTakeOrderedJob(gotoJob);
                SLog.Message(string.Format("[SocialInteractions] Making {0} bounce next to {1}", mover.LabelShort, target.LabelShort));
            }
            else
            {
                SLog.Message(string.Format("[SocialInteractions] {0} is already next to {1}, no need to bounce", mover.LabelShort, target.LabelShort));
            }
        }
        
        private void RemoveNakedHediff(Pawn pawn)
        {
            // Remove the SI_Naked hediff from the pawn
            HediffDef nakedDef = HediffDef.Named("SI_Naked");
            if (nakedDef != null && pawn.health != null && pawn.health.hediffSet != null)
            {
                try
                {
                    Hediff nakedHediff = pawn.health.hediffSet.GetFirstHediffOfDef(nakedDef);
                    if (nakedHediff != null)
                    {
                        pawn.health.RemoveHediff(nakedHediff);
                        SLog.Message(string.Format("[SocialInteractions] Removed SI_Naked hediff from {0}", pawn.LabelShort));
                    }
                }
                catch (Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] Exception removing SI_Naked hediff from {0}: {1}", pawn.LabelShort, ex.Message));
                }
            }
        }
    }
}