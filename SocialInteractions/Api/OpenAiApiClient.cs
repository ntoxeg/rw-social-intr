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
    [DataContract]
    public class OpenAiApiMessage
    {
        [DataMember(Name = "role")]
        public string Role { get; set; }
        [DataMember(Name = "content")]
        public string Content { get; set; }
    }

    [DataContract]
    public class OpenAiApiRequest
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "messages")]
        public List<OpenAiApiMessage> Messages { get; set; }
        [DataMember(Name = "temperature")]
        public float Temperature { get; set; }
        [DataMember(Name = "max_tokens")]
        public int? MaxTokens { get; set; }
        [DataMember(Name = "stream")]
        public bool Stream { get; set; }
        [DataMember(Name = "stop")]
        public List<string> Stop { get; set; }
        
        // Extended sampler settings for OpenAI-compatible servers
        [DataMember(Name = "top_p", EmitDefaultValue = false)]
        public float? TopP { get; set; }
        [DataMember(Name = "top_k", EmitDefaultValue = false)]
        public int? TopK { get; set; }
        [DataMember(Name = "min_p", EmitDefaultValue = false)]
        public float? MinP { get; set; }
        [DataMember(Name = "xtc_threshold", EmitDefaultValue = false)]
        public float? XtcThreshold { get; set; }
        [DataMember(Name = "xtc_probability", EmitDefaultValue = false)]
        public float? XtcProbability { get; set; }
        [DataMember(Name = "repetition_penalty", EmitDefaultValue = false)]
        public float? RepetitionPenalty { get; set; }

        public OpenAiApiRequest()
        {
            Stream = false;
            Messages = new List<OpenAiApiMessage>();
        }
    }

    [DataContract]
    public class OpenAiApiChoice
    {
        [DataMember(Name = "index")]
        public int Index { get; set; }
        [DataMember(Name = "message")]
        public OpenAiApiMessage Message { get; set; }
        [DataMember(Name = "finish_reason")]
        public string FinishReason { get; set; }
    }

    [DataContract]
    public class OpenAiApiResponse
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
        public OpenAiApiChoice[] Choices { get; set; }
        [DataMember(Name = "usage")]
        public object Usage { get; set; } // We won't use this, but it's in the API response
    }

    public class OpenAiApiClient : IDisposable
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient();
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _modelName;
        private readonly string _apiKey;
        private bool _disposed = false;

        public OpenAiApiClient(string apiUrl, string modelName, string apiKey)
        {
            _apiUrl = apiUrl;
            _modelName = modelName;
            // Trim whitespace which can cause header issues
            _apiKey = (apiKey != null) ? apiKey.Trim() : null;
            _httpClient = SharedHttpClient;
            
            // Clear any existing default request headers
            _httpClient.DefaultRequestHeaders.Clear();
            
            // Add default request headers for OpenAI
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
                        SLog.Warning("[SocialInteractions] Invalid API key format for OpenAI, skipping Authorization header.");
                    }
                }
                catch (Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] Failed to add Authorization header for OpenAI. Error: {0}", ex.Message));
                }
            }
            
            // Add required content type header
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SocialInteractionsMod/1.0");
        }

        // Helper method to validate HTTP header values
        private bool IsValidHeaderValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            // Check for control characters
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
                throw new ObjectDisposedException("OpenAiApiClient");

            try
            {
                var request = new OpenAiApiRequest
                {
                    Model = _modelName,
                    Temperature = temperature ?? SocialInteractions.Settings.llmTemperature,
                    MaxTokens = maxLength ?? SocialInteractions.Settings.llmMaxTokens,
                    Stream = false,
                    Stop = stopSequence ?? new List<string>(SocialInteractions.Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)),
                    
                    // Populate extended sampler settings
                    TopK = topK ?? (SocialInteractions.Settings.llmTopK > 0 ? (int?)SocialInteractions.Settings.llmTopK : null),
                    TopP = topP ?? (SocialInteractions.Settings.llmTopP < 1.0f ? (float?)SocialInteractions.Settings.llmTopP : null),
                    MinP = minP ?? (SocialInteractions.Settings.llmMinP > 0.0f ? (float?)SocialInteractions.Settings.llmMinP : null),
                    RepetitionPenalty = repetitionPenalty ?? (SocialInteractions.Settings.llmRepetitionPenalty != 1.0f ? (float?)SocialInteractions.Settings.llmRepetitionPenalty : null)
                };

                // Add XTC sampling if enabled
                if (enableXtcSampling ?? SocialInteractions.Settings.enableXtcSampling)
                {
                    request.XtcProbability = 0.5f;
                    request.XtcThreshold = 0.1f;
                }

                // Add system message to guide response format
                request.Messages.Add(new OpenAiApiMessage
                {
                    Role = "system",
                    Content = "You are generating dialogue for characters in a story. Respond with only the dialogue lines, without any thinking, reasoning, or meta-commentary. Do not include tags like <thinking> or explanations."
                });

                // Add the prompt as a user message
                request.Messages.Add(new OpenAiApiMessage
                {
                    Role = "user",
                    Content = prompt
                });

                // Convert to JSON
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(OpenAiApiRequest));
                MemoryStream stream = new MemoryStream();
                serializer.WriteObject(stream, request);
                stream.Position = 0;
                StreamReader reader = new StreamReader(stream);
                string jsonContent = reader.ReadToEnd();

                // Log the request for debugging
                SLog.Message(string.Format("[SocialInteractions] OpenAI API Request: {0}", jsonContent));

                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Use the correct OpenAI API endpoint with the provided URL
                string fullUrl = _apiUrl.TrimEnd('/');
                // If the URL doesn't already contain the OpenAI endpoint, append it
                if (!fullUrl.EndsWith("/v1/chat/completions"))
                {
                    if (!fullUrl.EndsWith("/v1"))
                    {
                        fullUrl = fullUrl + "/v1";
                    }
                    fullUrl = fullUrl + "/chat/completions";
                }
                
                var response = await _httpClient.PostAsync(fullUrl, httpContent);
                
                // Log the response status code for debugging
                SLog.Message(string.Format("[SocialInteractions] OpenAI API Response Status: {0}", response.StatusCode));
                
                response.EnsureSuccessStatusCode(); // Throws an exception if the HTTP response status is an error code

                var responseBody = await response.Content.ReadAsStringAsync();
                
                // Log the response body for debugging
                SLog.Message(string.Format("[SocialInteractions] OpenAI API Response Body: {0}", responseBody));

                DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(OpenAiApiResponse));
                MemoryStream responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody));
                OpenAiApiResponse apiResponse = (OpenAiApiResponse)deserializer.ReadObject(responseStream);

                if (apiResponse != null && apiResponse.Choices != null && apiResponse.Choices.Length > 0)
                {
                    return CleanChatResponse(apiResponse.Choices[0].Message.Content);
                }
                return null;
            }
            catch (HttpRequestException ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] OpenAiApiClient: HTTP request failed: {0}", ex.Message));
                return null;
            }
            catch (Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] OpenAiApiClient: Unexpected error during text generation: {0}", ex.Message));
                return null;
            }
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
                if (disposing)
                {
                    // We don't dispose the shared HttpClient as it's shared
                    // Only dispose if we had a custom HttpClient
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