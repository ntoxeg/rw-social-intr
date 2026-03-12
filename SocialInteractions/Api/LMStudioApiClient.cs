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
    // Text completion request format
// Text completion classes (Legacy)
    [DataContract]
    public class LMStudioCompletionRequest
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "prompt")]
        public string Prompt { get; set; }
        [DataMember(Name = "temperature")]
        public float Temperature { get; set; }
        [DataMember(Name = "max_tokens")]
        public int? MaxTokens { get; set; }
        [DataMember(Name = "stream")]
        public bool Stream { get; set; }
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

        public LMStudioCompletionRequest()
        {
            Stream = false;
        }
    }

    [DataContract]
    public class LMStudioCompletionChoice
    {
        [DataMember(Name = "index")]
        public int Index { get; set; }
        [DataMember(Name = "text")]
        public string Text { get; set; }
        [DataMember(Name = "finish_reason")]
        public string FinishReason { get; set; }
    }

    [DataContract]
    public class LMStudioCompletionResponse
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
        public LMStudioCompletionChoice[] Choices { get; set; }
        [DataMember(Name = "usage")]
        public object Usage { get; set; }
    }

    // Chat completion classes (OpenAI compatible)
    [DataContract]
    public class LMStudioChatMessage
    {
        [DataMember(Name = "role")]
        public string Role { get; set; }
        [DataMember(Name = "content")]
        public string Content { get; set; }
    }

    [DataContract]
    public class LMStudioChatRequest
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "messages")]
        public List<LMStudioChatMessage> Messages { get; set; }
        [DataMember(Name = "temperature")]
        public float Temperature { get; set; }
        [DataMember(Name = "max_tokens")]
        public int? MaxTokens { get; set; }
        [DataMember(Name = "stream")]
        public bool Stream { get; set; }
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

        public LMStudioChatRequest()
        {
            Stream = false;
            Messages = new List<LMStudioChatMessage>();
        }
    }

    [DataContract]
    public class LMStudioChatResponse
    {
        [DataMember(Name = "choices")]
        public LMStudioChatChoice[] Choices { get; set; }
    }

    [DataContract]
    public class LMStudioChatChoice
    {
        [DataMember(Name = "message")]
        public LMStudioChatMessage Message { get; set; }
    }

    public class LMStudioApiClient : IDisposable
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient();
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _modelName;
        private bool _disposed = false;

        public LMStudioApiClient(string apiUrl, string modelName)
        {
            _apiUrl = apiUrl;
            _modelName = modelName;
            _httpClient = SharedHttpClient;
        }

        public async Task<string> GenerateText(string prompt, int? maxLength = null, float? temperature = null, List<string> stopSequence = null, bool? enableXtcSampling = null, int? topK = null, float? topP = null, float? minP = null, float? repetitionPenalty = null)
        {
            if (_disposed)
                throw new ObjectDisposedException("LMStudioApiClient");

            try
            {
                if (SocialInteractions.Settings.forceChatCompletion)
                {
                    return await GenerateChatText(prompt, maxLength, temperature, stopSequence, topK, topP, minP, repetitionPenalty);
                }

                var request = new LMStudioCompletionRequest
                {
                    Model = _modelName,
                    Prompt = prompt,
                    Temperature = temperature ?? SocialInteractions.Settings.llmTemperature,
                    MaxTokens = maxLength ?? SocialInteractions.Settings.llmMaxTokens,
                    TopK = topK ?? (SocialInteractions.Settings.llmTopK > 0 ? (int?)SocialInteractions.Settings.llmTopK : null),
                    TopP = topP ?? (SocialInteractions.Settings.llmTopP < 1.0f ? (float?)SocialInteractions.Settings.llmTopP : null),
                    MinP = minP ?? (SocialInteractions.Settings.llmMinP > 0.0f ? (float?)SocialInteractions.Settings.llmMinP : null),
                    RepetitionPenalty = repetitionPenalty ?? (SocialInteractions.Settings.llmRepetitionPenalty != 1.0f ? (float?)SocialInteractions.Settings.llmRepetitionPenalty : null),
                    Stream = false,
                    Stop = stopSequence ?? new List<string>(SocialInteractions.Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                };

                // Convert to JSON
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(LMStudioCompletionRequest));
                MemoryStream stream = new MemoryStream();
                serializer.WriteObject(stream, request);
                stream.Position = 0;
                StreamReader reader = new StreamReader(stream);
                string jsonContent = reader.ReadToEnd();

                // Log the request for debugging
                SLog.Message(string.Format("[SocialInteractions] LMStudio API Request: {0}", jsonContent));

                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_apiUrl.TrimEnd('/') + "/v1/completions", httpContent);
                
                // Log the response status code for debugging
                SLog.Message(string.Format("[SocialInteractions] LMStudio API Response Status: {0}", response.StatusCode));
                
                response.EnsureSuccessStatusCode(); // Throws an exception if the HTTP response status is an error code

                var responseBody = await response.Content.ReadAsStringAsync();
                
                // Log the response body for debugging
                SLog.Message(string.Format("[SocialInteractions] LMStudio API Response Body: {0}", responseBody));

                DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(LMStudioCompletionResponse));
                MemoryStream responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody));
                LMStudioCompletionResponse apiResponse = (LMStudioCompletionResponse)deserializer.ReadObject(responseStream);

                if (apiResponse != null && apiResponse.Choices != null && apiResponse.Choices.Length > 0)
                {
                    return CleanChatResponse(apiResponse.Choices[0].Text);
                }
                return null;
            }
            catch (HttpRequestException ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] LMStudioApiClient: HTTP request failed: {0}", ex.Message));
                return null;
            }
            catch (Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] LMStudioApiClient: Unexpected error during text generation: {0}", ex.Message));
                return null;
            }
        }

        private async Task<string> GenerateChatText(string prompt, int? maxLength = null, float? temperature = null, List<string> stopSequence = null, int? topK = null, float? topP = null, float? minP = null, float? repetitionPenalty = null)
        {
            var request = new LMStudioChatRequest
            {
                Model = _modelName,
                Temperature = temperature ?? SocialInteractions.Settings.llmTemperature,
                MaxTokens = maxLength ?? SocialInteractions.Settings.llmMaxTokens,
                TopK = topK ?? (SocialInteractions.Settings.llmTopK > 0 ? (int?)SocialInteractions.Settings.llmTopK : null),
                TopP = topP ?? (SocialInteractions.Settings.llmTopP < 1.0f ? (float?)SocialInteractions.Settings.llmTopP : null),
                MinP = minP ?? (SocialInteractions.Settings.llmMinP > 0.0f ? (float?)SocialInteractions.Settings.llmMinP : null),
                RepetitionPenalty = repetitionPenalty ?? (SocialInteractions.Settings.llmRepetitionPenalty != 1.0f ? (float?)SocialInteractions.Settings.llmRepetitionPenalty : null),
                Stream = false,
                Stop = stopSequence ?? new List<string>(SocialInteractions.Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            };

            request.Messages.Add(new LMStudioChatMessage { Role = "system", Content = "You are generating dialogue for characters in a story. Respond with only the dialogue lines, without any thinking, reasoning, or meta-commentary." });
            request.Messages.Add(new LMStudioChatMessage { Role = "user", Content = prompt });

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(LMStudioChatRequest));
            MemoryStream stream = new MemoryStream();
            serializer.WriteObject(stream, request);
            stream.Position = 0;
            StreamReader reader = new StreamReader(stream);
            string jsonContent = reader.ReadToEnd();

            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_apiUrl.TrimEnd('/') + "/v1/chat/completions", httpContent);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(LMStudioChatResponse));
            using (var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody)))
            {
                var apiResponse = (LMStudioChatResponse)deserializer.ReadObject(responseStream);
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