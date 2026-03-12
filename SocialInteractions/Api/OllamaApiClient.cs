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
    public class OllamaApiRequest
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "prompt")]
        public string Prompt { get; set; }
        [DataMember(Name = "stream")]
        public bool Stream { get; set; }
        [DataMember(Name = "options")]
        public OllamaApiOptions Options { get; set; }

        public OllamaApiRequest()
        {
            Stream = false;
        }
    }

    [DataContract]
    public class OllamaApiOptions
    {
        [DataMember(Name = "temperature")]
        public float Temperature { get; set; }
        [DataMember(Name = "top_k", EmitDefaultValue = false)]
        public int? TopK { get; set; }
        [DataMember(Name = "top_p", EmitDefaultValue = false)]
        public float? TopP { get; set; }
        [DataMember(Name = "num_predict")]
        public int NumPredict { get; set; }
        [DataMember(Name = "stop")]
        public List<string> Stop { get; set; }
        [DataMember(Name = "mirostat")]
        public int Mirostat { get; set; }
        [DataMember(Name = "mirostat_tau")]
        public float MirostatTau { get; set; }
        [DataMember(Name = "mirostat_eta")]
        public float MirostatEta { get; set; }
        [DataMember(Name = "repeat_penalty", EmitDefaultValue = false)]
        public float? RepeatPenalty { get; set; }
        [DataMember(Name = "min_p", EmitDefaultValue = false)]
        public float? MinP { get; set; }

        public OllamaApiOptions()
        {
            Mirostat = 0;
            MirostatTau = 5.0f;
            MirostatEta = 0.1f;
        }
    }

    [DataContract]
    public class OllamaApiResponse
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "response")]
        public string Response { get; set; }
        [DataMember(Name = "done")]
        public bool Done { get; set; }
    }

    // Chat completion classes
    [DataContract]
    public class OllamaChatMessage
    {
        [DataMember(Name = "role")]
        public string Role { get; set; }
        [DataMember(Name = "content")]
        public string Content { get; set; }
    }

    [DataContract]
    public class OllamaChatRequest
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "messages")]
        public List<OllamaChatMessage> Messages { get; set; }
        [DataMember(Name = "stream")]
        public bool Stream { get; set; }
        [DataMember(Name = "options")]
        public OllamaApiOptions Options { get; set; }

        public OllamaChatRequest()
        {
            Stream = false;
        }
    }

    [DataContract]
    public class OllamaChatResponse
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "message")]
        public OllamaChatMessage Message { get; set; }
        [DataMember(Name = "done")]
        public bool Done { get; set; }
    }

    public class OllamaApiClient : IDisposable
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient();
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _modelName;
        private bool _disposed = false;

        public OllamaApiClient(string apiUrl, string modelName)
        {
            _apiUrl = apiUrl;
            _modelName = modelName;
            _httpClient = SharedHttpClient;
        }

        public async Task<string> GenerateText(string prompt, int? maxLength = null, float? temperature = null, List<string> stopSequence = null, bool? enableXtcSampling = null, int? topK = null, float? topP = null, float? minP = null, float? repetitionPenalty = null)
        {
            if (_disposed)
                throw new ObjectDisposedException("OllamaApiClient");

            try
            {
                if (SocialInteractions.Settings.forceChatCompletion)
                {
                    return await GenerateChatText(prompt, maxLength, temperature, stopSequence, topK, topP, minP, repetitionPenalty);
                }

                var request = new OllamaApiRequest
                {
                    Model = _modelName,
                    Prompt = prompt,
                    Stream = false,
                    Options = new OllamaApiOptions
                    {
                        Temperature = temperature ?? SocialInteractions.Settings.llmTemperature,
                        TopK = topK ?? (SocialInteractions.Settings.llmTopK > 0 ? (int?)SocialInteractions.Settings.llmTopK : null),
                        TopP = topP ?? (SocialInteractions.Settings.llmTopP < 1.0f ? (float?)SocialInteractions.Settings.llmTopP : null),
                        MinP = minP ?? (SocialInteractions.Settings.llmMinP > 0.0f ? (float?)SocialInteractions.Settings.llmMinP : null),
                        RepeatPenalty = repetitionPenalty ?? (SocialInteractions.Settings.llmRepetitionPenalty != 1.0f ? (float?)SocialInteractions.Settings.llmRepetitionPenalty : null),
                        NumPredict = maxLength ?? SocialInteractions.Settings.llmMaxTokens,
                        Stop = stopSequence ?? new List<string>(SocialInteractions.Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                    }
                };

                // Convert to JSON
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(OllamaApiRequest));
                MemoryStream stream = new MemoryStream();
                serializer.WriteObject(stream, request);
                stream.Position = 0;
                StreamReader reader = new StreamReader(stream);
                string jsonContent = reader.ReadToEnd();

                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_apiUrl + "/api/generate", httpContent);
                response.EnsureSuccessStatusCode(); // Throws an exception if the HTTP response status is an error code

                var responseBody = await response.Content.ReadAsStringAsync();

                DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(OllamaApiResponse));
                MemoryStream responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody));
                OllamaApiResponse apiResponse = (OllamaApiResponse)deserializer.ReadObject(responseStream);

                if (apiResponse != null && !string.IsNullOrEmpty(apiResponse.Response))
                {
                    return CleanChatResponse(apiResponse.Response);
                }
                return null;
            }
            catch (HttpRequestException ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] OllamaApiClient: HTTP request failed: {0}", ex.Message));
                return null;
            }
            catch (Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] OllamaApiClient: Unexpected error during text generation: {0}", ex.Message));
                return null;
            }
        }

        private async Task<string> GenerateChatText(string prompt, int? maxLength = null, float? temperature = null, List<string> stopSequence = null, int? topK = null, float? topP = null, float? minP = null, float? repetitionPenalty = null)
        {
            var request = new OllamaChatRequest
            {
                Model = _modelName,
                Stream = false,
                Messages = new List<OllamaChatMessage>
                {
                    new OllamaChatMessage { Role = "system", Content = "You are generating dialogue for characters in a story. Respond with only the dialogue lines, without any thinking, reasoning, or meta-commentary." },
                    new OllamaChatMessage { Role = "user", Content = prompt }
                },
                Options = new OllamaApiOptions
                {
                    Temperature = temperature ?? SocialInteractions.Settings.llmTemperature,
                    TopK = topK ?? (SocialInteractions.Settings.llmTopK > 0 ? (int?)SocialInteractions.Settings.llmTopK : null),
                    TopP = topP ?? (SocialInteractions.Settings.llmTopP < 1.0f ? (float?)SocialInteractions.Settings.llmTopP : null),
                    MinP = minP ?? (SocialInteractions.Settings.llmMinP > 0.0f ? (float?)SocialInteractions.Settings.llmMinP : null),
                    RepeatPenalty = repetitionPenalty ?? (SocialInteractions.Settings.llmRepetitionPenalty != 1.0f ? (float?)SocialInteractions.Settings.llmRepetitionPenalty : null),
                    NumPredict = maxLength ?? SocialInteractions.Settings.llmMaxTokens,
                    Stop = stopSequence ?? new List<string>(SocialInteractions.Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                }
            };

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(OllamaChatRequest));
            MemoryStream stream = new MemoryStream();
            serializer.WriteObject(stream, request);
            stream.Position = 0;
            StreamReader reader = new StreamReader(stream);
            string jsonContent = reader.ReadToEnd();

            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_apiUrl.TrimEnd('/') + "/api/chat", httpContent);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(OllamaChatResponse));
            using (var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody)))
            {
                var apiResponse = (OllamaChatResponse)deserializer.ReadObject(responseStream);
                if (apiResponse != null && apiResponse.Message != null)
                {
                    return CleanChatResponse(apiResponse.Message.Content);
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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