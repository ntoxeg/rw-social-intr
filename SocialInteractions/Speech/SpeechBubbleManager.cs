using Verse;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Text.RegularExpressions; // Add this using directive

namespace SocialInteractions
{
    public class SpeechBubbleManager : GameComponent
    {
        private static readonly object queueLock = new object();
        private static Queue<SpeechBubble> speechBubbleQueue = new Queue<SpeechBubble>();
        private static Dictionary<Pawn, float> pawnBubbleEndTimes = new Dictionary<Pawn, float>();
        private static float nextQueuedBubbleDisplayTime = 0f;
        private static float pauseStartTime = -1f; // Tracks when the game was paused
        private static int currentConversationId = 0;
        private static HashSet<int> activeConversations = new HashSet<int>();
        private static Dictionary<int, float> activeConversationStartTimes = new Dictionary<int, float>(); // Track start times for timeouts
        private const float ConversationTimeoutSeconds = 30f; // Fail-safe timeout
        
        // --- For Job Queue ---
        private static Queue<Action> pendingJobs = new Queue<Action>();
        // --- End For Job Queue ---

        public SpeechBubbleManager(Game game)
        {
            lock (queueLock)
            {
                speechBubbleQueue.Clear();
                pendingJobs.Clear();
            }
            pawnBubbleEndTimes.Clear();
            nextQueuedBubbleDisplayTime = 0f;
            pauseStartTime = -1f; // Initialize pause tracking
            currentConversationId = 0;
            activeConversations.Clear();

            // Clear the chat log on game load
            ChatLogManager.ClearChatLog();

            // Reset TTS state on game load
            TTSManager.Initialize();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // Check for game pause state and handle bubble display timing accordingly
            if (Find.TickManager.Paused)
            {
                // If we just entered pause state, record the time
                if (pauseStartTime < 0)
                {
                    pauseStartTime = Time.time;
                    SLog.Message("[SocialInteractions] Game paused. Pausing speech bubble timers.");
                }
                // If already paused, do nothing - bubbles will remain displayed
                return;
            }
            else
            {
                // If we're unpausing, adjust timers to account for paused duration
                if (pauseStartTime >= 0)
                {
                    float pauseDuration = Time.time - pauseStartTime;
                    nextQueuedBubbleDisplayTime += pauseDuration;

                    // Also adjust pawn bubble end times
                    List<Pawn> pawnsToUpdate = new List<Pawn>(pawnBubbleEndTimes.Keys);
                    foreach (Pawn pawn in pawnsToUpdate)
                    {
                        pawnBubbleEndTimes[pawn] += pauseDuration;
                    }

                    pauseStartTime = -1f; // Reset pause tracking
                    SLog.Message("[SocialInteractions] Game unpaused. Resuming speech bubble timers.");
                }
            }

            // Clean up expired instant bubbles
            List<Pawn> pawnsToRemove = new List<Pawn>();
            foreach (var entry in pawnBubbleEndTimes)
            {
                if (Time.time >= entry.Value)
                {
                    pawnsToRemove.Add(entry.Key);
                }
            }
            foreach (Pawn pawn in pawnsToRemove)
            {
                pawnBubbleEndTimes.Remove(pawn);
            }

            // Process queued bubbles
            lock (queueLock)
            {
                if (speechBubbleQueue.Count > 0 && Time.time >= nextQueuedBubbleDisplayTime)
                {
                    SpeechBubble bubble = speechBubbleQueue.Dequeue();
                    nextQueuedBubbleDisplayTime = Time.time + bubble.duration;
                    
                    try
                    {
                        // Trigger TTS when bubble pops out
                        if (!string.IsNullOrEmpty(bubble.ttsText))
                        {
                            SpeakIfEnabled(bubble.ttsText, bubble.speaker);
                        }

                        bool shouldShow = bubble.useCustomMote ? SocialInteractions.Settings.showLlmBubbles : SocialInteractions.Settings.showDefaultBubbles;
                        if (bubble.speaker != null && bubble.speaker.Map != null && shouldShow)
                        {
                            if (bubble.useCustomMote)
                            {
                                // Use custom mote for LLM-generated text
                                if (bubble.color.HasValue)
                                {
                                    MakeCustomMote(bubble.speaker, bubble.text, bubble.color.Value, bubble.duration);
                                }
                                else
                                {
                                    MakeCustomMote(bubble.speaker, bubble.text, Color.white, bubble.duration);
                                }
                            }
                            else
                            {
                                // Use standard mote for fallback text and combat dialogue
                                if (bubble.color.HasValue)
                                {
                                    MakeStandardMote(bubble.speaker, bubble.text, bubble.color.Value, bubble.duration);
                                }
                                else
                                {
                                    MakeStandardMote(bubble.speaker, bubble.text, Color.white, bubble.duration);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SLog.Error("[SpeechBubbleManager] Error displaying speech bubble: " + ex.Message);
                    }

                    if (bubble.conversationId != -1)
                    {
                        bool hasMoreBubblesInConversation = false;
                        foreach (SpeechBubble queuedBubble in speechBubbleQueue)
                        {
                            if (queuedBubble.conversationId == bubble.conversationId)
                            {
                                hasMoreBubblesInConversation = true;
                                break;
                            }
                        }
                        
                        if (!hasMoreBubblesInConversation)
                        {
                            EndConversation(bubble.conversationId);
                        }
                    }
                }
            }

            // Note: The IsLlmCurrentlyBusy() method provides real-time queue state for spam protection
            // The queue state is checked directly when needed rather than maintaining a potentially stale flag

            // --- Stale Conversation Cleanup ---
            lock (queueLock)
            {
                if (activeConversations.Count > 0)
                {
                    List<int> conversationsToRemove = new List<int>();
                    float currentTime = Time.time;
                    foreach (int convId in activeConversations)
                    {
                        float startTime;
                        if (activeConversationStartTimes.TryGetValue(convId, out startTime))
                        {
                            if (currentTime - startTime > ConversationTimeoutSeconds)
                            {
                                conversationsToRemove.Add(convId);
                                SLog.Warning(string.Format("[SpeechBubbleManager] Conversation {0} timed out after {1}s and was force-removed.", convId, ConversationTimeoutSeconds));
                            }
                        }
                        else
                        {
                            // If no start time, initialize it now (shouldn't happen with proper StartConversation call)
                            activeConversationStartTimes[convId] = currentTime;
                        }
                    }

                    foreach (int convId in conversationsToRemove)
                    {
                        EndConversation(convId);
                    }
                }
            }
            // --- End Stale Conversation Cleanup ---

            // Process pending jobs
            lock (queueLock)
            {
                // Process all pending jobs safely
                while (pendingJobs.Count > 0)
                {
                    Action jobAction = pendingJobs.Dequeue();
                    if (jobAction != null)
                    {
                        jobAction();
                    }
                }
            }
        }

        public static string GetDateSubject(Pawn initiator, Pawn recipient, LocalTargetInfo joySpot)
        {
            bool isRomantic = initiator.relations.DirectRelationExists(PawnRelationDefOf.Lover, recipient) ||
                              initiator.relations.DirectRelationExists(PawnRelationDefOf.Fiance, recipient) ||
                              initiator.relations.DirectRelationExists(PawnRelationDefOf.Spouse, recipient);

            string joySpotLabel = joySpot.Thing != null ? joySpot.Thing.Label : "a nice spot";

            if (isRomantic)
            {
                return string.Format("{0} has accepted {1}'s invitation for a date! Now they are hanging out together at {2}.", recipient.LabelShort, initiator.LabelShort, joySpotLabel);
            }
            else
            {
                return string.Format("{0} has accepted {1}'s invitation to hang out! Now they are visiting {2} together.", recipient.LabelShort, initiator.LabelShort, joySpotLabel);
            }
        }

        public static string GetDateEndSubject(Pawn initiator, Pawn recipient)
        {
            return string.Format("A successful date between {0} and {1} ends with a bang!", initiator.LabelShort, recipient.LabelShort);
        }
        
        public static string GetDateLovinSubject(Pawn initiator, Pawn recipient)
        {
            return string.Format("{0} and {1} are engaged in some wild lovin' after a fun date.", initiator.LabelShort, recipient.LabelShort);
        }
        
        public static string GetDateRejectionSubject(Pawn initiator, Pawn recipient)
        {
            return string.Format("{0} asks {1} for a date, but {1} declines.", initiator.LabelShort, recipient.LabelShort);
        }
        
        public static string GetPostDateLovinSubject(Pawn initiator, Pawn recipient)
        {
            return string.Format("{0} and {1} have finished their intimate moment and are reflecting on the experience.", initiator.LabelShort, recipient.LabelShort);
        }

        public static string GetDateWentBadlySubject(Pawn initiator, Pawn recipient)
        {
            return string.Format("{0} and {1} just had an awkward and uncomfortable date. The conversation was strained, there were misunderstandings, and both are feeling disappointed about how things went. They are expressing their frustration.", initiator.LabelShort, recipient.LabelShort);
        }

        public static void EnqueueJob(Action jobAction)
        {
            lock (queueLock)
            {
                pendingJobs.Enqueue(jobAction);
            }
        }

        public static int StartConversation()
        {
            lock (queueLock)
            {
                currentConversationId++;
                activeConversations.Add(currentConversationId);
                activeConversationStartTimes[currentConversationId] = Time.time;
            }
            return currentConversationId;
        }

        /// <summary>
        /// Increments the conversation count without marking it as active.
        /// Useful for background dialogue logging where we want a unique ID but 
        /// don't want to block the LLM busy state until speech bubbles are actually scheduled.
        /// </summary>
        public static int GetNextConversationId()
        {
            lock (queueLock)
            {
                currentConversationId++;
                return currentConversationId;
            }
        }

        public static void EndConversation(int conversationId)
        {
            lock (queueLock)
            {
                activeConversations.Remove(conversationId);
                activeConversationStartTimes.Remove(conversationId);
            }
        }

        public static bool IsConversationActive(int conversationId)
        {
            return activeConversations.Contains(conversationId);
        }

        public static bool IsLlmCurrentlyBusy()
        {
            lock (queueLock)
            {
                return speechBubbleQueue.Count > 0 || activeConversations.Count > 0;
            }
        }

        // --- For Queue Management ---
        /// <summary>
        /// Clears the speech bubble display queue. This is useful for high-priority interactions
        /// that should interrupt any queued messages. The currently displaying message will
        /// continue to its natural end; new messages will be displayed after it finishes.
        /// This method should be called from the main thread, e.g., via EnqueueJob.
        /// </summary>
        public static void ClearQueues()
        {
            lock (queueLock)
            {
                // Clear all pending speech bubbles
                speechBubbleQueue.Clear();
                
                // Do NOT clear activeConversations or pendingJobs here.
                // Clearing pendingJobs wipes out the high-priority jobs that were just 
                // enqueued (like the new response we're about to show).
                // Clearing activeConversations can interfere with the currently processing 
                // state of the LLM request.

                // Do NOT reset nextQueuedBubbleDisplayTime. This ensures that
                // the next message (from the high-priority interaction) will
                // wait for the current message's display time to finish naturally
                // before appearing, preventing visual overlap.
                // nextQueuedBubbleDisplayTime = Time.time; 
                
                SLog.Message("[SocialInteractions] Speech bubble queue cleared. Jobs and conversations preserved to handle high-priority response.");
            }
        }
        // --- End For Queue Management ---

        // Original enqueue method for simple messages (e.g., fallback messages)
        public static void Enqueue(Verse.Pawn speaker, string text, float duration, bool isFirstMessage, int conversationId, Color? color = null, bool useCustomMote = false)
        {
            lock (queueLock)
            {
                speechBubbleQueue.Enqueue(new SpeechBubble(speaker, text, duration, conversationId, false, color, useCustomMote));
            }
        }

        // New overload for LLM messages that handles all formatting internally
        public static void Enqueue(Verse.Pawn speaker, string rawMessage, Pawn recipient, float duration, bool isFirstMessage, int conversationId, bool isHighPriority = false, bool useCustomMote = true, string fallbackText = null, InteractionDef interactionDef = null)
        {
            // Format the message with speaker name and rich text
            string formattedMessage = FormatLlmMessage(rawMessage, speaker, recipient, isHighPriority);
            string wrappedMessage = SocialInteractions.WrapText(formattedMessage, SocialInteractions.Settings.wordsPerLineLimit);

            // Determine message type and color based on interaction type for proper chat log coloring
            MessageType messageType = MessageType.LLMChat; // Default
            Color messageColor = isHighPriority ? new Color(1.0f, 0.6f, 0.2f) : Color.white; // Orange for high priority, white for normal
            
            // Override message type and color based on interaction definition
            if (interactionDef != null)
            {
                if (interactionDef.defName == "Badmouthing" || 
                    interactionDef.defName == "CaughtCheating" ||
                    interactionDef.defName == "EnhancedInsult" ||
                    interactionDef.defName == "Admiration" ||
                    interactionDef.defName == "Insult" ||
                    interactionDef.defName == "Backstabbing")
                {
                    messageType = MessageType.DramaEvent; // Red for drama/insult interactions
                    messageColor = Color.red;
                }
                else if (interactionDef.defName == "DateAccepted" || 
                         interactionDef.defName == "DateRejected" || 
                         interactionDef.defName == "DateLovin" ||
                         interactionDef.defName == "GoOnDate" ||
                         interactionDef.defName == "Lovin" ||
                         interactionDef.defName == "RomanceAttempt" ||
                         interactionDef.defName == "MarriageProposal")
                {
                    messageType = MessageType.DateEvent; // Pink for dating/romance interactions
                    messageColor = new Color(1f, 0.7f, 0.7f); // Pink
                }
            }
            
            if (string.IsNullOrEmpty(fallbackText))
            {
                fallbackText = string.Format("{0} talks with {1}.", speaker.LabelShort, recipient.LabelShort);
            }
            ChatLogManager.AddMessage(new ChatMessage(speaker, recipient, rawMessage, messageType, conversationId, messageColor, fallbackText, formattedMessage));

            lock (queueLock)
            {
                speechBubbleQueue.Enqueue(new SpeechBubble(speaker, wrappedMessage, duration, conversationId, false, null, useCustomMote, rawMessage));
            }
        }

        // New overload that automatically calculates duration
        public static void Enqueue(Verse.Pawn speaker, string rawMessage, Pawn recipient, bool isFirstMessage, int conversationId, bool isHighPriority = false, string fallbackText = null)
        {
            float duration = EstimateReadingTime(rawMessage);
            Enqueue(speaker, rawMessage, recipient, duration, isFirstMessage, conversationId, isHighPriority, true, fallbackText);
        }

        // Overload for fallback messages that applies basic formatting
        public static void Enqueue(Verse.Pawn speaker, string text, float duration, bool isFirstMessage, int conversationId)
        {
            string wrappedMessage = SocialInteractions.WrapText(text, SocialInteractions.Settings.wordsPerLineLimit);
            // Add to chat log with fallback text
            ChatLogManager.AddMessage(new ChatMessage(speaker, null, text, MessageType.LLMChat, conversationId, Color.grey, text, text));
            lock (queueLock)
            {
                speechBubbleQueue.Enqueue(new SpeechBubble(speaker, wrappedMessage, duration, conversationId, false, null));
            }
        }

        // Overload for fallback messages that automatically calculates duration
        public static void Enqueue(Verse.Pawn speaker, string text, bool isFirstMessage, int conversationId)
        {
            float duration = EstimateReadingTime(text);
            Enqueue(speaker, text, duration, isFirstMessage, conversationId);
        }

        // New method for monologue messages that adds them to the chat log
        public static void EnqueueMonologue(Verse.Pawn speaker, string text, float duration, bool isFirstMessage, int conversationId, Color? color = null, bool useCustomMote = false, string subject = null, string ttsText = null)
        {
            // Add to chat log as a monologue message
            string fallbackText = string.IsNullOrEmpty(subject)
                ? string.Format("{0} thinks to themselves.", speaker.LabelShort)
                : string.Format("{0} ponders about {1}", speaker.LabelShort, subject);
            ChatLogManager.AddMessage(new ChatMessage(speaker, null, text, MessageType.LLMChat, conversationId, color ?? Color.grey, fallbackText, text));


            lock (queueLock)
            {
                speechBubbleQueue.Enqueue(new SpeechBubble(speaker, text, duration, conversationId, false, color, useCustomMote, ttsText ?? text));
            }
        }

        // For instant messages (combat taunts)
        public static void EnqueueInstant(Verse.Pawn speaker, string text, float duration, Color? color = null, bool useCustomMote = false)
        {
            float endTime;
            if (pawnBubbleEndTimes.TryGetValue(speaker, out endTime) && Time.time < endTime)
            {
                return; // Don't enqueue if this pawn already has an active instant bubble
            }
            duration = Math.Max(1f, duration);
            pawnBubbleEndTimes[speaker] = Time.time + duration; // Set bubbleEndTime for instant bubbles
            // No clearing of speechBubbleQueue here, as it's for instant display only
            bool shouldShow = useCustomMote ? SocialInteractions.Settings.showLlmBubbles : true; // Combat taunts (standard motes) have their own toggle
            if (speaker != null && speaker.Map != null && shouldShow)
            {
                string wrappedText = SocialInteractions.WrapText(text, SocialInteractions.Settings.wordsPerLineLimit);
                if (useCustomMote)
                {
                    // Use custom mote for LLM-generated text
                    if (color.HasValue)
                    {
                        MakeCustomMote(speaker, wrappedText, color.Value, duration);
                    }
                    else
                    {
                        MakeCustomMote(speaker, wrappedText, Color.white, duration);
                    }
                }
                else
                {
                    // Use standard mote for fallback text and combat dialogue
                    if (color.HasValue)
                    {
                        MakeStandardMote(speaker, wrappedText, color.Value, duration);
                    }
                    else
                    {
                        MakeStandardMote(speaker, wrappedText, Color.white, duration);
                    }
                }
            }
        }

        // New overload for LLM instant messages that handles all formatting internally
        public static void EnqueueInstant(Verse.Pawn speaker, string rawMessage, Pawn recipient, float duration, bool isHighPriority = false, bool useCustomMote = true)
        {
            // Format the message with speaker name and rich text
            string formattedMessage = FormatLlmMessage(rawMessage, speaker, recipient, isHighPriority);
            string wrappedMessage = SocialInteractions.WrapText(formattedMessage, SocialInteractions.Settings.wordsPerLineLimit);

            // Trigger TTS
            SpeakIfEnabled(rawMessage, speaker);
            
            // Add to chat log
            Color messageColor = isHighPriority ? new Color(1.0f, 0.6f, 0.2f) : Color.white; // Orange for high priority, white for normal
            string fallbackText = string.Format("{0} talks with {1}.", speaker.LabelShort, recipient.LabelShort);
            ChatLogManager.AddMessage(new ChatMessage(speaker, recipient, rawMessage, MessageType.LLMChat, -1, messageColor, fallbackText, formattedMessage));
            
            float endTime;
            if (pawnBubbleEndTimes.TryGetValue(speaker, out endTime) && Time.time < endTime)
            {
                return; // Don't enqueue if this pawn already has an active instant bubble
            }
            duration = Math.Max(1f, duration);
            pawnBubbleEndTimes[speaker] = Time.time + duration; // Set bubbleEndTime for instant bubbles
            // No clearing of speechBubbleQueue here, as it's for instant display only
            bool shouldShow = useCustomMote ? SocialInteractions.Settings.showLlmBubbles : true; // Combat taunts (standard motes) have their own toggle
            if (speaker != null && speaker.Map != null && shouldShow)
            {
                if (useCustomMote)
                {
                    // Use custom mote for LLM-generated text
                    MakeCustomMote(speaker, wrappedMessage, Color.white, duration);
                }
                else
                {
                    // Use standard mote for fallback text and combat dialogue
                    MakeStandardMote(speaker, wrappedMessage, Color.white, duration);
                }
            }
        }

        // New overload that automatically calculates duration
        public static void EnqueueInstant(Verse.Pawn speaker, string rawMessage, Pawn recipient, bool isHighPriority = false)
        {
            float duration = EstimateReadingTime(rawMessage);
            EnqueueInstant(speaker, rawMessage, recipient, duration, isHighPriority);
        }

        

        // For default summary bubbles
        public static void ShowDefaultBubble(Pawn speaker, string text)
        {
            float endTime;
            if (pawnBubbleEndTimes.TryGetValue(speaker, out endTime) && Time.time < endTime)
            {
                return; // Don't show if this pawn already has an active bubble
            }
            float duration = Math.Max(1f, SocialInteractions.EstimateReadingTime(text));
            pawnBubbleEndTimes[speaker] = Time.time + duration;
            if (speaker != null && speaker.Map != null && SocialInteractions.Settings.showDefaultBubbles)
            {
                string wrappedText = SocialInteractions.WrapText(text, SocialInteractions.Settings.wordsPerLineLimit);
                // Use standard mote for default bubbles
                MoteMaker.ThrowText(speaker.DrawPos, speaker.Map, wrappedText, new Color(0.75f, 0.75f, 0.75f));
            }
        }
        
        // Method to create a custom pauseable mote for LLM-generated text
        private static void MakeCustomMote(Pawn speaker, string text, Color color, float duration)
        {
            if (speaker == null || speaker.Map == null) 
            {
                SLog.Warning("[SocialInteractions] MakeCustomMote: speaker or speaker.Map is null");
                return;
            }
            
            // Create the custom mote
            PauseableMote mote = (PauseableMote)ThingMaker.MakeThing(SI_ThingDefOf.PauseableMote);
            if (mote == null)
            {
                SLog.Warning("[SocialInteractions] MakeCustomMote: Failed to create PauseableMote");
                return;
            }
            
            mote.exactPosition = speaker.DrawPos;
            mote.exactPosition.y = AltitudeLayer.MoteOverhead.AltitudeFor() + 1f; // Add a small offset
            mote.Scale = 1.0f;
            mote.originalDuration = duration;
            
            // Set the text and color
            mote.text = text;
            mote.textColor = color;
            
            // Spawn the mote
            GenSpawn.Spawn(mote, speaker.Position, speaker.Map, WipeMode.Vanish);
        }
        
        // Method to create a standard mote for fallback text and combat dialogue
        private static void MakeStandardMote(Pawn speaker, string text, Color color, float duration)
        {
            if (speaker == null || speaker.Map == null) 
            {
                SLog.Warning("[SocialInteractions] MakeStandardMote: speaker or speaker.Map is null");
                return;
            }
            
            // Use standard mote for fallback text and combat dialogue
            MoteMaker.ThrowText(speaker.DrawPos, speaker.Map, text, color, duration);
        }
        
        public static bool HasPendingSpeechBubbles(int conversationId)
        {
            lock (queueLock)
            {
                // Check if there are bubbles in the queue with the specified conversation ID
                foreach (SpeechBubble bubble in speechBubbleQueue)
                {
                    if (bubble.conversationId == conversationId)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        
        public static bool HasPendingSpeechBubblesForPawn(Pawn pawn)
        {
            lock (queueLock)
            {
                // Check if there are bubbles in the queue for the specified pawn
                foreach (SpeechBubble bubble in speechBubbleQueue)
                {
                    if (bubble.speaker == pawn)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        
        public static bool HasActiveConversations()
        {
            return activeConversations.Count > 0;
        }

        public static string FormatLlmText(string text)
        {
            // Use a regular expression to find text enclosed in asterisks, parentheses, or square brackets.
            text = Regex.Replace(text, @"\*(.*?)\*", "<color=#A9F0F0>$1</color>"); // light cyan for emphasis
            text = Regex.Replace(text, @"\((.*?)\)", "<color=#F0E68C>$1</color>"); // khaki for actions/emotes
            text = Regex.Replace(text, @"\[(.*?)\]", "<color=#DDA0DD>$1</color>"); // plum for thoughts/internal
            return text;
        }

        public static float EstimateReadingTime(string text)
        {
            // Simple estimate: words per second from settings.
            int wordCount = text.Split(new string[] { " ", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries).Length;
            float estimatedTime = 0f;
            if (SocialInteractions.Settings.wordsPerSecond <= 0)
            {
                estimatedTime = wordCount * 0.3f; // Fallback if setting is zero or negative
            }
            else
            {
                estimatedTime = wordCount / SocialInteractions.Settings.wordsPerSecond; // Seconds
            }
            return estimatedTime;
        }

        public static string FormatSpeakerName(Pawn speaker, string rawMessage, bool isHighPriority = false)
        {
            // Format the message to include the speaker's name with color
            string colorCode = isHighPriority ? "#ED7913" : "#87CEEB"; // Orange for high priority, light sky blue for normal
            string speakerNameWithColor = string.Format("<color={0}>{1}</color>", colorCode, speaker.LabelShort);
            string messageWithSpeaker = string.Format("{0}: {1}", speakerNameWithColor, rawMessage);
            return FormatLlmText(messageWithSpeaker);
        }

        public static string FormatLlmMessage(Pawn speaker, string rawMessage, bool isHighPriority = false)
        {
            // Extract the speaker name if it's included in the message
            string messageText = rawMessage;
            if (rawMessage.StartsWith(speaker.LabelShort + ":", StringComparison.OrdinalIgnoreCase))
            {
                messageText = rawMessage.Substring(speaker.LabelShort.Length + 1).Trim();
            }
            
            // Format the message with speaker name and rich text
            return FormatSpeakerName(speaker, messageText, isHighPriority);
        }

        // Overload for when we don't know the speaker yet
        public static string FormatLlmMessage(string rawMessage, Pawn pawn, Pawn recipient, bool isHighPriority = false)
        {
            Pawn speaker = null;
            string messageText = rawMessage;

            // Determine speaker and extract dialogue
            if (rawMessage.StartsWith(pawn.LabelShort + ":", StringComparison.OrdinalIgnoreCase))
            {
                speaker = pawn;
                messageText = rawMessage.Substring(pawn.LabelShort.Length + 1).Trim();
            }
            else if (recipient != null && rawMessage.StartsWith(recipient.LabelShort + ":", StringComparison.OrdinalIgnoreCase))
            {
                speaker = recipient;
                messageText = rawMessage.Substring(recipient.LabelShort.Length + 1).Trim();
            }
            else
            {
                speaker = pawn; // Default to initiator if speaker not specified
            }

            // Format the message with speaker name and rich text
            return FormatSpeakerName(speaker, messageText, isHighPriority);
        }

        /// <summary>
        /// Formats a monologue message for display.
        /// </summary>
        /// <param name="rawMessage">The raw message from the LLM.</param>
        /// <param name="pawn">The pawn who is speaking.</param>
        /// <param name="isHighPriority">Whether this is a high priority message.</param>
        /// <returns>The formatted message with speaker name and rich text.</returns>
        public static string FormatMonologueMessage(string rawMessage, Pawn pawn, bool isHighPriority = false)
        {
            string messageText = rawMessage;

            // Check if the message starts with the pawn's name
            if (rawMessage.StartsWith(pawn.LabelShort + ":", StringComparison.OrdinalIgnoreCase))
            {
                messageText = rawMessage.Substring(pawn.LabelShort.Length + 1).Trim();
            }

            // Format the message with speaker name and rich text
            return FormatSpeakerName(pawn, messageText, isHighPriority);
        }

        private static void SpeakIfEnabled(string text, Pawn speaker)
        {
            if (SocialInteractions.Settings.enableTTS)
            {
                string ttsText = text;
                
                // 1. Replace *message* with [message]
                ttsText = Regex.Replace(ttsText, @"\*([^*]+)\*", "[$1]");
                
                // 2. Replace [message] with [message]
                ttsText = Regex.Replace(ttsText, @"\[([^\]]+)\]", "[$1]");
                
                // 3. Replace <message> with [message], ignoring standard rich text tags
                // Ignored tags: b, i, color, size, material, quad (and their closing tags)
                // Pattern matches <...> but uses negative lookahead for known tags
                ttsText = Regex.Replace(ttsText, @"<(?!\/?(?:b|i|color|size|material|quad)\b)([^>]+)>", "[$1]");

                // 4. Strip remaining rich text tags (like <color=...>)
                string cleanText = Regex.Replace(ttsText, "<.*?>", string.Empty);
                
                TTSManager.Speak(cleanText, speaker, SocialInteractions.Settings.ttsSpeed, (int)SocialInteractions.Settings.ttsVolume);
            }
        }
    }

    public class SpeechBubble
    {
        public Pawn speaker;
        public string text;
        public float duration;
        public int conversationId;
        public bool isInstant;
        public Color? color;
        public bool useCustomMote; // Flag to indicate whether to use custom mote or standard mote
        public string ttsText; // Raw text for TTS

        public SpeechBubble(Pawn speaker, string text, float duration, int conversationId, bool isInstant = false, Color? color = null, bool useCustomMote = true, string ttsText = null)
        {
            this.speaker = speaker;
            this.text = text;
            this.duration = duration;
            this.conversationId = conversationId;
            this.isInstant = isInstant;
            this.color = color;
            this.useCustomMote = useCustomMote;
            this.ttsText = ttsText;
        }
    }
}