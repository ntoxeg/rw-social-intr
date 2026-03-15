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
    public class QwenApiMessage
    {
        [DataMember(Name = "role")]
        public string Role { get; set; }
        [DataMember(Name = "content")]
        public string Content { get; set; }
    }

    [DataContract]
    public class QwenApiRequest
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "messages")]
        public List<QwenApiMessage> Messages { get; set; }
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

        public QwenApiRequest()
        {
            Messages = new List<QwenApiMessage>();
            Stream = false;
        }
    }

    [DataContract]
    public class QwenApiChoice
    {
        [DataMember(Name = "index")]
        public int Index { get; set; }
        [DataMember(Name = "message")]
        public QwenApiMessage Message { get; set; }
        [DataMember(Name = "finish_reason")]
        public string FinishReason { get; set; }
    }

    [DataContract]
    public class QwenApiResponse
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }
        [DataMember(Name = "created")]
        public long Created { get; set; }
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "choices")]
        public QwenApiChoice[] Choices { get; set; }
        [DataMember(Name = "usage")]
        public object Usage { get; set; } // We won't use this directly, but it's in the API response
    }

    public class QwenApiClient : LlmClientBase
    {
        private readonly string _modelName;
        private readonly string _apiKey;

        public override string Name => "Qwen";

        public QwenApiClient(string apiUrl, string modelName, string apiKey, LlmClientConfig config = null) : base(apiUrl, config)
        {
            _modelName = modelName;
            _apiKey = apiKey != null ? apiKey.Trim() : null;
        }

        protected override object BuildRequestBody(string prompt, int? maxLength, float? temperature, List<string> stopSequence, bool? enableXtcSampling, int? topK, float? topP, float? minP, float? repetitionPenalty)
        {
            var request = new QwenApiRequest
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

            request.Messages.Add(new QwenApiMessage
            {
                Role = "system",
                Content = "You are generating dialogue for characters in a story. Respond with only the dialogue lines, without any thinking, reasoning, or meta-commentary. Do not include tags like <thinking> or explanations."
            });
            request.Messages.Add(new QwenApiMessage { Role = "user", Content = prompt });

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
                    SLog.Warning("[SocialInteractions] Invalid API key format for Qwen, skipping Authorization header.");
                }
            }

            return request;
        }

        protected override string BuildRequestUrl()
        {
            string fullUrl = ApiUrl.TrimEnd('/');
            if (!fullUrl.EndsWith("/api/v1/services/aigc/text-generation/generate"))
            {
                if (!fullUrl.EndsWith("/api/v1"))
                {
                    fullUrl = fullUrl + "/api/v1";
                }
                fullUrl = fullUrl + "/services/aigc/text-generation/generate";
            }

            return fullUrl;
        }

        protected override string ExtractText(string responseBody)
        {
            QwenApiResponse apiResponse = DeserializeJson<QwenApiResponse>(responseBody);
            if (apiResponse != null && apiResponse.Choices != null && apiResponse.Choices.Length > 0 && apiResponse.Choices[0].Message != null)
            {
                return CleanChatResponse(apiResponse.Choices[0].Message.Content);
            }

            return null;
        }
    }
}
