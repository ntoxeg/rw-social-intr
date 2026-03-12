using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SocialInteractions
{
    public class JobDriver_HaveDeepTalk : JobDriver
    {
        public InteractionDef interactionDef;
        public string subject;
        
        private Pawn Recipient { get { return (Pawn)job.GetTarget(TargetIndex.A).Thing; } }
        private bool llmTaskComplete = false;
        private string llmResponse;
        private List<string> messages = new List<string>();
        private int conversationId = -1;

        public override void ExposeData()
        {
            base.ExposeData();
            // These will be serialized with the job itself, not the driver
            Scribe_Values.Look(ref llmTaskComplete, "llmTaskComplete", false);
            Scribe_Values.Look(ref llmResponse, "llmResponse");
            Scribe_Collections.Look(ref messages, "messages", LookMode.Value);
            Scribe_Values.Look(ref conversationId, "conversationId", -1);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn recipient = (Pawn)job.GetTarget(TargetIndex.A).Thing;
            if (recipient == null) return false;
            return pawn.Reserve(recipient, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            SLog.Message("[SocialInteractions] JobDriver_HaveDeepTalk.MakeNewToils called.");
            
            // Add a finish action to ensure the conversation is ended regardless of how the job ends
            this.AddFinishAction((condition) => {
                if (this.conversationId != -1) {
                    SpeechBubbleManager.EndConversation(this.conversationId);
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_HaveDeepTalk: Ended conversation ID: {0} via finish action.", this.conversationId));
                    this.conversationId = -1;
                }
            });

            Pawn recipient = (Pawn)job.GetTarget(TargetIndex.A).Thing;
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => recipient == null || !recipient.Spawned || !recipient.Awake());

            // Go to the recipient
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Face each other
            Toil faceToil = new Toil();
            faceToil.initAction = () => {
                SLog.Message("[SocialInteractions] JobDriver_HaveDeepTalk: Facing recipient.");
                pawn.rotationTracker.FaceCell(recipient.Position);
                recipient.rotationTracker.FaceCell(pawn.Position);
            };
            faceToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return faceToil;

            // Get LLM response
            Toil getLlmResponseToil = new Toil();
            getLlmResponseToil.initAction = () => {
                SLog.Message("[SocialInteractions] JobDriver_HaveDeepTalk: Starting LLM response toil.");
                
                // Start a conversation to indicate LLM activity, so subsequent calls will be blocked by spam protection
                this.conversationId = SpeechBubbleManager.StartConversation();
                SLog.Message(string.Format("[SocialInteractions] JobDriver_HaveDeepTalk: Started conversation ID: {0}", this.conversationId));
                
                if (this.job == null) {
                    SLog.Error("Job is null. Ending job.");
                    pawn.jobs.EndCurrentJob(JobCondition.Errored);
                    return;
                }
                
                try
                {
                    llmTaskComplete = false;

                    Pawn recipientForTask = recipient;
                    // Cast the job to Job_HaveDeepTalk to access the interactionDef and subject
                    Job_HaveDeepTalk customJob = this.job as Job_HaveDeepTalk;
                    if (customJob == null)
                    {
                        SLog.Error("Job is not a Job_HaveDeepTalk. Ending job.");
                        pawn.jobs.EndCurrentJob(JobCondition.Errored);
                        return;
                    }

                    InteractionDef interactionDefForTask = customJob.interactionDef;
                    if (interactionDefForTask == null)
                    {
                        SLog.Error("InteractionDef is null. Ending job.");
                        pawn.jobs.EndCurrentJob(JobCondition.Errored);
                        return;
                    }

                    string subjectForTask = customJob.subject;
                    if (subjectForTask == null)
                    {
                        SLog.Error("Subject is null. Ending job.");
                        pawn.jobs.EndCurrentJob(JobCondition.Errored);
                        return;
                    }

                    SLog.Message(string.Format("[SocialInteractions] JobDriver_HaveDeepTalk: InteractionDef={0}, Subject={1}", 
                        interactionDefForTask.defName, subjectForTask));

                    Task.Run(async () => {
                        KoboldApiClient client = null;
                        try
                        {
                            if (recipientForTask == null)
                            {
                                SLog.Error("Recipient became null before LLM task could run.");
                                llmTaskComplete = true;
                                return;
                            }

                            if (SocialInteractions.Settings == null)
                            {
                                SLog.Error("SocialInteractions.Settings is null. Cannot generate LLM response.");
                                llmTaskComplete = true;
                                return;
                            }

                            if (string.IsNullOrEmpty(SocialInteractions.Settings.llmApiUrl))
                            {
                                SLog.Error("LLM API URL is not set in mod settings. Cannot generate LLM response.");
                                llmTaskComplete = true;
                                return;
                            }

                            if (interactionDefForTask == null)
                            {
                                SLog.Error("InteractionDef is null inside Task.Run. Cannot generate LLM response.");
                                llmTaskComplete = true;
                                return;
                            }

                            string prompt = SocialInteractions.GenerateDeepTalkPrompt(pawn, recipientForTask, interactionDefForTask, subjectForTask);
                            if (!string.IsNullOrEmpty(prompt))
                            {
                                client = new KoboldApiClient(SocialInteractions.Settings.llmApiUrl, SocialInteractions.Settings.llmApiKey);
                                llmResponse = await client.GenerateText(prompt);
                                
                                if (llmResponse == null)
                                {
                                    SLog.Warning("[SocialInteractions] JobDriver_HaveDeepTalk: LLM API returned null response");
                                    llmTaskComplete = true;
                                    return;
                                }
                                
                                if (!string.IsNullOrEmpty(llmResponse))
                                {
                                    messages = llmResponse.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                                }
                                else
                                {
                                    SLog.Warning("[SocialInteractions] JobDriver_HaveDeepTalk: LLM API returned empty response");
                                }
                            }
                            else
                            {
                                SLog.Warning("[SocialInteractions] JobDriver_HaveDeepTalk: Failed to generate prompt");
                            }
                        }
                        catch (Exception ex)
                        {
                            SLog.Error(string.Format("Error in Task.Run: {0} {1}", ex.Message, ex.StackTrace));
                        }
                        finally
                        {
                            if (client != null)
                            {
                                client.Dispose();
                            }
                            llmTaskComplete = true;
                        }
                    });
                }
                catch (Exception ex)
                {
                    SLog.Error(string.Format("Error in getLlmResponseToil: {0} {1}", ex.Message, ex.StackTrace));
                    pawn.jobs.EndCurrentJob(JobCondition.Errored);
                }
            };
            getLlmResponseToil.tickAction = () => {
                if (llmTaskComplete)
                {
                    SLog.Message("[SocialInteractions] JobDriver_HaveDeepTalk: LLM task complete, moving to next toil.");
                    getLlmResponseToil.actor.jobs.curDriver.ReadyForNextToil();
                }
            };
            getLlmResponseToil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return getLlmResponseToil;

            // Display messages
            Toil displayMessagesToil = new Toil();
            displayMessagesToil.initAction = () => {
                SLog.Message(string.Format("[SocialInteractions] JobDriver_HaveDeepTalk: Displaying messages. Message count: {0}", messages.Count));
                
                Pawn recipientForDisplay = (Pawn)job.GetTarget(TargetIndex.A).Thing;
                if (recipientForDisplay == null) return;

                for (int i = 0; i < messages.Count; i++)
                {
                    string rawMessage = messages[i].Trim();
                    
                    if (!string.IsNullOrWhiteSpace(rawMessage))
                    {
                        // Determine the speaker from the message
                        Pawn speaker = pawn; // Default to initiator
                        string messageText = rawMessage;
                        
                        // Check if the message starts with a speaker name
                        if (rawMessage.StartsWith(pawn.Name.ToStringShort + ":", StringComparison.OrdinalIgnoreCase))
                        {
                            speaker = pawn;
                            messageText = rawMessage.Substring(pawn.Name.ToStringShort.Length + 1).Trim();
                        }
                        else if (rawMessage.StartsWith(recipientForDisplay.Name.ToStringShort + ":", StringComparison.OrdinalIgnoreCase))
                        {
                            speaker = recipientForDisplay;
                            messageText = rawMessage.Substring(recipientForDisplay.Name.ToStringShort.Length + 1).Trim();
                        }
                        
                        SpeechBubbleManager.Enqueue(speaker, messageText, recipientForDisplay, i == 0, conversationId, true, subject); // Orange for high priority (stopping interactions), pass subject as fallback text
                    }
                }
            };
            displayMessagesToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return displayMessagesToil;

            // Wait for conversation to finish
            Toil waitForConversationToil = new Toil();
            waitForConversationToil.FailOn(() =>
            {
                Pawn recipientPawn = (Pawn)job.GetTarget(TargetIndex.A).Thing;
                return recipientPawn == null || recipientPawn.Downed || recipientPawn.Dead;
            });
            waitForConversationToil.tickAction = () => {
                if (job.def.joyKind != null && pawn.needs != null && pawn.needs.joy != null)
                {
                    pawn.needs.joy.GainJoy(0.00015f, job.def.joyKind);
                }
                if (conversationId == -1 || !SpeechBubbleManager.IsConversationActive(conversationId))
                {
                    SLog.Message("[SocialInteractions] JobDriver_HaveDeepTalk: Conversation finished, ending both jobs.");
                    // End both jobs when conversation is finished
                    
                    // First end the recipient's BeTalkedTo job if it exists
                    Pawn finalRecipient = (Pawn)job.GetTarget(TargetIndex.A).Thing;
                    if (finalRecipient != null && finalRecipient.jobs != null && finalRecipient.jobs.curDriver != null)
                    {
                        JobDriver_BeTalkedTo recipientDriver = finalRecipient.jobs.curDriver as JobDriver_BeTalkedTo;
                        if (recipientDriver != null)
                        {
                            SLog.Message(string.Format("[SocialInteractions] JobDriver_HaveDeepTalk: Ending recipient {0}'s BeTalkedTo job.", finalRecipient.LabelShort));
                            recipientDriver.EndJob(JobCondition.Succeeded);
                        }
                        else
                        {
                            SLog.Message(string.Format("[SocialInteractions] JobDriver_HaveDeepTalk: Recipient {0} is not doing a BeTalkedTo job.", finalRecipient.LabelShort));
                        }
                    }
                    else
                    {
                        SLog.Message("[SocialInteractions] JobDriver_HaveDeepTalk: Recipient is null or doesn't have a job.");
                    }
                    
                    // Then end this job
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_HaveDeepTalk: Ending initiator {0}'s job.", pawn.LabelShort));
                    pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                }
            };
            waitForConversationToil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return waitForConversationToil;
        }
    }
}