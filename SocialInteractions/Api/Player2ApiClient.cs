using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Collections.Generic;

namespace SocialInteractions
{
    [DataContract]
    public class Player2ApiMessage
    {
        [DataMember(Name = "role")]
        public string Role { get; set; }
        [DataMember(Name = "content")]
        public string Content { get; set; }
    }

    [DataContract]
    public class Player2ApiRequest
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "messages")]
        public List<Player2ApiMessage> Messages { get; set; }
        [DataMember(Name = "temperature")]
        public float Temperature { get; set; }
        [DataMember(Name = "max_tokens")]
        public int? MaxTokens { get; set; }
        [DataMember(Name = "stream")]
        public bool Stream { get; set; }
        [DataMember(Name = "stop")]
        public List<string> Stop { get; set; }
        
        [DataMember(Name = "top_p", EmitDefaultValue = false)]
        public float? TopP { get; set; }
        [DataMember(Name = "top_k", EmitDefaultValue = false)]
        public int? TopK { get; set; }
        [DataMember(Name = "min_p", EmitDefaultValue = false)]
        public float? MinP { get; set; }
        [DataMember(Name = "repetition_penalty", EmitDefaultValue = false)]
        public float? RepetitionPenalty { get; set; }

        public Player2ApiRequest()
        {
            Stream = false;
            Messages = new List<Player2ApiMessage>();
        }
    }

    [DataContract]
    public class Player2ApiChoice
    {
        [DataMember(Name = "index")]
        public int Index { get; set; }
        [DataMember(Name = "message")]
        public Player2ApiMessage Message { get; set; }
        [DataMember(Name = "finish_reason")]
        public string FinishReason { get; set; }
    }

    [DataContract]
    public class Player2ApiResponse
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }
        [DataMember(Name = "object")]
        public string Object { get; set; }
        [DataMember(Name = "created")]
        public long Created { get; set; }
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "choices")]
        public Player2ApiChoice[] Choices { get; set; }
        [DataMember(Name = "usage")]
        public object Usage { get; set; }
    }

    public class Player2ApiClient : IDisposable
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient();
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _modelName;
        private readonly string _apiKey;
        private readonly string _gameClientId;
        private bool _disposed = false;

        // Health heartbeat timer for Player2 usage tracking
        private static Timer _healthTimer;
        private static string _healthBaseUrl;
        private static string _healthGameClientId;

        public Player2ApiClient(string apiUrl, string modelName, string apiKey, string gameClientId = null)
        {
            _apiUrl = apiUrl;
            _modelName = modelName;
            _apiKey = (apiKey != null) ? apiKey.Trim() : null;
            _gameClientId = (gameClientId != null) ? gameClientId.Trim() : null;
            _httpClient = SharedHttpClient;
        }

        /// <summary>
        /// Starts a background health heartbeat that pings the Player2 /health endpoint every 60 seconds.
        /// This is required for proper usage tracking and revenue share attribution.
        /// </summary>
        public static void StartHealthHeartbeat(string baseUrl, string gameClientId)
        {
            StopHealthHeartbeat(); // Stop any existing heartbeat first

            if (string.IsNullOrEmpty(baseUrl))
            {
                SLog.Warning("[SocialInteractions] Cannot start Player2 health heartbeat: no base URL configured.");
                return;
            }

            _healthBaseUrl = baseUrl.TrimEnd('/');
            _healthGameClientId = (gameClientId != null) ? gameClientId.Trim() : null;

            // Send initial health ping immediately, then every 60 seconds
            _healthTimer = new Timer(HealthHeartbeatCallback, null, 0, 60000);
            SLog.Message("[SocialInteractions] Player2 health heartbeat started.");
        }

        /// <summary>
        /// Stops the health heartbeat timer.
        /// </summary>
        public static void StopHealthHeartbeat()
        {
            if (_healthTimer != null)
            {
                _healthTimer.Dispose();
                _healthTimer = null;
                SLog.Message("[SocialInteractions] Player2 health heartbeat stopped.");
            }
        }

        private static async void HealthHeartbeatCallback(object state)
        {
            try
            {
                string healthUrl = _healthBaseUrl + "/v1/health";
                
                using (var request = new HttpRequestMessage(HttpMethod.Get, healthUrl))
                {
                    if (!string.IsNullOrEmpty(_healthGameClientId))
                    {
                        request.Headers.Add("player2-game-key", _healthGameClientId);
                    }
                    request.Headers.Add("User-Agent", "SocialInteractionsMod/1.0");

                    var response = await SharedHttpClient.SendAsync(request);
                    SLog.Message(string.Format("[SocialInteractions] Player2 health heartbeat: {0}", response.StatusCode));
                }
            }
            catch (Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] Player2 health heartbeat failed: {0}", ex.Message));
            }
        }

        private bool IsValidHeaderValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            foreach (char c in value)
            {
                if (char.IsControl(c))
                    return false;
            }

            return true;
        }

        public async Task<string> GenerateText(string prompt, int? maxLength = null, float? temperature = null, List<string> stopSequence = null, bool? enableXtcSampling = null, int? topK = null, float? topP = null, float? minP = null, float? repetitionPenalty = null)
        {
            if (_disposed)
                throw new ObjectDisposedException("Player2ApiClient");

            try
            {
                var request = new Player2ApiRequest
                {
                    Model = _modelName,
                    Temperature = temperature ?? SocialInteractions.Settings.llmTemperature,
                    MaxTokens = maxLength ?? SocialInteractions.Settings.llmMaxTokens,
                    Stream = false,
                    Stop = stopSequence ?? new List<string>(SocialInteractions.Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)),
                    
                    TopK = topK ?? (SocialInteractions.Settings.llmTopK > 0 ? (int?)SocialInteractions.Settings.llmTopK : null),
                    TopP = topP ?? (SocialInteractions.Settings.llmTopP < 1.0f ? (float?)SocialInteractions.Settings.llmTopP : null),
                    MinP = minP ?? (SocialInteractions.Settings.llmMinP > 0.0f ? (float?)SocialInteractions.Settings.llmMinP : null),
                    RepetitionPenalty = repetitionPenalty ?? (SocialInteractions.Settings.llmRepetitionPenalty != 1.0f ? (float?)SocialInteractions.Settings.llmRepetitionPenalty : null)
                };

                request.Messages.Add(new Player2ApiMessage
                {
                    Role = "system",
                    Content = "You are generating dialogue for characters in a story. Respond with only the dialogue lines, without any thinking, reasoning, or meta-commentary. Do not include tags like <thinking> or explanations."
                });

                request.Messages.Add(new Player2ApiMessage
                {
                    Role = "user",
                    Content = prompt
                });

                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Player2ApiRequest));
                MemoryStream stream = new MemoryStream();
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    serializer.WriteObject(writer.BaseStream, request);
                    writer.Flush();
                }
                string jsonContent = Encoding.UTF8.GetString(stream.ToArray());
                
                // Sanitize the JSON content to ensure it's clean for the server
                jsonContent = SanitizeJsonString(jsonContent);

                SLog.Message(string.Format("[SocialInteractions] Player2 API Request: {0}", jsonContent));

                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                string fullUrl = _apiUrl.TrimEnd('/');
                if (!fullUrl.EndsWith("/v1/chat/completions"))
                {
                    if (!fullUrl.EndsWith("/v1"))
                    {
                        fullUrl = fullUrl + "/v1";
                    }
                    fullUrl = fullUrl + "/chat/completions";
                }
                
                using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, fullUrl))
                {
                    httpRequestMessage.Content = httpContent;
                    httpRequestMessage.Headers.Add("User-Agent", "SocialInteractionsMod/1.0");
                    
                    if (!string.IsNullOrEmpty(_apiKey))
                    {
                        httpRequestMessage.Headers.Add("Authorization", string.Format("Bearer {0}", _apiKey));
                    }
                    if (!string.IsNullOrEmpty(_gameClientId))
                    {
                        httpRequestMessage.Headers.Add("player2-game-key", _gameClientId);
                    }

                    var response = await _httpClient.SendAsync(httpRequestMessage);
                    var responseBody = await response.Content.ReadAsStringAsync();
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] Player2 API Error (Status {0}): {1}", response.StatusCode, responseBody));
                        return null;
                    }

                    SLog.Message(string.Format("[SocialInteractions] Player2 API Response Body: {0}", responseBody));

                    DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(Player2ApiResponse));
                    MemoryStream responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody));
                    Player2ApiResponse apiResponse = (Player2ApiResponse)deserializer.ReadObject(responseStream);

                    if (apiResponse != null && apiResponse.Choices != null && apiResponse.Choices.Length > 0)
                    {
                        return CleanChatResponse(apiResponse.Choices[0].Message.Content);
                    }
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] Player2ApiClient: HTTP request failed: {0}", ex.Message));
                return null;
            }
            catch (Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] Player2ApiClient: Unexpected error during text generation: {0}", ex.Message));
                return null;
            }
        }

        private string SanitizeJsonString(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;
            
            // Remove some common "smart" characters that often get mangled or cause 400s
            json = json.Replace("\u201c", "\"").Replace("\u201d", "\""); // Smart quotes
            json = json.Replace("\u2018", "'").Replace("\u2019", "'"); // Smart single quotes
            json = json.Replace("\u2013", "-").Replace("\u2014", "-"); // En/Em dashes
            json = json.Replace("\u2026", "..."); // Ellipsis
            
            // Further sanitize to ensure only printable ASCII + common whitespace
            // This is a bit aggressive but helps with sensitive local servers
            // StringBuilder sb = new StringBuilder();
            // foreach (char c in json)
            // {
            //     if (c < 128)
            //     {
            //         sb.Append(c);
            //     }
            //     else
            //     {
            //         // For non-ASCII, use a space or skip to avoid mangling the JSON structure
            //         sb.Append(' ');
            //     }
            // }
            //return sb.ToString();
            return json;
        }

        private string CleanChatResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return response;

            response = System.Text.RegularExpressions.Regex.Replace(response, @"<thinking>.*?</thinking>", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            response = System.Text.RegularExpressions.Regex.Replace(response, @"<think>.*?</think>", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            response = System.Text.RegularExpressions.Regex.Replace(response, @"\[thinking\].*?\[/thinking\]", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            response = response.Trim();
            
            return response;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
