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

    public class LMStudioApiClient : LlmClientBase
    {
        private readonly string _modelName;

        public override string Name => "LMStudio";

        public LMStudioApiClient(string apiUrl, string modelName, LlmClientConfig config = null) : base(apiUrl, config)
        {
            _modelName = modelName;
        }

        private bool UseChatCompletion => Config.ForceChatCompletion;

        protected override object BuildRequestBody(string prompt, int? maxLength, float? temperature, List<string> stopSequence, bool? enableXtcSampling, int? topK, float? topP, float? minP, float? repetitionPenalty)
        {
            if (UseChatCompletion)
            {
                var chatRequest = new LMStudioChatRequest
                {
                    Model = _modelName,
                    Temperature = temperature ?? Config.Temperature,
                    MaxTokens = maxLength ?? Config.MaxTokens,
                    TopK = topK ?? (Config.TopK > 0 ? (int?)Config.TopK : null),
                    TopP = topP ?? (Config.TopP < 1.0f ? (float?)Config.TopP : null),
                    MinP = minP ?? (Config.MinP > 0.0f ? (float?)Config.MinP : null),
                    RepetitionPenalty = repetitionPenalty ?? (Config.RepetitionPenalty != 1.0f ? (float?)Config.RepetitionPenalty : null),
                    Stream = false,
                    Stop = BuildStopSequenceList(stopSequence)
                };

                chatRequest.Messages.Add(new LMStudioChatMessage { Role = "system", Content = "You are generating dialogue for characters in a story. Respond with only the dialogue lines, without any thinking, reasoning, or meta-commentary." });
                chatRequest.Messages.Add(new LMStudioChatMessage { Role = "user", Content = prompt });
                return chatRequest;
            }

            return new LMStudioCompletionRequest
            {
                Model = _modelName,
                Prompt = prompt,
                Temperature = temperature ?? Config.Temperature,
                MaxTokens = maxLength ?? Config.MaxTokens,
                TopK = topK ?? (Config.TopK > 0 ? (int?)Config.TopK : null),
                TopP = topP ?? (Config.TopP < 1.0f ? (float?)Config.TopP : null),
                MinP = minP ?? (Config.MinP > 0.0f ? (float?)Config.MinP : null),
                RepetitionPenalty = repetitionPenalty ?? (Config.RepetitionPenalty != 1.0f ? (float?)Config.RepetitionPenalty : null),
                Stream = false,
                Stop = BuildStopSequenceList(stopSequence)
            };
        }

        protected override string BuildRequestUrl()
        {
            return UseChatCompletion
                ? ApiUrl.TrimEnd('/') + "/v1/chat/completions"
                : ApiUrl.TrimEnd('/') + "/v1/completions";
        }

        protected override string ExtractText(string responseBody)
        {
            if (UseChatCompletion)
            {
                LMStudioChatResponse chatResponse = DeserializeJson<LMStudioChatResponse>(responseBody);
                if (chatResponse != null && chatResponse.Choices != null && chatResponse.Choices.Length > 0 && chatResponse.Choices[0].Message != null)
                {
                    return CleanChatResponse(chatResponse.Choices[0].Message.Content);
                }

                return null;
            }

            LMStudioCompletionResponse completionResponse = DeserializeJson<LMStudioCompletionResponse>(responseBody);
            if (completionResponse != null && completionResponse.Choices != null && completionResponse.Choices.Length > 0)
            {
                return CleanChatResponse(completionResponse.Choices[0].Text);
            }

            return null;
        }
    }
}
