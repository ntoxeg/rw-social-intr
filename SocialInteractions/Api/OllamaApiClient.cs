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

    public class OllamaApiClient : LlmClientBase
    {
        private readonly string _modelName;

        public override string Name => "Ollama";

        public OllamaApiClient(string apiUrl, string modelName, LlmClientConfig config = null) : base(apiUrl, config)
        {
            _modelName = modelName;
        }

        private bool UseChatCompletion => Config.ForceChatCompletion;

        protected override object BuildRequestBody(string prompt, int? maxLength, float? temperature, List<string> stopSequence, bool? enableXtcSampling, int? topK, float? topP, float? minP, float? repetitionPenalty)
        {
            var options = new OllamaApiOptions
            {
                Temperature = temperature ?? Config.Temperature,
                TopK = topK ?? (Config.TopK > 0 ? (int?)Config.TopK : null),
                TopP = topP ?? (Config.TopP < 1.0f ? (float?)Config.TopP : null),
                MinP = minP ?? (Config.MinP > 0.0f ? (float?)Config.MinP : null),
                RepeatPenalty = repetitionPenalty ?? (Config.RepetitionPenalty != 1.0f ? (float?)Config.RepetitionPenalty : null),
                NumPredict = maxLength ?? Config.MaxTokens,
                Stop = BuildStopSequenceList(stopSequence)
            };

            if (UseChatCompletion)
            {
                var chatRequest = new OllamaChatRequest
                {
                    Model = _modelName,
                    Stream = false,
                    Options = options,
                    Messages = new List<OllamaChatMessage>
                    {
                        new OllamaChatMessage { Role = "system", Content = "You are generating dialogue for characters in a story. Respond with only the dialogue lines, without any thinking, reasoning, or meta-commentary." },
                        new OllamaChatMessage { Role = "user", Content = prompt }
                    }
                };

                return chatRequest;
            }

            return new OllamaApiRequest
            {
                Model = _modelName,
                Prompt = prompt,
                Stream = false,
                Options = options
            };
        }

        protected override string BuildRequestUrl()
        {
            return UseChatCompletion
                ? ApiUrl.TrimEnd('/') + "/api/chat"
                : ApiUrl.TrimEnd('/') + "/api/generate";
        }

        protected override string ExtractText(string responseBody)
        {
            if (UseChatCompletion)
            {
                OllamaChatResponse chatResponse = DeserializeJson<OllamaChatResponse>(responseBody);
                if (chatResponse != null && chatResponse.Message != null)
                {
                    return CleanChatResponse(chatResponse.Message.Content);
                }

                return null;
            }

            OllamaApiResponse apiResponse = DeserializeJson<OllamaApiResponse>(responseBody);
            if (apiResponse != null && !string.IsNullOrEmpty(apiResponse.Response))
            {
                return CleanChatResponse(apiResponse.Response);
            }

            return null;
        }
    }
}
