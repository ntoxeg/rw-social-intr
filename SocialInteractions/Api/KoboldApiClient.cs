using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Collections.Generic;

namespace SocialInteractions
{
// Text completion classes (Legacy)
    [DataContract]
    public class KoboldApiRequest
    {
        [DataMember(Name = "prompt")]
        public string Prompt { get; set; }
        [DataMember(Name = "max_length")]
        public int MaxLength { get; set; }
        [DataMember(Name = "temperature")]
        public float Temperature { get; set; }
        [DataMember(Name = "stop_sequence")]
        public List<string> StopSequence { get; set; }
        [DataMember(Name = "sampler_order")]
        public int[] SamplerOrder { get; set; }
        [DataMember(Name = "xtc_probability", EmitDefaultValue = false)]
        public float XtcProbability { get; set; }
        [DataMember(Name = "xtc_threshold", EmitDefaultValue = false)]
        public float XtcThreshold { get; set; }
        [DataMember(Name = "top_k", EmitDefaultValue = false)]
        public int TopK { get; set; }
        [DataMember(Name = "top_p", EmitDefaultValue = false)]
        public float TopP { get; set; }
        [DataMember(Name = "min_p", EmitDefaultValue = false)]
        public float MinP { get; set; }
        [DataMember(Name = "rep_pen", EmitDefaultValue = false)]
        public float? RepetitionPenalty { get; set; }

        public KoboldApiRequest()
        {
            MaxLength = 200;
            Temperature = 0.7f;
        }
    }

    [DataContract]
    public class KoboldApiResponse
    {
        [DataMember(Name = "results")]
        public KoboldApiResult[] Results { get; set; }
    }

    [DataContract]
    public class KoboldApiResult
    {
        [DataMember(Name = "text")]
        public string Text { get; set; }
    }

    // Chat completion classes (OpenAI compatible)
    [DataContract]
    public class KoboldChatMessage
    {
        [DataMember(Name = "role")]
        public string Role { get; set; }
        [DataMember(Name = "content")]
        public string Content { get; set; }
    }

    [DataContract]
    public class KoboldChatRequest
    {
        [DataMember(Name = "messages")]
        public List<KoboldChatMessage> Messages { get; set; }
        [DataMember(Name = "max_tokens")]
        public int MaxTokens { get; set; }
        [DataMember(Name = "temperature")]
        public float Temperature { get; set; }
        [DataMember(Name = "stop")]
        public List<string> Stop { get; set; }
        [DataMember(Name = "top_k", EmitDefaultValue = false)]
        public int? TopK { get; set; }
        [DataMember(Name = "top_p", EmitDefaultValue = false)]
        public float? TopP { get; set; }
        [DataMember(Name = "min_p", EmitDefaultValue = false)]
        public float? MinP { get; set; }
        [DataMember(Name = "repetition_penalty", EmitDefaultValue = false)]
        public float? RepetitionPenalty { get; set; }

        public KoboldChatRequest()
        {
            Messages = new List<KoboldChatMessage>();
        }
    }

    [DataContract]
    public class KoboldChatResponse
    {
        [DataMember(Name = "choices")]
        public KoboldChatChoice[] Choices { get; set; }
    }

    [DataContract]
    public class KoboldChatChoice
    {
        [DataMember(Name = "message")]
        public KoboldChatMessage Message { get; set; }
    }

    public class KoboldApiClient : IDisposable
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient();
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _apiKey;
        private bool _disposed = false;

        public KoboldApiClient(string apiUrl, string apiKey)
        {
            _apiUrl = apiUrl;
            // Trim whitespace which can cause header issues
            _apiKey = (apiKey != null) ? apiKey.Trim() : null;
            _httpClient = SharedHttpClient;
            
            // Add default request headers if needed, e.g., for API key
            if (!string.IsNullOrEmpty(_apiKey))
            {
                try
                {
                    // Validate that the API key doesn't contain invalid characters
                    if (IsValidHeaderValue(_apiKey))
                    {
                        _httpClient.DefaultRequestHeaders.Add("Authorization", string.Format("Bearer {0}", _apiKey));
                    }
                    else
                    {
                        // --- Enhanced Logging ---
                        //SLog.Warning(string.Format("[SocialInteractions] Invalid API key format after trimming, skipping Authorization header. Key length: {0}, Key (first 10 chars): '{1}'", _apiKey.Length, _apiKey.Length > 0 ? _apiKey.Substring(0, System.Math.Min(10, _apiKey.Length)) : ""));
                        // --- End Enhanced Logging ---
                    }
                }
                catch (Exception /*ex*/)
                {
                    // --- Enhanced Logging ---
                    int apiKeyLength = (_apiKey != null) ? _apiKey.Length : 0;
                    string apiKeyPreview = "";
                    if (_apiKey != null && _apiKey.Length > 0)
                    {
                        apiKeyPreview = _apiKey.Substring(0, System.Math.Min(10, _apiKey.Length));
                    }
                    //SLog.Warning(string.Format("[SocialInteractions] Failed to add Authorization header. API Key Length: {0}, API Key Preview (first 10 chars): '{1}'. Error: {2}", apiKeyLength, apiKeyPreview, ex.Message));
                    // --- End Enhanced Logging ---
                }
            }
            else
            {
                // Log if key is null/empty, might be intentional
                // SLog.Message("[SocialInteractions] KoboldApiClient constructed with null or empty API key. Authorization header will not be added.");
            }
        }

        private bool IsValidHeaderValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            
            // Check for control characters and other invalid characters
            foreach (char c in value)
            {
                if (char.IsControl(c) || c == '\r' || c == '\n' || c == '\t')
                {
                    return false;
                }
            }
            return true;
        }

        public async Task<string> GenerateText(string prompt, int? maxLength = null, float? temperature = null, List<string> stopSequence = null, bool? enableXtcSampling = null, int? topK = null, float? topP = null, float? minP = null, float? repetitionPenalty = null)
        {
            if (_disposed)
                throw new ObjectDisposedException("KoboldApiClient");

            try
            {
                if (SocialInteractions.Settings.forceChatCompletion)
                {
                    return await GenerateChatText(prompt, maxLength, temperature, stopSequence, topK, topP, minP, repetitionPenalty);
                }

                var request = new KoboldApiRequest
                {
                    Prompt = prompt,
                    MaxLength = maxLength ?? SocialInteractions.Settings.llmMaxTokens,
                    Temperature = temperature ?? SocialInteractions.Settings.llmTemperature,
                    StopSequence = stopSequence ?? new List<string>(SocialInteractions.Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)),
                    TopK = topK ?? SocialInteractions.Settings.llmTopK,
                    TopP = topP ?? SocialInteractions.Settings.llmTopP,
                    MinP = minP ?? SocialInteractions.Settings.llmMinP,
                    RepetitionPenalty = repetitionPenalty ?? (SocialInteractions.Settings.llmRepetitionPenalty != 1.0f ? (float?)SocialInteractions.Settings.llmRepetitionPenalty : null)
                };

                if (enableXtcSampling ?? SocialInteractions.Settings.enableXtcSampling)
                {
                    request.SamplerOrder = new int[] { 6,0,1,3,4,2,5 };
                    request.XtcProbability = 0.5f;
                    request.XtcThreshold = 0.1f;
                }
                else
                {
                    request.SamplerOrder = new int[] { 6,0,1,3,4,2,5 };
                }

                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(KoboldApiRequest));
                MemoryStream stream = new MemoryStream();
                serializer.WriteObject(stream, request);
                stream.Position = 0;
                StreamReader reader = new StreamReader(stream);
                string jsonContent = reader.ReadToEnd();

                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_apiUrl + "/api/v1/generate", httpContent);
                response.EnsureSuccessStatusCode(); // Throws an exception if the HTTP response status is an error code

                var responseBody = await response.Content.ReadAsStringAsync();

                DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(KoboldApiResponse));
                MemoryStream responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody));
                KoboldApiResponse apiResponse = (KoboldApiResponse)deserializer.ReadObject(responseStream);

                if (apiResponse != null && apiResponse.Results != null && apiResponse.Results.Length > 0)
                {
                    // Log the API request and response
                    // SLog.Message(string.Format("[SocialInteractions] LLM API Request: {0}", prompt));
                    // SLog.Message(string.Format("[SocialInteractions] LLM API Response: {0}", apiResponse.Results[0].Text));
                    
                    return CleanChatResponse(apiResponse.Results[0].Text);
                }
                return null;
            }
            catch (HttpRequestException ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] KoboldApiClient: HTTP request failed: {0}", ex.Message));
                return null;
            }
            catch (Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] KoboldApiClient: Unexpected error during text generation: {0}", ex.Message));
                return null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private async Task<string> GenerateChatText(string prompt, int? maxLength = null, float? temperature = null, List<string> stopSequence = null, int? topK = null, float? topP = null, float? minP = null, float? repetitionPenalty = null)
        {
            var request = new KoboldChatRequest
            {
                MaxTokens = maxLength ?? SocialInteractions.Settings.llmMaxTokens,
                Temperature = temperature ?? SocialInteractions.Settings.llmTemperature,
                Stop = stopSequence ?? new List<string>(SocialInteractions.Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)),
                TopK = topK ?? (SocialInteractions.Settings.llmTopK > 0 ? (int?)SocialInteractions.Settings.llmTopK : null),
                TopP = topP ?? (SocialInteractions.Settings.llmTopP < 1.0f ? (float?)SocialInteractions.Settings.llmTopP : null),
                MinP = minP ?? (SocialInteractions.Settings.llmMinP > 0.0f ? (float?)SocialInteractions.Settings.llmMinP : null),
                RepetitionPenalty = repetitionPenalty ?? (SocialInteractions.Settings.llmRepetitionPenalty != 1.0f ? (float?)SocialInteractions.Settings.llmRepetitionPenalty : null)
            };

            request.Messages.Add(new KoboldChatMessage { Role = "system", Content = "You are generating dialogue for characters in a story. Respond with only the dialogue lines, without any thinking, reasoning, or meta-commentary." });
            request.Messages.Add(new KoboldChatMessage { Role = "user", Content = prompt });

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(KoboldChatRequest));
            MemoryStream stream = new MemoryStream();
            serializer.WriteObject(stream, request);
            stream.Position = 0;
            StreamReader reader = new StreamReader(stream);
            string jsonContent = reader.ReadToEnd();

            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_apiUrl.TrimEnd('/') + "/v1/chat/completions", httpContent);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(KoboldChatResponse));
            using (var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody)))
            {
                var apiResponse = (KoboldChatResponse)deserializer.ReadObject(responseStream);
                if (apiResponse != null && apiResponse.Choices != null && apiResponse.Choices.Length > 0)
                {
                    return CleanChatResponse(apiResponse.Choices[0].Message.Content);
                }
            }
            return null;
        }

        private string CleanChatResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return response;

            // Remove thinking blocks with various tag formats
            response = System.Text.RegularExpressions.Regex.Replace(response, @"<thinking>.*?</thinking>", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            response = System.Text.RegularExpressions.Regex.Replace(response, @"<think>.*?</think>", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            response = System.Text.RegularExpressions.Regex.Replace(response, @"\[thinking\].*?\[/thinking\]", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Trim whitespace
            response = response.Trim();
            
            return response;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // Note: We don't dispose the shared HttpClient as it's shared
                // In a more sophisticated implementation, we might use HttpClientFactory
                _disposed = true;
            }
        }
    }
}
