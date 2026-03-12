using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace SocialInteractions
{
    public class VoiceAssignmentManager : GameComponent
    {
        // Persistent mapping of Pawn -> VoiceName
        private Dictionary<Pawn, string> voiceMapping = new Dictionary<Pawn, string>();

        // Cache for available voices (runtime only, not saved)
        private static List<string> availableVoices = new List<string>();
        
        public static List<string> AvailableVoices
        {
            get { return availableVoices; }
        }

        public VoiceAssignmentManager(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (SocialInteractions.Settings.enableTTS)
            {
                TTSManager.FetchVoicesFromApi();
            }
        }

        public void ResetAllocations()
        {
            if (voiceMapping != null)
            {
                voiceMapping.Clear();
                SLog.Message("[SocialInteractions] Voice allocations reset.");
            }
        }

        // New method to assign voices with even distribution (when fewer voices than pawns)
        public void AssignUniqueVoices(List<Pawn> pawns)
        {
            if (pawns == null || pawns.Count == 0 || availableVoices == null || availableVoices.Count == 0)
            {
                return;
            }

            // Create separate lists for male and female voices
            List<string> allMaleVoices = new List<string>();
            List<string> allFemaleVoices = new List<string>();

            foreach (var voice in availableVoices)
            {
                // Check if voice matches gender-specific prefixes
                string voiceForPrefixCheck = voice;

                // If the voice ends with a file extension, extract the base name for prefix checking
                if (voice.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                    voice.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                    voice.EndsWith(".flac", StringComparison.OrdinalIgnoreCase) ||
                    voice.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                    voice.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase))
                {
                    int extIndex = voice.LastIndexOf('.');
                    if (extIndex > 0)
                    {
                        voiceForPrefixCheck = voice.Substring(0, extIndex); // Remove extension for prefix checking
                    }
                }

                if (voiceForPrefixCheck.StartsWith("am_", StringComparison.OrdinalIgnoreCase) ||
                    voiceForPrefixCheck.StartsWith("bm_", StringComparison.OrdinalIgnoreCase) ||
                    voiceForPrefixCheck.StartsWith("m_", StringComparison.OrdinalIgnoreCase) ||
                    voiceForPrefixCheck.StartsWith("[Male]", StringComparison.OrdinalIgnoreCase))
                {
                    allMaleVoices.Add(voice);
                }
                else if (voiceForPrefixCheck.StartsWith("af_", StringComparison.OrdinalIgnoreCase) ||
                         voiceForPrefixCheck.StartsWith("bf_", StringComparison.OrdinalIgnoreCase) ||
                         voiceForPrefixCheck.StartsWith("f_", StringComparison.OrdinalIgnoreCase) ||
                         voiceForPrefixCheck.StartsWith("[Female]", StringComparison.OrdinalIgnoreCase))
                {
                    allFemaleVoices.Add(voice);
                }
            }

            // Shuffle both gender-specific lists to ensure randomness
            ShuffleList(allMaleVoices);
            ShuffleList(allFemaleVoices);

            // Separate male and female pawns
            List<Pawn> malePawns = new List<Pawn>();
            List<Pawn> femalePawns = new List<Pawn>();

            foreach (Pawn pawn in pawns)
            {
                if (pawn != null && pawn.gender != Gender.None) // Skip genderless pawns
                {
                    if (pawn.gender == Gender.Male)
                    {
                        malePawns.Add(pawn);
                    }
                    else
                    {
                        femalePawns.Add(pawn);
                    }
                }
            }

            // Assign voices to male pawns using round-robin from available male voices
            for (int i = 0; i < malePawns.Count; i++)
            {
                if (allMaleVoices.Count > 0)
                {
                    // Use round-robin assignment: voice[i % voiceCount]
                    string assignedVoice = allMaleVoices[i % allMaleVoices.Count];
                    voiceMapping[malePawns[i]] = assignedVoice;
                }
                else if (availableVoices.Count > 0)
                {
                    // If no gender-specific voices, use any available voice
                    string assignedVoice = availableVoices[i % availableVoices.Count];
                    voiceMapping[malePawns[i]] = assignedVoice;
                }
                else
                {
                    voiceMapping[malePawns[i]] = "alloy"; // Fallback if no voices available
                }
            }

            // Assign voices to female pawns using round-robin from available female voices
            for (int i = 0; i < femalePawns.Count; i++)
            {
                if (allFemaleVoices.Count > 0)
                {
                    // Use round-robin assignment: voice[i % voiceCount]
                    string assignedVoice = allFemaleVoices[i % allFemaleVoices.Count];
                    voiceMapping[femalePawns[i]] = assignedVoice;
                }
                else if (availableVoices.Count > 0)
                {
                    // If no gender-specific voices, use any available voice
                    string assignedVoice = availableVoices[i % availableVoices.Count];
                    voiceMapping[femalePawns[i]] = assignedVoice;
                }
                else
                {
                    voiceMapping[femalePawns[i]] = "alloy"; // Fallback if no voices available
                }
            }
        }

        // Helper method to shuffle a list using the Fisher-Yates algorithm
        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Rand.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        // Working lists for Scribe
        private List<Pawn> scribeKeys;
        private List<string> scribeValues;

        public override void ExposeData()
        {
            base.ExposeData();

            // Manual handling to prevent "Null key" errors during load
            // This replaces Scribe_Collections.Look(ref voiceMapping...) which crashes on null keys
            
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (voiceMapping != null)
                {
                    // Remove null keys before saving just in case
                    List<Pawn> keysToRemove = new List<Pawn>();
                    foreach(var key in voiceMapping.Keys)
                    {
                        if (key == null) keysToRemove.Add(key);
                    }
                    foreach(var key in keysToRemove)
                    {
                         voiceMapping.Remove(key);
                    }

                    scribeKeys = new List<Pawn>(voiceMapping.Keys);
                    scribeValues = new List<string>(voiceMapping.Values);
                }
            }

            // Mimic the structure of Scribe_Collections.Look(Dictionary) for compatibility
            if (Scribe.EnterNode("voiceMapping"))
            {
                try
                {
                    Scribe_Collections.Look(ref scribeKeys, "keys", LookMode.Reference);
                    Scribe_Collections.Look(ref scribeValues, "values", LookMode.Value);
                }
                finally
                {
                    Scribe.ExitNode();
                }
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (voiceMapping == null)
                {
                    voiceMapping = new Dictionary<Pawn, string>();
                }
                voiceMapping.Clear();

                if (scribeKeys != null && scribeValues != null)
                {
                    for (int i = 0; i < scribeKeys.Count; i++)
                    {
                        // The critical fix: Check for null keys before adding to dictionary
                        if (scribeKeys[i] != null)
                        {
                            voiceMapping[scribeKeys[i]] = scribeValues[i];
                        }
                    }
                }
            }
        }

        public string GetOrAssignVoice(Pawn pawn)
        {
            if (pawn == null) return "alloy";

            string assignedVoice;
            if (voiceMapping.TryGetValue(pawn, out assignedVoice))
            {
                // Only return if it's not empty
                if (!string.IsNullOrEmpty(assignedVoice)) return assignedVoice;
            }

            // If we don't have available voices loaded yet, we can't assign a PERMANENT one correctly
            // because we don't know what's available.
            // But we can fallback to "alloy" (generic) without saving it, 
            // OR we can try to trigger a load?
            
            if (availableVoices == null || availableVoices.Count == 0)
            {
                // Fallback to default, do NOT save it yet
                return pawn.gender == Gender.Female ? "alloy" : "alloy"; // Placeholder defaults
            }

            // Allocate a new voice
            string newVoice = AssignNewVoice(pawn);
            if (!string.IsNullOrEmpty(newVoice))
            {
                 voiceMapping[pawn] = newVoice;
                 return newVoice;
            }

            return "alloy";
        }
        
        private string AssignNewVoice(Pawn pawn)
        {
            // Filter available voices based on gender
            List<string> candidates = new List<string>();
            // Include both American and British voice variants
            string[] prefixes = pawn.gender == Gender.Female 
                ? new string[] { "af_", "bf_", "f_", "[Female]" } 
                : new string[] { "am_", "bm_", "m_", "[Male]" };

            foreach (var voice in availableVoices)
            {
                // Check if voice matches any of the gender-appropriate prefixes
                // Handle both with and without file extensions
                string voiceForPrefixCheck = voice;

                // If the voice ends with a file extension, extract the base name for prefix checking
                if (voice.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                    voice.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                    voice.EndsWith(".flac", StringComparison.OrdinalIgnoreCase) ||
                    voice.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                    voice.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase))
                {
                    int extIndex = voice.LastIndexOf('.');
                    if (extIndex > 0)
                    {
                        voiceForPrefixCheck = voice.Substring(0, extIndex); // Remove extension for prefix checking
                    }
                }

                foreach (string prefix in prefixes)
                {
                    if (voiceForPrefixCheck.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        candidates.Add(voice); // Add the original voice name (with extension) to candidates
                        break; // No need to check other prefixes for this voice
                    }
                }
            }
            
            // If no gender-specific matches, maybe use any voice?
            if (candidates.Count == 0 && availableVoices.Count > 0)
            {
                // If strictly "af"/"am", we might find nothing. 
                // But let's assume if we have voices, we might want to use them.
                // For now, let's strict filter, but fallback to ANY if none match prefix? 
                // User said: "make sure... voices starting with af_ are female..."
                // Implies we SHOULD respect it.
                
                // If no matching gender voices found, return default (don't assign random mismatch)
                return "alloy"; 
            }

            if (candidates.Count > 0)
            {
                return candidates.RandomElement();
            }

            return "alloy";
        }

        public static void SetAvailableVoices(List<string> voices)
        {
            availableVoices = voices;
        }

        // Method to manually assign a specific voice to a pawn
        public void SetVoiceForPawn(Pawn pawn, string voiceName)
        {
            if (pawn == null) return;

            // Validate that the voice exists in available voices
            if (string.IsNullOrEmpty(voiceName) ||
                (availableVoices.Count > 0 && !availableVoices.Contains(voiceName)))
            {
                string originalPawnName = pawn.Name != null ? pawn.Name.ToStringShort : "Unknown";
                SLog.Warning(string.Format("[SocialInteractions] Attempted to assign invalid voice '{0}' to pawn {1}",
                    voiceName, originalPawnName));
                return;
            }

            voiceMapping[pawn] = voiceName;
            string assignedPawnName = pawn.Name != null ? pawn.Name.ToStringShort : "Unknown";
            SLog.Message(string.Format("[SocialInteractions] Manually assigned voice '{0}' to pawn {1}",
                voiceName, assignedPawnName));
        }

        // Method to get the currently assigned voice for a pawn
        public string GetVoiceForPawn(Pawn pawn)
        {
            if (pawn == null) return null;

            string assignedVoice;
            if (voiceMapping.TryGetValue(pawn, out assignedVoice))
            {
                return assignedVoice;
            }
            return null;
        }

        // Method to clear a pawn's voice assignment (will trigger reassignment)
        public void ClearVoiceForPawn(Pawn pawn)
        {
            if (pawn == null) return;

            voiceMapping.Remove(pawn);
        }

        // Method to get all currently assigned voices with pawn names
        public Dictionary<string, string> GetVoiceAssignmentsSummary()
        {
            var summary = new Dictionary<string, string>();
            foreach (var kvp in voiceMapping)
            {
                if (kvp.Key != null && !string.IsNullOrEmpty(kvp.Value))
                {
                    string pawnName = kvp.Key.Name != null ? kvp.Key.Name.ToStringShort : "Unknown Pawn";
                    summary[pawnName] = kvp.Value;
                }
            }
            return summary;
        }
    }
}
