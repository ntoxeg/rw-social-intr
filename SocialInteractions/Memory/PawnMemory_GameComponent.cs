using RimWorld;
using Verse;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using SocialInteractions.Api;

namespace SocialInteractions.Memory
{
    /// <summary>
    /// GameComponent to handle pawn memory storage and daily event buffering.
    /// Provides persistent memory storage and thread-safe buffer management for social interactions.
    /// </summary>
    public class PawnMemory_GameComponent : GameComponent
    {
        // Dictionary to store pawn memories (key: pawn thingIDNumber, value: memory string)
        private Dictionary<int, string> memories = new Dictionary<int, string>();

        // Buffer for daily event accumulation (key: pawn thingIDNumber, value: list of events)
        private Dictionary<int, List<string>> buffer = new Dictionary<int, List<string>>();

        // Lock for thread-safe buffer access
        private readonly object bufferLock = new object();

        // Maximum entries per pawn in buffer
        private const int BufferCapacity = 50;

        // Daily processing cadence (60000 ticks = 1 in-game day)
        private const int DailyMemoryProcessingIntervalTicks = 60000;

        // Tick tracking and overlap prevention for daily memory processing
        private int lastDailyMemoryProcessingTick;
        private bool isProcessingDailyMemories;

        // Tracks the last in-game day when compaction was attempted per pawn
        private Dictionary<int, int> lastCompactionDayByPawn = new Dictionary<int, int>();

        public PawnMemory_GameComponent()
        {
        }

        public PawnMemory_GameComponent(Game game)
        {
            // Register component for global access
            Services.Memory = this;

            lastDailyMemoryProcessingTick = Find.TickManager != null
                ? Find.TickManager.TicksGame
                : 0;
            isProcessingDailyMemories = false;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // Expose memories dictionary using standard Scribe_Collections.Look
            Scribe_Collections.Look(ref memories, "memories", LookMode.Value, LookMode.Value);

            // Manual handling for buffer dictionary with List values (following VoiceAssignmentManager pattern)
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (buffer != null)
                {
                    // Prepare lists for serialization
                    List<int> bufferKeys = new List<int>(buffer.Keys);
                    List<List<string>> bufferValues = new List<List<string>>();
                    foreach (var kvp in buffer)
                    {
                        bufferValues.Add(new List<string>(kvp.Value));
                    }

                    // Save the prepared lists
                    if (Scribe.EnterNode("buffer"))
                    {
                        try
                        {
                            Scribe_Collections.Look(ref bufferKeys, "bufferKeys", LookMode.Value);
                            Scribe_Collections.Look(ref bufferValues, "bufferValues", LookMode.Value);
                        }
                        finally
                        {
                            Scribe.ExitNode();
                        }
                    }
                }
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (buffer == null)
                {
                    buffer = new Dictionary<int, List<string>>();
                }
                buffer.Clear();

                // Reconstruct buffer from saved lists
                if (Scribe.EnterNode("buffer"))
                {
                    try
                    {
                        List<int> bufferKeys = null;
                        List<List<string>> bufferValues = null;

                        Scribe_Collections.Look(ref bufferKeys, "bufferKeys", LookMode.Value);
                        Scribe_Collections.Look(ref bufferValues, "bufferValues", LookMode.Value);

                        if (bufferKeys != null && bufferValues != null)
                        {
                            for (int i = 0; i < bufferKeys.Count && i < bufferValues.Count; i++)
                            {
                                buffer[bufferKeys[i]] = bufferValues[i];
                            }
                        }
                    }
                    finally
                    {
                        Scribe.ExitNode();
                    }
                }
            }

            Scribe_Values.Look(ref lastDailyMemoryProcessingTick, "lastDailyMemoryProcessingTick", 0);
            Scribe_Values.Look(ref isProcessingDailyMemories, "isProcessingDailyMemories", false);
            Scribe_Collections.Look(ref lastCompactionDayByPawn, "lastCompactionDayByPawn", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lastCompactionDayByPawn == null)
                {
                    lastCompactionDayByPawn = new Dictionary<int, int>();
                }

                // Never resume a previously in-progress async run after loading.
                isProcessingDailyMemories = false;
            }
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if (!SocialInteractions.Settings.Memory.enableMemorySystem)
            {
                return;
            }

            if (isProcessingDailyMemories || Find.TickManager == null)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - lastDailyMemoryProcessingTick < DailyMemoryProcessingIntervalTicks)
            {
                return;
            }

            lastDailyMemoryProcessingTick = currentTick;
            isProcessingDailyMemories = true;

            Task.Run(async () =>
            {
                try
                {
                    await ProcessDailyMemoriesAsync();
                }
                catch (Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] Daily memory processing failed: {0}", ex.Message));
                }
                finally
                {
                    isProcessingDailyMemories = false;
                }
            });
        }

        private async Task ProcessDailyMemoriesAsync()
        {
            List<int> pawnsWithEntries = GetAllPawnsWithBufferEntries();
            if (pawnsWithEntries.Count == 0)
            {
                return;
            }

            SLog.Message(string.Format("[SocialInteractions] Processing memories for {0} pawns", pawnsWithEntries.Count));

            using (ILlmClient client = LlmClientFactory.Create(SocialInteractions.Settings))
            {
                foreach (int pawnId in pawnsWithEntries)
                {
                    Pawn pawn = FindPawnById(pawnId);
                    if (pawn == null || pawn.Destroyed || pawn.Dead || !pawn.Spawned)
                    {
                        GetAndClearBuffer(pawnId);
                        SLog.Warning(string.Format("[SocialInteractions] Skipping memory processing for missing/dead pawn {0}; cleaned buffer.", pawnId));
                        continue;
                    }

                    List<string> todaysEvents = GetAndClearBuffer(pawnId);
                    if (todaysEvents.Count == 0)
                    {
                        continue;
                    }

                    string prompt = BuildMemoryPrompt(pawn, GetMemory(pawnId), todaysEvents);

                    try
                    {
                        string responseText = await client.GenerateText(prompt);
                        if (string.IsNullOrWhiteSpace(responseText))
                        {
                            RestoreBufferEntries(pawnId, todaysEvents);
                            SLog.Warning(string.Format("[SocialInteractions] Memory write failed for {0}: empty LLM response", pawn.Name != null ? pawn.Name.ToStringShort : pawn.LabelShort));
                            continue;
                        }

                        SetMemory(pawnId, responseText.Trim());
                        await CompactMemory(pawnId);
                        SLog.Message(string.Format("[SocialInteractions] Memory updated for {0}", pawn.Name != null ? pawn.Name.ToStringShort : pawn.LabelShort));
                    }
                    catch (Exception ex)
                    {
                        RestoreBufferEntries(pawnId, todaysEvents);
                        SLog.Warning(string.Format("[SocialInteractions] Memory write failed for {0}: {1}", pawn.Name != null ? pawn.Name.ToStringShort : pawn.LabelShort, ex.Message));
                    }
                }
            }
        }

        private async Task CompactMemory(int pawnId)
        {
            string currentMemory = GetMemory(pawnId);
            if (string.IsNullOrEmpty(currentMemory))
            {
                return;
            }

            int beforeLength = currentMemory.Length;
            int charLimit = SocialInteractions.Settings.Memory.memoryCharacterLimit;
            if (charLimit <= 0)
            {
                return;
            }

            bool attemptedLlmCompaction = false;
            if (ShouldCompact(pawnId) && CanAttemptCompactionToday(pawnId))
            {
                attemptedLlmCompaction = true;
                MarkCompactionAttemptForToday(pawnId);

                Pawn pawn = FindPawnById(pawnId);
                string pawnName = pawn != null && pawn.Name != null ? pawn.Name.ToStringShort : (pawn != null ? pawn.LabelShort : string.Format("Pawn #{0}", pawnId));
                string compactionPrompt = BuildCompactionPrompt(pawnName, currentMemory, charLimit);

                try
                {
                    using (ILlmClient client = LlmClientFactory.Create(SocialInteractions.Settings))
                    {
                        string compacted = await client.GenerateText(compactionPrompt);
                        if (!string.IsNullOrWhiteSpace(compacted))
                        {
                            currentMemory = compacted.Trim();
                        }
                    }
                }
                catch (Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] Memory compaction LLM failed for {0}: {1}", pawnName, ex.Message));
                }
            }

            string truncatedMemory = ApplyFifoTruncation(currentMemory, charLimit);
            if (!string.Equals(truncatedMemory, currentMemory, StringComparison.Ordinal))
            {
                currentMemory = truncatedMemory;
            }

            if (!string.Equals(GetMemory(pawnId), currentMemory, StringComparison.Ordinal))
            {
                SetMemory(pawnId, currentMemory);
            }

            int afterLength = string.IsNullOrEmpty(currentMemory) ? 0 : currentMemory.Length;
            if (attemptedLlmCompaction || beforeLength != afterLength)
            {
                Pawn pawn = FindPawnById(pawnId);
                string pawnName = pawn != null && pawn.Name != null ? pawn.Name.ToStringShort : (pawn != null ? pawn.LabelShort : string.Format("Pawn #{0}", pawnId));
                SLog.Message(string.Format("[SocialInteractions] Compacted memories for {0}: {1} → {2}", pawnName, beforeLength, afterLength));
            }
        }

        private bool ShouldCompact(int pawnId)
        {
            string memory = GetMemory(pawnId);
            if (string.IsNullOrEmpty(memory))
            {
                return false;
            }

            int threshold = SocialInteractions.Settings.Memory.memoryCompactionThreshold;
            if (threshold <= 0)
            {
                threshold = (int)(SocialInteractions.Settings.Memory.memoryCharacterLimit * 0.8f);
            }

            return memory.Length > threshold;
        }

        private bool CanAttemptCompactionToday(int pawnId)
        {
            int currentDay = Find.TickManager != null
                ? Find.TickManager.TicksGame / DailyMemoryProcessingIntervalTicks
                : 0;

            int lastAttemptDay;
            if (lastCompactionDayByPawn.TryGetValue(pawnId, out lastAttemptDay))
            {
                return lastAttemptDay < currentDay;
            }

            return true;
        }

        private void MarkCompactionAttemptForToday(int pawnId)
        {
            int currentDay = Find.TickManager != null
                ? Find.TickManager.TicksGame / DailyMemoryProcessingIntervalTicks
                : 0;
            lastCompactionDayByPawn[pawnId] = currentDay;
        }

        private static string BuildCompactionPrompt(string pawnName, string fullMemories, int charLimit)
        {
            return SocialInteractions.Settings.Memory.memoryCompactionPromptTemplate
                .Replace("[pawn_name]", string.IsNullOrEmpty(pawnName) ? "Unknown" : pawnName)
                .Replace("[full_memories]", string.IsNullOrEmpty(fullMemories) ? "None" : fullMemories)
                .Replace("[char_limit]", charLimit.ToString());
        }

        internal static string ApplyFifoTruncation(string memory, int charLimit)
        {
            if (string.IsNullOrEmpty(memory) || charLimit <= 0 || memory.Length <= charLimit)
            {
                return memory;
            }

            string compacted = memory;
            while (compacted.Length > charLimit)
            {
                int charsToRemove = compacted.Length - charLimit;
                int cutPoint = charsToRemove;
                if (cutPoint <= 0 || cutPoint >= compacted.Length)
                {
                    break;
                }

                int sentenceBoundary = compacted.LastIndexOfAny(new char[] { '.', '\n' }, cutPoint - 1);
                int removeUntil = sentenceBoundary >= 0 ? sentenceBoundary + 1 : cutPoint;

                while (removeUntil < compacted.Length && char.IsWhiteSpace(compacted[removeUntil]))
                {
                    removeUntil++;
                }

                compacted = removeUntil >= compacted.Length ? string.Empty : compacted.Substring(removeUntil);

                if (removeUntil == 0)
                {
                    break;
                }
            }

            return compacted;
        }

        private static Pawn FindPawnById(int pawnId)
        {
            if (PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead == null)
            {
                return null;
            }

            for (int i = 0; i < PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Count; i++)
            {
                Pawn pawn = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead[i];
                if (pawn != null && pawn.thingIDNumber == pawnId)
                {
                    return pawn;
                }
            }

            return null;
        }

        private static string BuildMemoryPrompt(Pawn pawn, string existingMemory, List<string> todaysEvents)
        {
            string pawnName = pawn.Name != null ? pawn.Name.ToStringShort : pawn.LabelShort;
            string traitsText = GetTraitsText(pawn);
            string moodText = GetMoodText(pawn);
            int charLimit = SocialInteractions.Settings.Memory.memoryCharacterLimit;

            return SocialInteractions.Settings.Memory.memoryPromptTemplate
                .Replace("[pawn_name]", pawnName)
                .Replace("[existing_memories]", string.IsNullOrEmpty(existingMemory) ? "None" : existingMemory)
                .Replace("[todays_events]", string.Join("\n", todaysEvents.ToArray()))
                .Replace("[pawn_traits]", traitsText)
                .Replace("[pawn_mood]", moodText)
                .Replace("[char_limit]", charLimit.ToString());
        }

        private static string GetTraitsText(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return "None";
            }

            List<string> traits = new List<string>();
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait != null)
                {
                    traits.Add(trait.Label);
                }
            }

            return traits.Count > 0 ? string.Join(", ", traits.ToArray()) : "None";
        }

        private static string GetMoodText(Pawn pawn)
        {
            if (pawn == null || pawn.needs == null || pawn.needs.mood == null)
            {
                return "N/A";
            }

            float level = pawn.needs.mood.CurLevelPercentage;
            return string.Format("{0}% ({1})", (level * 100f).ToString("F0"), GetMoodLabel(level));
        }

        private static string GetMoodLabel(float level)
        {
            if (level < 0.15f) return "Deeply Upset";
            if (level < 0.35f) return "Upset";
            if (level < 0.60f) return "Neutral";
            if (level < 0.80f) return "Content";
            return "Happy";
        }

        private void RestoreBufferEntries(int pawnId, List<string> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            foreach (string entry in entries)
            {
                AddBufferEntry(pawnId, entry);
            }
        }

        /// <summary>
        /// Gets the memory for a pawn
        /// </summary>
        public string GetMemory(int pawnId)
        {
            if (memories.ContainsKey(pawnId))
            {
                return memories[pawnId];
            }
            return string.Empty;
        }

        /// <summary>
        /// Sets the memory for a pawn
        /// </summary>
        public void SetMemory(int pawnId, string memory)
        {
            if (string.IsNullOrEmpty(memory))
            {
                memories.Remove(pawnId);
            }
            else
            {
                memories[pawnId] = memory;
            }
        }

        /// <summary>
        /// Adds an entry to the daily event buffer for a pawn (thread-safe)
        /// </summary>
        public void AddBufferEntry(int pawnId, string entry)
        {
            lock (bufferLock)
            {
                if (!buffer.ContainsKey(pawnId))
                {
                    buffer[pawnId] = new List<string>();
                }

                buffer[pawnId].Add(entry);

                // Enforce buffer capacity: drop oldest entries if exceeded
                if (buffer[pawnId].Count > BufferCapacity)
                {
                    buffer[pawnId].RemoveRange(0, buffer[pawnId].Count - BufferCapacity);
                }
            }
        }

        /// <summary>
        /// Gets and clears the buffer for a pawn (thread-safe)
        /// </summary>
        public List<string> GetAndClearBuffer(int pawnId)
        {
            lock (bufferLock)
            {
                if (!buffer.ContainsKey(pawnId))
                {
                    return new List<string>();
                }

                List<string> result = new List<string>(buffer[pawnId]);
                buffer[pawnId].Clear();
                return result;
            }
        }

        /// <summary>
        /// Gets all pawns that have buffer entries (thread-safe)
        /// </summary>
        public List<int> GetAllPawnsWithBufferEntries()
        {
            lock (bufferLock)
            {
                List<int> result = new List<int>();
                foreach (var kvp in buffer)
                {
                    if (kvp.Value.Count > 0)
                    {
                        result.Add(kvp.Key);
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// Clears all memory and buffer data for a pawn
        /// </summary>
        public void ClearMemory(int pawnId)
        {
            memories.Remove(pawnId);
            lastCompactionDayByPawn.Remove(pawnId);
            lock (bufferLock)
            {
                buffer.Remove(pawnId);
            }
        }
    }
}
