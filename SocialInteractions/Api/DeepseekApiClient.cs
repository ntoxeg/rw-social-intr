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
    public class DeepseekApiMessage
    {
        [DataMember(Name = "role")]
        public string Role { get; set; }
        [DataMember(Name = "content")]
        public string Content { get; set; }
    }

    [DataContract]
    public class DeepseekApiRequest
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "messages")]
        public List<DeepseekApiMessage> Messages { get; set; }
        [DataMember(Name = "temperature", EmitDefaultValue = false)]
        public float? Temperature { get; set; }
        [DataMember(Name = "max_tokens", EmitDefaultValue = false)]
        public int? MaxTokens { get; set; }
        [DataMember(Name = "top_p", EmitDefaultValue = false)]
        public float? TopP { get; set; }
        [DataMember(Name = "top_k", EmitDefaultValue = false)]
        public int? TopK { get; set; }
        [DataMember(Name = "repetition_penalty", EmitDefaultValue = false)]
        public float? RepetitionPenalty { get; set; }
        [DataMember(Name = "stream", EmitDefaultValue = false)]
        public bool? Stream { get; set; }
        [DataMember(Name = "stop", EmitDefaultValue = false)]
        public List<string> Stop { get; set; }
        [DataMember(Name = "thinking", EmitDefaultValue = false)]
        public DeepseekApiThinking Thinking { get; set; }

        public DeepseekApiRequest()
        {
            Messages = new List<DeepseekApiMessage>();
            Stream = false;
        }
    }

    [DataContract]
    public class DeepseekApiThinking
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "budget_tokens")]
        public int BudgetTokens { get; set; }
    }

    [DataContract]
    public class DeepseekApiChoice
    {
        [DataMember(Name = "index")]
        public int Index { get; set; }
        [DataMember(Name = "message")]
        public DeepseekApiMessage Message { get; set; }
        [DataMember(Name = "finish_reason")]
        public string FinishReason { get; set; }
    }

    [DataContract]
    public class DeepseekApiResponse
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }
        [DataMember(Name = "created")]
        public long Created { get; set; }
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "choices")]
        public DeepseekApiChoice[] Choices { get; set; }
        [DataMember(Name = "usage")]
        public object Usage { get; set; } // We won't use this directly, but it's in the API response
    }

    public class DeepseekApiClient : LlmClientBase
    {
        private readonly string _modelName;
        private readonly string _apiKey;

        public override string Name => "Deepseek";

        public DeepseekApiClient(string apiUrl, string modelName, string apiKey, LlmClientConfig config = null) : base(apiUrl, config)
        {
            _modelName = modelName;
            _apiKey = apiKey != null ? apiKey.Trim() : null;
        }

        protected override object BuildRequestBody(string prompt, int? maxLength, float? temperature, List<string> stopSequence, bool? enableXtcSampling, int? topK, float? topP, float? minP, float? repetitionPenalty)
        {
            var request = new DeepseekApiRequest
            {
                Model = _modelName,
                Temperature = temperature ?? Config.Temperature,
                MaxTokens = maxLength ?? Config.MaxTokens,
                TopP = topP ?? (Config.TopP < 1.0f ? (float?)Config.TopP : null),
                TopK = topK ?? (Config.TopK > 0 ? (int?)Config.TopK : null),
                RepetitionPenalty = repetitionPenalty ?? (Config.RepetitionPenalty != 1.0f ? (float?)Config.RepetitionPenalty : null),
                Stream = false,
                Stop = BuildStopSequenceList(stopSequence)
            };

            request.Messages.Add(new DeepseekApiMessage
            {
                Role = "system",
                Content = "You are generating dialogue for characters in a story. Respond with only the dialogue lines, without any thinking, reasoning, or meta-commentary. Do not include tags like <thinking> or explanations."
            });
            request.Messages.Add(new DeepseekApiMessage { Role = "user", Content = prompt });

            request.Thinking = Config.DisableThinking
                ? new DeepseekApiThinking { Type = "disabled" }
                : new DeepseekApiThinking { Type = "enabled", BudgetTokens = Math.Max(1024, Config.MaxTokens) };

            return request;
        }

        protected override HttpRequestMessage CreateRequestMessage(string requestUrl, HttpContent content)
        {
            var request = base.CreateRequestMessage(requestUrl, content);
            if (!string.IsNullOrEmpty(_apiKey))
            {
                if (IsValidHeaderValue(_apiKey))
                {
                    request.Headers.TryAddWithoutValidation("Authorization", string.Format("Bearer {0}", _apiKey));
                }
                else
                {
                    SLog.Warning("[SocialInteractions] Invalid API key format for Deepseek, skipping Authorization header.");
                }
            }

            return request;
        }

        protected override string BuildRequestUrl()
        {
            string fullUrl = ApiUrl.TrimEnd('/');
            if (!fullUrl.EndsWith("/chat/completions"))
            {
                fullUrl = fullUrl + "/chat/completions";
            }
            return fullUrl;
        }

        protected override string ExtractText(string responseBody)
        {
            DeepseekApiResponse apiResponse = DeserializeJson<DeepseekApiResponse>(responseBody);
            if (apiResponse != null && apiResponse.Choices != null && apiResponse.Choices.Length > 0 && apiResponse.Choices[0].Message != null)
            {
                return CleanChatResponse(apiResponse.Choices[0].Message.Content);
            }

            return null;
        }
    }
}
