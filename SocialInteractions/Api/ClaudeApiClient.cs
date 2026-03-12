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
using System.Net.Http.Headers;

namespace SocialInteractions
{
    [DataContract]
    public class ClaudeApiTextBlock
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "text")]
        public string Text { get; set; }
        
        public ClaudeApiTextBlock()
        {
            Type = "text";
        }
    }

    [DataContract]
    public class ClaudeApiMessage
    {
        [DataMember(Name = "role")]
        public string Role { get; set; }
        [DataMember(Name = "content")]
        public object Content { get; set; } // Can be string or array of content blocks
    }

    [DataContract]
    public class ClaudeApiRequest
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "max_tokens")]
        public int MaxTokens { get; set; }
        [DataMember(Name = "messages")]
        public List<ClaudeApiMessage> Messages { get; set; }
        [DataMember(Name = "temperature", EmitDefaultValue = false)]
        public float? Temperature { get; set; }
        [DataMember(Name = "top_p", EmitDefaultValue = false)]
        public float? TopP { get; set; }
        [DataMember(Name = "top_k", EmitDefaultValue = false)]
        public int? TopK { get; set; }
        [DataMember(Name = "repetition_penalty", EmitDefaultValue = false)]
        public float? RepetitionPenalty { get; set; }
        [DataMember(Name = "system", EmitDefaultValue = false)]
        public string System { get; set; }
        [DataMember(Name = "stop_sequences", EmitDefaultValue = false)]
        public List<string> StopSequences { get; set; }
        [DataMember(Name = "thinking", EmitDefaultValue = false)]
        public ClaudeApiThinking Thinking { get; set; }

        public ClaudeApiRequest()
        {
            Messages = new List<ClaudeApiMessage>();
        }
    }

    [DataContract]
    public class ClaudeApiThinking
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "budget_tokens")]
        public int BudgetTokens { get; set; }
    }

    [DataContract]
    public class ClaudeApiTextBlockResponse
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "text")]
        public string Text { get; set; }
    }

    [DataContract]
    public class ClaudeApiResponse
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "role")]
        public string Role { get; set; }
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "content")]
        public List<ClaudeApiTextBlockResponse> Content { get; set; }
        [DataMember(Name = "stop_reason")]
        public string StopReason { get; set; }
        [DataMember(Name = "stop_sequence")]
        public string StopSequence { get; set; }
        [DataMember(Name = "usage")]
        public object Usage { get; set; } // Usage information with input/output tokens
    }

    public class ClaudeApiClient : IDisposable
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient();
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _modelName;
        private readonly string _apiKey;
        private bool _disposed = false;

        public ClaudeApiClient(string apiUrl, string modelName, string apiKey)
        {
            _apiUrl = apiUrl;
            _modelName = modelName;
            // Trim whitespace which can cause header issues
            _apiKey = (apiKey != null) ? apiKey.Trim() : null;
            _httpClient = SharedHttpClient;
            
            // Clear any existing default request headers
            _httpClient.DefaultRequestHeaders.Clear();
            
            // Add required headers for Claude API
            if (!string.IsNullOrEmpty(_apiKey))
            {
                try
                {
                    // Validate that the API key doesn't contain invalid characters
                    if (IsValidHeaderValue(_apiKey))
                    {
                        // Claude API uses x-api-key header (not Authorization)
                        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
                    }
                    else
                    {
                        SLog.Warning("[SocialInteractions] Invalid API key format for Claude, skipping x-api-key header.");
                    }
                }
                catch (Exception ex)
                {
                    SLog.Warning(string.Format("[SocialInteractions] Failed to add x-api-key header for Claude. Error: {0}", ex.Message));
                }
            }
            
            // Add required Claude-specific headers
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
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
                throw new ObjectDisposedException("ClaudeApiClient");

            try
            {
                var request = new ClaudeApiRequest
                {
                    Model = _modelName,
                    MaxTokens = maxLength ?? SocialInteractions.Settings.llmMaxTokens,
                    Temperature = temperature ?? SocialInteractions.Settings.llmTemperature,
                    TopP = topP ?? (SocialInteractions.Settings.llmTopP < 1.0f ? (float?)SocialInteractions.Settings.llmTopP : null),
                    TopK = topK ?? (SocialInteractions.Settings.llmTopK > 0 ? (int?)SocialInteractions.Settings.llmTopK : null),
                    RepetitionPenalty = repetitionPenalty ?? (SocialInteractions.Settings.llmRepetitionPenalty != 1.0f ? (float?)SocialInteractions.Settings.llmRepetitionPenalty : null),
                    System = "You are generating dialogue for characters in a story. Respond with only the dialogue lines, without any thinking, reasoning, or meta-commentary. Do not include tags like <thinking> or explanations.",
                    StopSequences = stopSequence ?? new List<string>(SocialInteractions.Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                };

                // Add the prompt as a user message
                request.Messages.Add(new ClaudeApiMessage
                {
                    Role = "user",
                    Content = prompt
                });

                if (SocialInteractions.Settings.disableLlmThinking)
                {
                    request.Thinking = new ClaudeApiThinking
                    {
                        Type = "disabled"
                    };
                }
                else
                {
                    // By default, if the model supports it, enable it with a reasonable budget
                    request.Thinking = new ClaudeApiThinking
                    {
                        Type = "enabled",
                        BudgetTokens = Math.Max(1024, SocialInteractions.Settings.llmMaxTokens)
                    };
                }

                // Convert to JSON
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(ClaudeApiRequest));
                MemoryStream stream = new MemoryStream();
                serializer.WriteObject(stream, request);
                stream.Position = 0;
                StreamReader reader = new StreamReader(stream);
                string jsonContent = reader.ReadToEnd();

                // Log the request for debugging
                SLog.Message(string.Format("[SocialInteractions] Claude API Request: {0}", jsonContent));

                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                // Set the content type for this specific request
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                // Use the correct Claude API endpoint
                string fullUrl = _apiUrl.TrimEnd('/');
                
                // For Claude API, if the URL doesn't already contain the messages endpoint, append it
                if (!fullUrl.EndsWith("/v1/messages"))
                {
                    if (!fullUrl.EndsWith("/v1"))
                    {
                        fullUrl = fullUrl + "/v1";
                    }
                    fullUrl = fullUrl + "/messages";
                }

                var response = await _httpClient.PostAsync(fullUrl, httpContent);
                
                // Log the response status code for debugging
                SLog.Message(string.Format("[SocialInteractions] Claude API Response Status: {0}", response.StatusCode));
                
                response.EnsureSuccessStatusCode(); // Throws an exception if the HTTP response status is an error code

                var responseBody = await response.Content.ReadAsStringAsync();
                
                // Log the response body for debugging
                SLog.Message(string.Format("[SocialInteractions] Claude API Response Body: {0}", responseBody));

                DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(ClaudeApiResponse));
                MemoryStream responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody));
                ClaudeApiResponse apiResponse = (ClaudeApiResponse)deserializer.ReadObject(responseStream);

                if (apiResponse != null && apiResponse.Content != null && apiResponse.Content.Count > 0)
                {
                    // Extract text from the first content block
                    var firstBlock = apiResponse.Content[0];
                    return firstBlock != null ? CleanChatResponse(firstBlock.Text) : null;
                }
                return null;
            }
            catch (HttpRequestException ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] ClaudeApiClient: HTTP request failed: {0}", ex.Message));
                return null;
            }
            catch (Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] ClaudeApiClient: Unexpected error during text generation: {0}", ex.Message));
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