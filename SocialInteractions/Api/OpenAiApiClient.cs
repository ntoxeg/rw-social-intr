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
using SocialInteractions;

namespace SocialInteractions.Api
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

    public class OpenAiApiClient : LlmClientBase
    {
        private readonly string _modelName;
        private readonly string _apiKey;

        public override string Name => "OpenAI";

        public OpenAiApiClient(string apiUrl, string modelName, string apiKey) : base(apiUrl)
        {
            _modelName = modelName;
            _apiKey = apiKey != null ? apiKey.Trim() : null;
        }

        protected override object BuildRequestBody(string prompt, int? maxLength, float? temperature, List<string> stopSequence, bool? enableXtcSampling, int? topK, float? topP, float? minP, float? repetitionPenalty)
        {
            var request = new OpenAiApiRequest
            {
                Model = _modelName,
                Temperature = temperature ?? SocialInteractions.Settings.llmTemperature,
                MaxTokens = maxLength ?? SocialInteractions.Settings.llmMaxTokens,
                Stream = false,
                Stop = BuildStopSequenceList(stopSequence),
                TopK = topK ?? (SocialInteractions.Settings.llmTopK > 0 ? (int?)SocialInteractions.Settings.llmTopK : null),
                TopP = topP ?? (SocialInteractions.Settings.llmTopP < 1.0f ? (float?)SocialInteractions.Settings.llmTopP : null),
                MinP = minP ?? (SocialInteractions.Settings.llmMinP > 0.0f ? (float?)SocialInteractions.Settings.llmMinP : null),
                RepetitionPenalty = repetitionPenalty ?? (SocialInteractions.Settings.llmRepetitionPenalty != 1.0f ? (float?)SocialInteractions.Settings.llmRepetitionPenalty : null)
            };

            if (enableXtcSampling ?? SocialInteractions.Settings.enableXtcSampling)
            {
                request.XtcProbability = 0.5f;
                request.XtcThreshold = 0.1f;
            }

            request.Messages.Add(new OpenAiApiMessage
            {
                Role = "system",
                Content = "You are generating dialogue for characters in a story. Respond with only the dialogue lines, without any thinking, reasoning, or meta-commentary. Do not include tags like <thinking> or explanations."
            });
            request.Messages.Add(new OpenAiApiMessage { Role = "user", Content = prompt });

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
                    SLog.Warning("[SocialInteractions] Invalid API key format for OpenAI, skipping Authorization header.");
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
            OpenAiApiResponse apiResponse = DeserializeJson<OpenAiApiResponse>(responseBody);
            if (apiResponse != null && apiResponse.Choices != null && apiResponse.Choices.Length > 0 && apiResponse.Choices[0].Message != null)
            {
                return CleanChatResponse(apiResponse.Choices[0].Message.Content);
            }

            return null;
        }
    }
}