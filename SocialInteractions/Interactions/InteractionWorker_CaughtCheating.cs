using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SocialInteractions
{
    public class InteractionWorker_CaughtCheating : InteractionWorker
    {
        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            // Add null checks to prevent exceptions
            if (initiator == null || recipient == null)
            {
                SLog.Warning("[SocialInteractions] InteractionWorker_CaughtCheating: Initiator or recipient is null, skipping interaction.");
                base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
                return;
            }

            // Don't add initial thoughts here - let the branching logic in JobDriver_CaughtCheating handle adding appropriate thoughts
            // Get the partner for later use in JobDriver_CaughtCheating
            Pawn partner = DatingManager.GetPartnerOfDateWith(recipient);
            if (partner != null)
            {
                // Store the partner for use in JobDriver_CaughtCheating
                SocialInteractions.CheaterPartners[recipient.ThingID] = partner;
            }

            // Don't start the job immediately - let the initiator move to the recipient first
            // The Goto job is already created by Pawn_Tick_Patch
            // When the initiator arrives, the JobDriver_CaughtCheating will be started automatically

            // Call base method for any additional logic
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
        }

        public void TriggerFightLogic(Pawn initiator, Pawn recipient, Pawn partner)
        {
            // Add null checks to prevent exceptions
            if (initiator == null || recipient == null)
            {
                SLog.Warning("[SocialInteractions] TriggerFightLogic: Initiator or recipient is null, skipping fight logic.");
                return;
            }

            SLog.Message("[SocialInteractions] TriggerFightLogic: Starting fight logic evaluation.");
            
            // The partner always flees
            if (partner != null)
            {
                // Make the partner flee from the initiator
                TryMakePartnerFlee(partner, initiator);
            }
            
            // Add appropriate thoughts for the fight branch
            // Initiator (the one who caught cheating) gets the initial CaughtCheating thought
            ThoughtDef caughtCheatingThought = DefDatabase<ThoughtDef>.GetNamed("CaughtCheating");
            if (caughtCheatingThought != null)
            {
                initiator.needs.mood.thoughts.memories.TryGainMemory(caughtCheatingThought, recipient);
            }

            // Recipient (the cheater) gets the initial GotCaughtCheating thought
            ThoughtDef gotCaughtCheatingThought = DefDatabase<ThoughtDef>.GetNamed("GotCaughtCheating");
            if (gotCaughtCheatingThought != null)
            {
                recipient.needs.mood.thoughts.memories.TryGainMemory(gotCaughtCheatingThought, initiator);
            }

            // Partner (the one being cheated on) gets the initial WasCheatedOn thought
            if (partner != null)
            {
                ThoughtDef wasCheatedOnThought = DefDatabase<ThoughtDef>.GetNamed("WasCheatedOn");
                if (wasCheatedOnThought != null)
                {
                    partner.needs.mood.thoughts.memories.TryGainMemory(wasCheatedOnThought, recipient);
                }
            }
            
            // Determine the type of response based on various factors
            float responseRoll = Rand.Value;
            SLog.Message(string.Format("[SocialInteractions] TriggerFightLogic: Response roll: {0}.", responseRoll));
            
            // Calculate relationship strength between initiator and recipient
            float relationshipStrength = 0f;
            if (initiator.relations != null && recipient != null)
            {
                relationshipStrength = initiator.relations.OpinionOf(recipient) / 100f; // Normalize to -1 to 1 range
            }
            
            // Check for specific traits that might influence the response
            bool isKind = initiator.story.traits.HasTrait(TraitDefOf.Kind);
            bool isWimp = initiator.story.traits.HasTrait(TraitDefOf.Wimp);
            bool isBrawler = initiator.story.traits.HasTrait(TraitDefOf.Brawler);
            
            // Check for Ideology precepts that might influence the response
            bool hasFreeLoveOrPolygamy = HasFreeLoveOrPolygamyPrecept(initiator);
            
            // Modify the response based on traits and relationship
            float fightChance = 0.5f; // Base 50% chance
            if (isKind) fightChance -= 0.2f; // Kind pawns are less likely to fight
            if (isWimp) fightChance -= 0.3f; // Wimpy pawns are less likely to fight
            if (isBrawler) fightChance += 0.3f; // Brawlers are more likely to fight
            if (hasFreeLoveOrPolygamy) fightChance -= 0.4f; // Pawns with free love or polygamy are less likely to fight
            fightChance += relationshipStrength * 0.3f; // Stronger relationships reduce fight chance
            
            // Clamp the chance between 0.1 and 0.9
            fightChance = Mathf.Clamp(fightChance, 0.1f, 0.9f);
            
            SLog.Message(string.Format("[SocialInteractions] TriggerFightLogic: Adjusted fight chance: {0}.", fightChance));
            
            if (responseRoll > fightChance)
            {
                // Non-violent response
                HandleNonViolentResponse(initiator, recipient, partner, isKind, isWimp, hasFreeLoveOrPolygamy);
            }
            else
            {
                // Violent response - fight the cheater
                if (initiator.Faction == recipient.Faction && 
                    initiator.mindState != null && 
                    initiator.mindState.mentalStateHandler != null &&
                    !initiator.Downed && !initiator.Dead && 
                    !recipient.Downed && !recipient.Dead &&
                    initiator.Spawned && recipient.Spawned &&
                    initiator.Awake() && recipient.Awake() &&
                    SocialInteractionUtility.CanInitiateInteraction(initiator) &&
                    SocialInteractionUtility.CanReceiveInteraction(recipient))
                {
                    bool fightStarted = initiator.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.SocialFighting, null, false, false, false, recipient);
                    if (!fightStarted)
                    {
                        // Log why the fight failed to start if needed for debugging
                        if (initiator.mindState.mentalStateHandler.CurState != null)
                        {
                            SLog.Message(string.Format("[SocialInteractions] TriggerFightLogic: Initiator already in mental state: {0}", initiator.mindState.mentalStateHandler.CurState.def.defName));
                        }
                        // Fallback to non-violent response if fight couldn't start
                        HandleNonViolentResponse(initiator, recipient, partner, isKind, isWimp, hasFreeLoveOrPolygamy);
                    }
                }
                else
                {
                    // Log which conditions failed if needed for debugging
                    if (initiator.Faction != recipient.Faction)
                        SLog.Message("[SocialInteractions] TriggerFightLogic: Faction mismatch between initiator and recipient.");
                    // Fallback to non-violent response
                    HandleNonViolentResponse(initiator, recipient, partner, isKind, isWimp, hasFreeLoveOrPolygamy);
                }
            }
        }
        
        private bool HasFreeLoveOrPolygamyPrecept(Pawn pawn)
        {
            // Check if Ideology is active
            if (!ModsConfig.IdeologyActive)
            {
                return false;
            }
            
            // Check if the pawn has an ideology
            if (pawn.Ideo == null)
            {
                return false;
            }
            
            // Check for free love precept (Lovin_FreeApproved) or polygamy precepts
            return pawn.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("Lovin_FreeApproved")) ||
                   pawn.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("SpouseCount_Male_Unlimited")) ||
                   pawn.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("SpouseCount_Female_Unlimited")) ||
                   pawn.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("SpouseCount_Male_MaxThree")) ||
                   pawn.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("SpouseCount_Female_MaxThree")) ||
                   pawn.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("SpouseCount_Male_MaxFour")) ||
                   pawn.Ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("SpouseCount_Female_MaxFour"));
        }
        
        private void HandleNonViolentResponse(Pawn initiator, Pawn recipient, Pawn partner, bool isKind, bool isWimp, bool hasFreeLoveOrPolygamy)
        {
            // Different non-violent responses based on traits and other factors
            float responseRoll = Rand.Value;
            
            // If the pawn has free love or polygamy precept and there's a partner, join them in a 3p action
            if (hasFreeLoveOrPolygamy && partner != null && responseRoll < 0.7f)
            {
                // 3p action - initiate a special lovin' job with all three pawns
                SLog.Message(string.Format("[SocialInteractions] HandleNonViolentResponse: {0} has free love/polygamy precept, initiating 3p action with {1} and {2}.", 
                    initiator.LabelShort, recipient.LabelShort, partner.LabelShort));
                
                // Log the 3p action
                SLog.Message(string.Format("[SocialInteractions] InitiateThreewayLovin: {0}, {1}, and {2} engaged in a 3p action.", 
                    initiator.LabelShort, recipient.LabelShort, partner.LabelShort));
                
                // For 3p route, add positive thoughts for all pawns (no negative thoughts)
                ThoughtDef threewayLovinThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("ThreewayLovin");
                if (threewayLovinThought != null)
                {
                    // Add positive thoughts for all involved pawns
                    initiator.needs.mood.thoughts.memories.TryGainMemory(threewayLovinThought, recipient);
                    recipient.needs.mood.thoughts.memories.TryGainMemory(threewayLovinThought, initiator);
                    partner.needs.mood.thoughts.memories.TryGainMemory(threewayLovinThought, initiator);
                }
            }
            else if (isWimp && responseRoll < 0.7f)
            {
                // Wimpy response - run away (add appropriate thoughts for all pawns)
                SLog.Message(string.Format("[SocialInteractions] HandleNonViolentResponse: {0} is running away (wimp).", initiator.LabelShort));
                
                // Add appropriate thoughts for all pawns
                // Initiator gets a wimp-specific thought
                ThoughtDef reconcilingThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("ReconcilingAfterCheating");
                if (reconcilingThought != null)
                {
                    initiator.needs.mood.thoughts.memories.TryGainMemory(reconcilingThought, recipient);
                }

                // Recipient (the cheater) gets GotCaughtCheating thought
                ThoughtDef gotCaughtCheatingThought = DefDatabase<ThoughtDef>.GetNamed("GotCaughtCheating");
                if (gotCaughtCheatingThought != null)
                {
                    recipient.needs.mood.thoughts.memories.TryGainMemory(gotCaughtCheatingThought, initiator);
                }

                // Partner (the one being cheated on) gets WasCheatedOn thought
                if (partner != null)
                {
                    ThoughtDef wasCheatedOnThought = DefDatabase<ThoughtDef>.GetNamed("WasCheatedOn");
                    if (wasCheatedOnThought != null)
                    {
                        partner.needs.mood.thoughts.memories.TryGainMemory(wasCheatedOnThought, recipient);
                    }
                }
                
                // Create a list of threats (in this case, just the recipient)
                List<Thing> threats = new List<Thing> { recipient };
                
                // Try to find a cell to flee to
                IntVec3 fleeCell = CellFinderLoose.GetFleeDest(initiator, threats, 15f); // Flee farther away
                
                if (fleeCell.IsValid && fleeCell != initiator.Position)
                {
                    // Create a job for the initiator to go to the flee cell
                    Job fleeJob = JobMaker.MakeJob(JobDefOf.Goto, fleeCell);
                    fleeJob.locomotionUrgency = LocomotionUrgency.Sprint; // Make them sprint away
                    fleeJob.expiryInterval = 1200; // Expire the job after 20 seconds if not completed
                    
                    // Start the job
                    initiator.jobs.TryTakeOrderedJob(fleeJob);
                }
            }
            else if (isKind && responseRoll < 0.6f)
            {
                // Kind response - try to reconcile (add appropriate thoughts for all pawns)
                SLog.Message(string.Format("[SocialInteractions] HandleNonViolentResponse: {0} is trying to reconcile (kind).", initiator.LabelShort));
                
                // Add appropriate thoughts for all pawns
                // Initiator also gets a reconcile-specific thought
                ThoughtDef reconcilingThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("ReconcilingAfterCheating");
                if (reconcilingThought != null)
                {
                    initiator.needs.mood.thoughts.memories.TryGainMemory(reconcilingThought, recipient);
                }

                // Recipient (the cheater) gets GotCaughtCheating thought
                ThoughtDef gotCaughtCheatingThought = DefDatabase<ThoughtDef>.GetNamed("GotCaughtCheating");
                if (gotCaughtCheatingThought != null)
                {
                    recipient.needs.mood.thoughts.memories.TryGainMemory(gotCaughtCheatingThought, initiator);
                }

                // Partner (the one being cheated on) gets WasCheatedOn thought
                if (partner != null)
                {
                    ThoughtDef wasCheatedOnThought = DefDatabase<ThoughtDef>.GetNamed("WasCheatedOn");
                    if (wasCheatedOnThought != null)
                    {
                        partner.needs.mood.thoughts.memories.TryGainMemory(wasCheatedOnThought, recipient);
                    }
                }
            }
            else
            {
                // Default non-violent response - just end the relationship (add appropriate thoughts for all pawns)
                SLog.Message(string.Format("[SocialInteractions] HandleNonViolentResponse: {0} is breaking up with {1}.", initiator.LabelShort, recipient.LabelShort));
                
                // Add appropriate thoughts for all pawns
                // Initiator (the one who caught cheating) gets CaughtCheating thought and breakup thought
                ThoughtDef caughtCheatingThought = DefDatabase<ThoughtDef>.GetNamed("CaughtCheating");
                if (caughtCheatingThought != null)
                {
                    initiator.needs.mood.thoughts.memories.TryGainMemory(caughtCheatingThought, recipient);
                }
                
                ThoughtDef brokeUpThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("BrokeUpAfterCheating");
                if (brokeUpThought != null)
                {
                    initiator.needs.mood.thoughts.memories.TryGainMemory(brokeUpThought, recipient);
                }

                // Recipient (the cheater) gets GotCaughtCheating thought
                ThoughtDef gotCaughtCheatingThought = DefDatabase<ThoughtDef>.GetNamed("GotCaughtCheating");
                if (gotCaughtCheatingThought != null)
                {
                    recipient.needs.mood.thoughts.memories.TryGainMemory(gotCaughtCheatingThought, initiator);
                }

                // Partner (the one being cheated on) gets WasCheatedOn thought
                if (partner != null)
                {
                    ThoughtDef wasCheatedOnThought = DefDatabase<ThoughtDef>.GetNamed("WasCheatedOn");
                    if (wasCheatedOnThought != null)
                    {
                        partner.needs.mood.thoughts.memories.TryGainMemory(wasCheatedOnThought, recipient);
                    }
                }
                
                // Break up with the cheater by removing the relationship
                // Find the existing relationship between initiator and recipient
                PawnRelationDef relationDef = null;
                DirectPawnRelation relation = null;
                
                // Check for different types of relationships
                if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Spouse, recipient))
                {
                    relationDef = PawnRelationDefOf.Spouse;
                    relation = new DirectPawnRelation(relationDef, recipient, 0);
                }
                else if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Lover, recipient))
                {
                    relationDef = PawnRelationDefOf.Lover;
                    relation = new DirectPawnRelation(relationDef, recipient, 0);
                }
                else if (initiator.relations.DirectRelationExists(PawnRelationDefOf.Fiance, recipient))
                {
                    relationDef = PawnRelationDefOf.Fiance;
                    relation = new DirectPawnRelation(relationDef, recipient, 0);
                }
                
                // If we found a relationship, remove it and add the appropriate ex-relationship
                if (relationDef != null)
                {
                    // Remove the current relationship
                    initiator.relations.RemoveDirectRelation(relationDef, recipient);
                    
                    // Add the appropriate ex-relationship
                    if (relationDef == PawnRelationDefOf.Spouse)
                    {
                        initiator.relations.AddDirectRelation(PawnRelationDefOf.ExSpouse, recipient);
                    }
                    else if (relationDef == PawnRelationDefOf.Lover)
                    {
                        initiator.relations.AddDirectRelation(PawnRelationDefOf.ExLover, recipient);
                    }
                    else if (relationDef == PawnRelationDefOf.Fiance)
                    {
                        // For fiance, we just remove it without adding an ex-relationship
                        SLog.Message(string.Format("[SocialInteractions] HandleNonViolentResponse: Removed engagement between {0} and {1}.", initiator.LabelShort, recipient.LabelShort));
                    }
                }
            }
        }
        
        public void MakePartnerFleeImmediately(Pawn partner, Pawn initiator)
        {
            if (partner == null || initiator == null)
            {
                SLog.Warning("[SocialInteractions] MakePartnerFleeImmediately: Partner or initiator is null, skipping.");
                return;
            }
            
            SLog.Message(string.Format("[SocialInteractions] MakePartnerFleeImmediately: Making partner {0} flee from initiator {1}.", partner.LabelShort, initiator.LabelShort));
            TryMakePartnerFlee(partner, initiator);
        }
        
        private void TryMakePartnerFlee(Pawn partner, Pawn initiator)
        {
            if (partner == null || initiator == null || partner.Map == null)
            {
                return;
            }
            
            // Don't remove the SI_OnDate hediff here - let the JobDriver_CaughtCheating handle it
            // when the confrontation is finished
            
            // Create a list of threats (in this case, just the initiator)
            List<Thing> threats = new List<Thing> { initiator };
            
            // Try to find a cell to flee to
            IntVec3 fleeCell = CellFinderLoose.GetFleeDest(partner, threats, 10f); // Flee 10 cells away
            
            if (fleeCell.IsValid && fleeCell != partner.Position)
            {
                // Create a job for the partner to go to the flee cell
                Job fleeJob = JobMaker.MakeJob(JobDefOf.Goto, fleeCell);
                fleeJob.locomotionUrgency = LocomotionUrgency.Sprint; // Make them sprint away
                fleeJob.expiryInterval = 900; // Expire the job after 10 seconds if not completed
                
                // Start the job
                partner.jobs.TryTakeOrderedJob(fleeJob);
            }
        }
    }
}