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
    public class GrokApiMessage
    {
        [DataMember(Name = "role")]
        public string Role { get; set; }
        [DataMember(Name = "content")]
        public string Content { get; set; }
    }

    [DataContract]
    public class GrokApiRequest
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "messages")]
        public List<GrokApiMessage> Messages { get; set; }
        [DataMember(Name = "temperature")]
        public float Temperature { get; set; }
        [DataMember(Name = "max_tokens", EmitDefaultValue = false)]
        public int? MaxTokens { get; set; }
        [DataMember(Name = "top_p", EmitDefaultValue = false)]
        public float? TopP { get; set; }
        [DataMember(Name = "top_k", EmitDefaultValue = false)]
        public int? TopK { get; set; }
        [DataMember(Name = "repetition_penalty", EmitDefaultValue = false)]
        public float? RepetitionPenalty { get; set; }
        [DataMember(Name = "stream")]
        public bool Stream { get; set; }
        [DataMember(Name = "stop", EmitDefaultValue = false)]
        public List<string> Stop { get; set; }

        public GrokApiRequest()
        {
            Messages = new List<GrokApiMessage>();
            Stream = false;
        }
    }

    [DataContract]
    public class GrokApiChoice
    {
        [DataMember(Name = "index")]
        public int Index { get; set; }
        [DataMember(Name = "message")]
        public GrokApiMessage Message { get; set; }
        [DataMember(Name = "finish_reason")]
        public string FinishReason { get; set; }
    }

    [DataContract]
    public class GrokApiResponse
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }
        [DataMember(Name = "created")]
        public long Created { get; set; }
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "choices")]
        public GrokApiChoice[] Choices { get; set; }
        [DataMember(Name = "usage")]
        public object Usage { get; set; } // We won't use this directly, but it's in the API response
    }

    public class GrokApiClient : LlmClientBase
    {
        private readonly string _modelName;
        private readonly string _apiKey;

        public override string Name => "Grok";

        public GrokApiClient(string apiUrl, string modelName, string apiKey, LlmClientConfig config = null) : base(apiUrl, config)
        {
            _modelName = modelName;
            _apiKey = apiKey != null ? apiKey.Trim() : null;
        }

        protected override object BuildRequestBody(string prompt, int? maxLength, float? temperature, List<string> stopSequence, bool? enableXtcSampling, int? topK, float? topP, float? minP, float? repetitionPenalty)
        {
            var stopList = BuildStopSequenceList(stopSequence);
            if (stopList.Count == 0)
            {
                stopList = null;
            }

            var request = new GrokApiRequest
            {
                Model = _modelName,
                Temperature = temperature ?? Config.Temperature,
                MaxTokens = maxLength ?? Config.MaxTokens,
                TopP = topP ?? (Config.TopP < 1.0f ? (float?)Config.TopP : null),
                TopK = topK ?? (Config.TopK > 0 ? (int?)Config.TopK : null),
                RepetitionPenalty = repetitionPenalty ?? (Config.RepetitionPenalty != 1.0f ? (float?)Config.RepetitionPenalty : null),
                Stream = false,
                Stop = stopList
            };

            request.Messages.Add(new GrokApiMessage
            {
                Role = "system",
                Content = "You are generating dialogue for characters in a story. Respond with only the dialogue lines, without any thinking, reasoning, or meta-commentary. Do not include tags like <thinking> or explanations."
            });
            request.Messages.Add(new GrokApiMessage { Role = "user", Content = prompt });

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
                    SLog.Warning("[SocialInteractions] Invalid API key format for Grok, skipping Authorization header.");
                }
            }

            return request;
        }

        protected override string BuildRequestUrl()
        {
            string fullUrl = ApiUrl.TrimEnd('/');
            if (!fullUrl.EndsWith("/v1/chat/completions"))
            {
                if (!fullUrl.EndsWith("/v1"))
                {
                    fullUrl = fullUrl + "/v1";
                }
                fullUrl = fullUrl + "/chat/completions";
            }

            return fullUrl;
        }

        protected override string ExtractText(string responseBody)
        {
            GrokApiResponse apiResponse = DeserializeJson<GrokApiResponse>(responseBody);
            if (apiResponse != null && apiResponse.Choices != null && apiResponse.Choices.Length > 0 && apiResponse.Choices[0].Message != null)
            {
                return CleanChatResponse(apiResponse.Choices[0].Message.Content);
            }

            return null;
        }
    }
}
