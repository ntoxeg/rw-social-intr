using RimWorld;
using Verse;
using System.Collections.Generic;
using SocialInteractions;

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

        public PawnMemory_GameComponent()
        {
        }

        public PawnMemory_GameComponent(Game game)
        {
            // Register component for global access
            Services.Memory = this;
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
            lock (bufferLock)
            {
                buffer.Remove(pawnId);
            }
        }
    }
}
