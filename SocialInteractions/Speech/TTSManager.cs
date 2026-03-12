using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Text;

namespace SocialInteractions
{
    public static class TTSManager
    {
        // Managed AudioSource
        private static AudioSource audioSource;

        // Queue for TTS audio clips to prevent overlapping
        private static Queue<TTSQueueEntry> ttsQueue = new Queue<TTSQueueEntry>();
        private static bool isPlaying = false;

        // Ordering for async requests
        private static int nextRequestId = 0;
        private static int nextPlaybackId = 0;
        private static Dictionary<int, TTSQueueEntry> playbackBuffer = new Dictionary<int, TTSQueueEntry>();
        private static readonly object bufferLock = new object();
        
        // Lookup for Player2 Voice IDs: Name (Language) -> ID
        private static Dictionary<string, string> voiceIdLookup = new Dictionary<string, string>();


        public static void Initialize()
        {
            // Reset state on game load/init
            lock (bufferLock)
            {
                nextRequestId = 0;
                nextPlaybackId = 0;
                playbackBuffer.Clear();
                ttsQueue.Clear();
                isPlaying = false;
            }
        }

        public static void Speak(string text, Pawn speaker, float speed = 1.0f, int volume = 100)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!SocialInteractions.Settings.enableTTS || SocialInteractions.Settings.ttsMuted) return;

            string voiceName = "alloy";
            if (speaker != null && Current.Game != null)
            {
                var manager = Current.Game.GetComponent<VoiceAssignmentManager>();
                if (manager != null)
                {
                    voiceName = manager.GetOrAssignVoice(speaker);
                }
            }

            // In the new system, we only use External API
            int requestId;
            lock (bufferLock)
            {
                requestId = nextRequestId++;
            }
            SpeakWithApi(text, voiceName, requestId);
        }

        public static void Stop()
        {
            // Clear all queues to stop future playback
            lock (bufferLock)
            {
                ttsQueue.Clear();
                playbackBuffer.Clear();
                // Fast-forward playback ID to ignore any in-flight requests that might arrive later
                nextPlaybackId = nextRequestId;
                isPlaying = false;
            }
            
            // Stop current audio
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }
        }

        public static List<string> GetVoices()
        {
             return VoiceAssignmentManager.AvailableVoices;
        }

        public static void FetchVoicesFromApi()
        {
            if (string.IsNullOrEmpty(SocialInteractions.Settings.ttsApiUrl)) return;
            
            // Try to deduce the voices endpoint
            string speechUrl = SocialInteractions.Settings.ttsApiUrl;
            string voicesUrl = speechUrl;

            if (SocialInteractions.Settings.ttsApiType == TtsApiType.Player2)
            {
                if (speechUrl.Contains("/speak"))
                {
                    voicesUrl = speechUrl.Replace("/speak", "/voices");
                }
                else
                {
                    // If user only provided the base URL (e.g. http://127.0.0.1:4315/v1/tts)
                    voicesUrl = speechUrl.TrimEnd('/') + "/voices";
                }
            }
            else
            {
                voicesUrl = speechUrl.Replace("/speech", "/voices"); // Simple heuristic for OpenAI-like
                if (voicesUrl == speechUrl) voicesUrl = speechUrl + ((speechUrl.EndsWith("/")) ? "" : "/") + "v1/audio/voices"; // Fallback
            }

            SLog.Message("[SocialInteractions] TTSManager: Fetching voices from: " + voicesUrl);

            LongEventHandler.ExecuteWhenFinished(() =>
            {
                ((MonoBehaviour)Current.Root).StartCoroutine(FetchVoicesCoroutine(voicesUrl));
            });
        }

        private static IEnumerator FetchVoicesCoroutine(string url)
        {
             string apiKey = SocialInteractions.Settings.ttsApiKey;
             var request = UnityWebRequest.Get(url);
             if (!string.IsNullOrEmpty(apiKey))
             {
                 request.SetRequestHeader("Authorization", "Bearer " + apiKey);
             }

             yield return request.SendWebRequest();

             if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
             {
                 SLog.Error("[SocialInteractions] TTSManager: Failed to fetch voices: " + request.error);
             }
             else
             {
                 string json = request.downloadHandler.text;
                 SLog.Message("[SocialInteractions] TTSManager: Voices response: " + json);

                 List<string> voices = new List<string>();

                 try
                 {
                     // Simple parsing without JsonUtility dependency issues
                     // Look for "voices":[ ... ] block (for OpenAI-compatible APIs)
                     int voicesStart = json.IndexOf("\"voices\"");
                     if (voicesStart != -1)
                     {
                         int arrayStart = json.IndexOf('[', voicesStart);
                         int arrayEnd = json.IndexOf(']', arrayStart);
                         if (arrayStart != -1 && arrayEnd != -1)
                         {
                             string arrayContent = json.Substring(arrayStart, arrayEnd - arrayStart + 1);
                             ParseVoiceArray(arrayContent, ref voices);
                         }
                     }
                     else
                     {
                         // If "voices" not found, try parsing as direct array [ "voice1", "voice2", ... ]
                         int arrayStart = json.IndexOf('[');
                         int arrayEnd = json.LastIndexOf(']');
                         if (arrayStart != -1 && arrayEnd != -1 && arrayEnd > arrayStart)
                         {
                             string arrayContent = json.Substring(arrayStart, arrayEnd - arrayStart + 1);
                             ParseVoiceArray(arrayContent, ref voices);
                         }
                     }
                 }
                 catch (Exception ex)
                 {
                     SLog.Warning("[SocialInteractions] TTSManager: Parsing failed: " + ex.Message);
                 }

                 if (voices.Count > 0)
                 {
                     SLog.Message(string.Format("[SocialInteractions] TTSManager: Found {0} voices.", voices.Count));
                     VoiceAssignmentManager.SetAvailableVoices(voices);
                 }
                 else
                 {
                     SLog.Warning("[SocialInteractions] TTSManager: No voices found in response.");
                 }
             }
        }

        private static void ParseVoiceArray(string arrayContent, ref List<string> voices)
        {
            // For Player2, we need to extract objects with "id" and "name"
            if (SocialInteractions.Settings.ttsApiType == TtsApiType.Player2)
            {
                // Simple object parsing: find each { ... } block
                var objectMatches = System.Text.RegularExpressions.Regex.Matches(arrayContent, @"\{([^{}]+)\}");
                foreach (System.Text.RegularExpressions.Match objMatch in objectMatches)
                {
                    string objContent = objMatch.Groups[1].Value;
                    
                    // Extract id, name, and gender using regex
                    var idMatch = System.Text.RegularExpressions.Regex.Match(objContent, "\"id\"\\s*:\\s*\"([a-zA-Z0-9.-]+)\"");
                    var nameMatch = System.Text.RegularExpressions.Regex.Match(objContent, "\"name\"\\s*:\\s*\"([a-zA-Z0-9.\\s]+)\"");
                    var langMatch = System.Text.RegularExpressions.Regex.Match(objContent, "\"language\"\\s*:\\s*\"([a-zA-Z0-9._-]+)\"");
                    var genderMatch = System.Text.RegularExpressions.Regex.Match(objContent, "\"gender\"\\s*:\\s*\"([a-zA-Z]+)\"");

                    if (idMatch.Success && nameMatch.Success)
                    {
                        string id = idMatch.Groups[1].Value;
                        string name = nameMatch.Groups[1].Value;
                        string lang = langMatch.Success ? langMatch.Groups[1].Value : "";
                        string gender = genderMatch.Success ? genderMatch.Groups[1].Value.ToLower() : "";
                        
                        // Prefix name with [Male] or [Female] if found
                        string prefix = "";
                        if (gender == "male") prefix = "[Male] ";
                        else if (gender == "female") prefix = "[Female] ";
                        
                        string displayName = prefix + name;
                        if (!string.IsNullOrEmpty(lang)) displayName += string.Format(" ({0})", lang);
                        
                        if (!voices.Contains(displayName)) voices.Add(displayName);
                        voiceIdLookup[displayName] = id;
                    }
                }
            }
            else
            {
                // Standard flat array parsing or simple regex for OpenAI-like
                var matches = System.Text.RegularExpressions.Regex.Matches(arrayContent, "\"([a-zA-Z0-9_.-]+)\"");
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    string v = match.Groups[1].Value;
                    // Skip keys if they were caught by accident
                    if (v == "voices" || v == "id" || v == "name") continue;

                    if (!voices.Contains(v)) voices.Add(v);
                }
            }
        }

        private static void SpeakWithApi(string text, string voiceName, int requestId)
        {
            if (string.IsNullOrEmpty(SocialInteractions.Settings.ttsApiUrl))
            {
                SLog.Warning("[SocialInteractions] TTSManager: TTS API URL is empty.");
                // Mark request as failed/done to prevent blocking
                ProcessPlaybackBuffer(requestId, null, 0); 
                return;
            }

            // Directly start coroutine as we are on the main thread
            if (Current.Root != null)
            {
                ((MonoBehaviour)Current.Root).StartCoroutine(FetchAndPlayAudio(text, voiceName, requestId));
            }
            else
            {
                SLog.Warning("[SocialInteractions] TTSManager: Verify Current.Root is not null.");
                ProcessPlaybackBuffer(requestId, null, 0);
            }
        }

        private static IEnumerator FetchAndPlayAudio(string text, string voiceName, int requestId)
        {
            // Yield once to ensure we don't choke the frame if batching calls
            yield return null;

            AudioClip clip = null;
            bool success = false;

            string url = SocialInteractions.Settings.ttsApiUrl;
            string apiKey = SocialInteractions.Settings.ttsApiKey;
            string model = SocialInteractions.Settings.ttsModel;
            string voice = !string.IsNullOrEmpty(voiceName) ? voiceName : "alloy";
            float speed = Mathf.Clamp(SocialInteractions.Settings.ttsSpeed, 0.25f, 4.0f);

            string json = "";
            if (SocialInteractions.Settings.ttsApiType == TtsApiType.Player2)
            {
                // Player2 format: {"text": "...", "voice_ids": ["..."], "speed": 1.0, "audio_format": "wav", "play_in_app": false}
                // Note: using play_in_app: false to routing audio back to mod.
                string internalPlaybackStr = SocialInteractions.Settings.ttsInternalPlayback ? "true" : "false";
                
                // Lookup UUID if possible, otherwise use name as fallback
                string voiceId = voice;
                string foundId;
                if (voiceIdLookup.TryGetValue(voice, out foundId))
                {
                    voiceId = foundId;
                }

                json = string.Format("{{\"text\": \"{0}\", \"voice_ids\": [\"{1}\"], \"speed\": {2}, \"audio_format\": \"wav\", \"internal_playback\": {3}, \"play_in_app\": false}}",
                    text.Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", ""),
                    voiceId,
                    speed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    internalPlaybackStr);
            }
            else
            {
                // OpenAI compatible format (default)
                json = string.Format("{{\"model\": \"{0}\", \"input\": \"{1}\", \"voice\": \"{2}\", \"speed\": {3}}}", 
                    model, 
                    text.Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", ""), 
                    voice, 
                    speed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            }

            // Define formats to try (for standard binary APIs)
            var formats = new[] 
            { 
                new { Type = AudioType.WAV, Name = "WAV" },
                new { Type = AudioType.MPEG, Name = "MPEG" },
                new { Type = AudioType.OGGVORBIS, Name = "OGG" }
            };

            if (SocialInteractions.Settings.ttsApiType == TtsApiType.Player2)
            {
                // Player2 returns a JSON wrapper with base64 data
                using (UnityWebRequest request = UnityWebRequest.Put(url, json))
                {
                    request.method = "POST";
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.downloadHandler = new DownloadHandlerBuffer();
                    if (!string.IsNullOrEmpty(apiKey)) request.SetRequestHeader("Authorization", "Bearer " + apiKey);

                    yield return request.SendWebRequest();

                    if (request.result != UnityWebRequest.Result.ConnectionError && request.result != UnityWebRequest.Result.ProtocolError)
                    {
                        string responseText = request.downloadHandler.text;
                        if (!string.IsNullOrEmpty(responseText) && responseText.TrimStart().StartsWith("{"))
                        {
                            // Extract "data":"..."
                            int dataIdx = responseText.IndexOf("\"data\":");
                            if (dataIdx != -1)
                            {
                                int startQuote = responseText.IndexOf('"', dataIdx + 7);
                                int endQuote = responseText.LastIndexOf('"');
                                if (startQuote != -1 && endQuote > startQuote)
                                {
                                    string base64Data = responseText.Substring(startQuote + 1, endQuote - startQuote - 1);
                                    string tempPathStr = "";
                                    try
                                    {
                                        byte[] audioBytes = Convert.FromBase64String(base64Data);
                                        tempPathStr = Path.Combine(Path.GetTempPath(), "si_tts_" + requestId + ".wav");
                                        File.WriteAllBytes(tempPathStr, audioBytes);
                                    }
                                    catch (Exception ex)
                                    {
                                        SLog.Warning("[SocialInteractions] Player2 JSON/Base64 decode failed: " + ex.Message);
                                    }

                                    if (!string.IsNullOrEmpty(tempPathStr))
                                    {
                                        // Load from local file
                                        using (UnityWebRequest localReq = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPathStr, AudioType.WAV))
                                        {
                                            yield return localReq.SendWebRequest();
                                            if (localReq.result != UnityWebRequest.Result.ConnectionError && localReq.result != UnityWebRequest.Result.ProtocolError)
                                            {
                                                clip = DownloadHandlerAudioClip.GetContent(localReq);
                                                if (clip != null && clip.frequency > 0) success = true;
                                            }
                                        }

                                        // Try to delete temp file
                                        try { if (File.Exists(tempPathStr)) File.Delete(tempPathStr); } catch {}
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        SLog.Warning(string.Format("[SocialInteractions] TTSManager: Player2 API Error {0}: {1}", request.responseCode, request.error));
                    }
                }
            }
            else
            {
                // Standard binary API handling (OpenAI-like)
                foreach (var format in formats)
                {
                    using (UnityWebRequest request = UnityWebRequest.Put(url, json))
                    {
                        request.method = "POST";
                        request.SetRequestHeader("Content-Type", "application/json");
                        request.downloadHandler = new DownloadHandlerAudioClip(url, format.Type);
                        if (!string.IsNullOrEmpty(apiKey)) request.SetRequestHeader("Authorization", "Bearer " + apiKey);

                        yield return request.SendWebRequest();

                        if (request.result != UnityWebRequest.Result.ConnectionError && request.result != UnityWebRequest.Result.ProtocolError)
                        {
                            clip = DownloadHandlerAudioClip.GetContent(request);
                            if (clip != null && clip.frequency > 0)
                            {
                                success = true;
                                break; // Success!
                            }
                        }
                        else
                        {
                            if (request.result == UnityWebRequest.Result.ConnectionError) break;
                            if (request.responseCode == 401 || request.responseCode == 403 || request.responseCode == 404) break; 
                        }
                    }
                }
            }

            if (success)
            {
                // We pass 1.0f here because ManagePlaybackQueue applies the settings volume dynamically
                ProcessPlaybackBuffer(requestId, clip, 1.0f);
                if (Current.Game != null && Current.Root != null)
                {
                    ((MonoBehaviour)Current.Root).StartCoroutine(ManagePlaybackQueue());
                }
            }
            else
            {
                if (clip == null)
                {
                    SLog.Warning("[SocialInteractions] TTSManager: Failed to fetch TTS for request " + requestId);
                }
                ProcessPlaybackBuffer(requestId, null, 0);
            }
        }

        // Queue entry for TTS playback
        private class TTSQueueEntry
        {
            public AudioClip clip;
            public float volume;

            public TTSQueueEntry(AudioClip clip, float volume)
            {
                this.clip = clip;
                this.volume = volume;
            }
        }

        private static void AddToPlaybackQueue(AudioClip clip, float volume)
        {
            ttsQueue.Enqueue(new TTSQueueEntry(clip, volume));
        }

        private static void ProcessPlaybackBuffer(int requestId, AudioClip clip, float volume)
        {
            lock (bufferLock)
            {
                // SLog.Message(string.Format("[TTS Debug] ProcessPlaybackBuffer: Recvd ID {0}. Waiting for {1}. Buffer Size: {2}", requestId, nextPlaybackId, playbackBuffer.Count));

                // Add to buffer (if successful) or just mark as ready-to-skip (if null)
                // We use a dummy entry with null clip for failed items to keep sequence moving
                playbackBuffer[requestId] = new TTSQueueEntry(clip, volume);

                // Try to move items from buffer to queue in order
                while (playbackBuffer.ContainsKey(nextPlaybackId))
                {
                    // SLog.Message(string.Format("[TTS Debug] ProcessPlaybackBuffer: Promoting ID {0} to queue.", nextPlaybackId));
                    var entry = playbackBuffer[nextPlaybackId];
                    if (entry.clip != null) // Only valid clips
                    {
                        AddToPlaybackQueue(entry.clip, entry.volume);
                        
                        // Start the playback manager coroutine if not already running
                        if (Current.Game != null && Current.Root != null)
                        {
                            ((MonoBehaviour)Current.Root).StartCoroutine(ManagePlaybackQueue());
                        }
                    }
                    else
                    {
                         // SLog.Message(string.Format("[TTS Debug] ProcessPlaybackBuffer: ID {0} has null clip, skipping.", nextPlaybackId));
                    }
                    
                    playbackBuffer.Remove(nextPlaybackId);
                    nextPlaybackId++;
                }
            }
        }

        private static IEnumerator ManagePlaybackQueue()
        {
            // Prevent multiple queue managers from running
            if (isPlaying)
            {
                yield break;
            }

            isPlaying = true;

            while (ttsQueue.Count > 0)
            {
                TTSQueueEntry entry = ttsQueue.Dequeue();

                // Ensure audio source is created and configured properly
                if (audioSource == null)
                {
                    GameObject go = new GameObject("SocialInteractions_TTS_Audio");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    audioSource = go.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false; // Don't play automatically
                    audioSource.spatialBlend = 0f; // 2D sound (0 = fully 2D, 1 = fully 3D)
                }

                // Ensure audio source is configured properly
                audioSource.spatialBlend = 0f; // Ensure 2D sound
                audioSource.rolloffMode = AudioRolloffMode.Logarithmic; // Set rolloff mode
                audioSource.maxDistance = 500f; // Set a reasonable max distance
                audioSource.minDistance = 1f; // Set min distance
                audioSource.ignoreListenerVolume = true; // Make TTS independent of game's master volume slider

                audioSource.clip = entry.clip;
                // entry.volume is now 1.0f by default from ProcessPlaybackBuffer
                // We apply the settings volume here so it reacts to slider changes in real-time
                audioSource.volume = entry.volume * (SocialInteractions.Settings.ttsVolume / 100f);
                audioSource.Play();
                
                // SLog.Message("[TTS Debug] Started playback of clip. Duration: " + entry.clip.length);

                // Wait for the clip to finish playing (clip.length is in seconds)
                // We'll wait for the full duration of the clip
                float clipDuration = entry.clip.length;
                float waitTime = 0f;
                // Use unscaled time to ensure it works even if game is paused
                while (waitTime < clipDuration && audioSource.isPlaying)
                {
                    yield return null; // Wait one frame
                    waitTime += Time.unscaledDeltaTime;
                }

                // Add a small pause between clips
                yield return new WaitForSecondsRealtime(0.1f);
            }

            isPlaying = false;
        }
    }
}
