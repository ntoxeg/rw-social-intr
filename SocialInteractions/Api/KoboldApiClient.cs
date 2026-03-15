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

    public class KoboldApiClient : LlmClientBase
    {
        private readonly string _apiKey;

        public override string Name => "KoboldCpp";

        public KoboldApiClient(string apiUrl, string apiKey, LlmClientConfig config = null) : base(apiUrl, config)
        {
            _apiKey = apiKey != null ? apiKey.Trim() : null;
        }

        private bool UseChatCompletion => Config.ForceChatCompletion;

        protected override object BuildRequestBody(string prompt, int? maxLength, float? temperature, List<string> stopSequence, bool? enableXtcSampling, int? topK, float? topP, float? minP, float? repetitionPenalty)
        {
            if (UseChatCompletion)
            {
                var chatRequest = new KoboldChatRequest
                {
                    MaxTokens = maxLength ?? Config.MaxTokens,
                    Temperature = temperature ?? Config.Temperature,
                    Stop = BuildStopSequenceList(stopSequence),
                    TopK = topK ?? (Config.TopK > 0 ? (int?)Config.TopK : null),
                    TopP = topP ?? (Config.TopP < 1.0f ? (float?)Config.TopP : null),
                    MinP = minP ?? (Config.MinP > 0.0f ? (float?)Config.MinP : null),
                    RepetitionPenalty = repetitionPenalty ?? (Config.RepetitionPenalty != 1.0f ? (float?)Config.RepetitionPenalty : null)
                };

                chatRequest.Messages.Add(new KoboldChatMessage { Role = "system", Content = "You are generating dialogue for characters in a story. Respond with only the dialogue lines, without any thinking, reasoning, or meta-commentary." });
                chatRequest.Messages.Add(new KoboldChatMessage { Role = "user", Content = prompt });
                return chatRequest;
            }

            var request = new KoboldApiRequest
            {
                Prompt = prompt,
                MaxLength = maxLength ?? Config.MaxTokens,
                Temperature = temperature ?? Config.Temperature,
                StopSequence = BuildStopSequenceList(stopSequence),
                TopK = topK ?? Config.TopK,
                TopP = topP ?? Config.TopP,
                MinP = minP ?? Config.MinP,
                RepetitionPenalty = repetitionPenalty ?? (Config.RepetitionPenalty != 1.0f ? (float?)Config.RepetitionPenalty : null),
                SamplerOrder = new[] { 6, 0, 1, 3, 4, 2, 5 }
            };

            if (enableXtcSampling ?? Config.EnableXtcSampling)
            {
                request.XtcProbability = 0.5f;
                request.XtcThreshold = 0.1f;
            }

            return request;
        }

        protected override HttpRequestMessage CreateRequestMessage(string requestUrl, HttpContent content)
        {
            var request = base.CreateRequestMessage(requestUrl, content);
            if (!string.IsNullOrEmpty(_apiKey) && IsValidHeaderValue(_apiKey))
            {
                request.Headers.TryAddWithoutValidation("Authorization", string.Format("Bearer {0}", _apiKey));
            }

            return request;
        }

        protected override string BuildRequestUrl()
        {
            return UseChatCompletion
                ? ApiUrl.TrimEnd('/') + "/v1/chat/completions"
                : ApiUrl.TrimEnd('/') + "/api/v1/generate";
        }

        protected override string ExtractText(string responseBody)
        {
            if (UseChatCompletion)
            {
                KoboldChatResponse chatResponse = DeserializeJson<KoboldChatResponse>(responseBody);
                if (chatResponse != null && chatResponse.Choices != null && chatResponse.Choices.Length > 0 && chatResponse.Choices[0].Message != null)
                {
                    return CleanChatResponse(chatResponse.Choices[0].Message.Content);
                }

                return null;
            }

            KoboldApiResponse apiResponse = DeserializeJson<KoboldApiResponse>(responseBody);
            if (apiResponse != null && apiResponse.Results != null && apiResponse.Results.Length > 0)
            {
                return CleanChatResponse(apiResponse.Results[0].Text);
            }

            return null;
        }
    }
}
