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
using SocialInteractions;

namespace SocialInteractions.Api
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

    public class ClaudeApiClient : LlmClientBase
    {
        private readonly string _modelName;
        private readonly string _apiKey;

        public override string Name => "Claude";

        public ClaudeApiClient(string apiUrl, string modelName, string apiKey, LlmClientConfig config = null) : base(apiUrl, config)
        {
            _modelName = modelName;
            _apiKey = apiKey != null ? apiKey.Trim() : null;
        }

        protected override object BuildRequestBody(string prompt, int? maxLength, float? temperature, List<string> stopSequence, bool? enableXtcSampling, int? topK, float? topP, float? minP, float? repetitionPenalty)
        {
            var request = new ClaudeApiRequest
            {
                Model = _modelName,
                MaxTokens = maxLength ?? Config.MaxTokens,
                Temperature = temperature ?? Config.Temperature,
                TopP = topP ?? (Config.TopP < 1.0f ? (float?)Config.TopP : null),
                TopK = topK ?? (Config.TopK > 0 ? (int?)Config.TopK : null),
                RepetitionPenalty = repetitionPenalty ?? (Config.RepetitionPenalty != 1.0f ? (float?)Config.RepetitionPenalty : null),
                System = "You are generating dialogue for characters in a story. Respond with only the dialogue lines, without any thinking, reasoning, or meta-commentary. Do not include tags like <thinking> or explanations.",
                StopSequences = BuildStopSequenceList(stopSequence)
            };

            request.Messages.Add(new ClaudeApiMessage { Role = "user", Content = prompt });
            request.Thinking = Config.DisableThinking
                ? new ClaudeApiThinking { Type = "disabled" }
                : new ClaudeApiThinking { Type = "enabled", BudgetTokens = Math.Max(1024, Config.MaxTokens) };

            return request;
        }

        protected override HttpRequestMessage CreateRequestMessage(string requestUrl, HttpContent content)
        {
            var request = base.CreateRequestMessage(requestUrl, content);
            request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

            if (!string.IsNullOrEmpty(_apiKey))
            {
                if (IsValidHeaderValue(_apiKey))
                {
                    request.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
                }
                else
                {
                    SLog.Warning("[SocialInteractions] Invalid API key format for Claude, skipping x-api-key header.");
                }
            }

            return request;
        }

        protected override string BuildRequestUrl()
        {
            string fullUrl = ApiUrl.TrimEnd('/');
            if (!fullUrl.EndsWith("/v1/messages"))
            {
                if (!fullUrl.EndsWith("/v1"))
                {
                    fullUrl = fullUrl + "/v1";
                }
                fullUrl = fullUrl + "/messages";
            }

            return fullUrl;
        }

        protected override string ExtractText(string responseBody)
        {
            ClaudeApiResponse apiResponse = DeserializeJson<ClaudeApiResponse>(responseBody);
            if (apiResponse != null && apiResponse.Content != null && apiResponse.Content.Count > 0 && apiResponse.Content[0] != null)
            {
                return CleanChatResponse(apiResponse.Content[0].Text);
            }

            return null;
        }
    }
}
