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
    public class GeminiApiPart
    {
        [DataMember(Name = "text", EmitDefaultValue = false)]
        public string Text { get; set; }
    }

    [DataContract]
    public class GeminiApiContent
    {
        [DataMember(Name = "parts")]
        public List<GeminiApiPart> Parts { get; set; }
        [DataMember(Name = "role", EmitDefaultValue = false)]
        public string Role { get; set; }

        public GeminiApiContent()
        {
            Parts = new List<GeminiApiPart>();
        }
    }
    [DataContract]
    public class GeminiApiGenerationConfig
    {
        [DataMember(Name = "maxOutputTokens")]
        public int MaxOutputTokens { get; set; }
        [DataMember(Name = "stopSequences")]
        public List<string> StopSequences { get; set; }
        [DataMember(Name = "temperature")]
        public float Temperature { get; set; }
        [DataMember(Name = "topP", EmitDefaultValue = false)]
        public float? TopP { get; set; }
        [DataMember(Name = "topK", EmitDefaultValue = false)]
        public int? TopK { get; set; }
        [DataMember(Name = "repetition_penalty", EmitDefaultValue = false)]
        public float? RepetitionPenalty { get; set; }
        [DataMember(Name = "thinkingConfig", EmitDefaultValue = false)]
        public GeminiApiThinkingConfig ThinkingConfig { get; set; }
    }

    [DataContract]
    public class GeminiApiThinkingConfig
    {
        [DataMember(Name = "thinkingBudget", EmitDefaultValue = false)]
        public int? ThinkingBudget { get; set; }
        [DataMember(Name = "thinkingLevel", EmitDefaultValue = false)]
        public string ThinkingLevel { get; set; }
    }

    [DataContract]
    public class GeminiApiSystemInstruction
    {
        [DataMember(Name = "parts")]
        public List<GeminiApiPart> Parts { get; set; }

        public GeminiApiSystemInstruction()
        {
            Parts = new List<GeminiApiPart>();
        }
    }

    [DataContract]
    public class GeminiApiRequest
    {
        [DataMember(Name = "contents")]
        public List<GeminiApiContent> Contents { get; set; }
        [DataMember(Name = "generationConfig", EmitDefaultValue = false)]
        public GeminiApiGenerationConfig GenerationConfig { get; set; }
        [DataMember(Name = "system_instruction", EmitDefaultValue = false)]
        public GeminiApiSystemInstruction SystemInstruction { get; set; }

        public GeminiApiRequest()
        {
            Contents = new List<GeminiApiContent>();
        }
    }

    [DataContract]
    public class GeminiApiResponseCandidate
    {
        [DataMember(Name = "content")]
        public GeminiApiContent Content { get; set; }
        [DataMember(Name = "finishReason", EmitDefaultValue = false)]
        public string FinishReason { get; set; }
    }

    [DataContract]
    public class GeminiApiResponse
    {
        [DataMember(Name = "candidates")]
        public GeminiApiResponseCandidate[] Candidates { get; set; }
    }

    public class GeminiApiClient : LlmClientBase
    {
        private readonly string _apiKey;

        public override string Name => "Gemini";

        public GeminiApiClient(string apiUrl, string apiKey) : base(apiUrl)
        {
            _apiKey = apiKey != null ? apiKey.Trim() : null;
        }

        protected override object BuildRequestBody(string prompt, int? maxLength, float? temperature, List<string> stopSequence, bool? enableXtcSampling, int? topK, float? topP, float? minP, float? repetitionPenalty)
        {
            var request = new GeminiApiRequest();

            var stopSequences = BuildStopSequenceList(stopSequence);
            if (stopSequences.Count > 5)
            {
                stopSequences = stopSequences.GetRange(0, 5);
            }

            request.GenerationConfig = new GeminiApiGenerationConfig
            {
                MaxOutputTokens = maxLength ?? SocialInteractions.Settings.llmMaxTokens,
                Temperature = temperature ?? SocialInteractions.Settings.llmTemperature,
                TopP = topP ?? (SocialInteractions.Settings.llmTopP < 1.0f ? (float?)SocialInteractions.Settings.llmTopP : null),
                TopK = topK ?? (SocialInteractions.Settings.llmTopK > 0 ? (int?)SocialInteractions.Settings.llmTopK : null),
                RepetitionPenalty = repetitionPenalty ?? (SocialInteractions.Settings.llmRepetitionPenalty != 1.0f ? (float?)SocialInteractions.Settings.llmRepetitionPenalty : null),
                StopSequences = stopSequences
            };

            if (SocialInteractions.Settings.disableLlmThinking)
            {
                request.GenerationConfig.ThinkingConfig = new GeminiApiThinkingConfig { ThinkingBudget = 0 };
            }

            request.SystemInstruction = new GeminiApiSystemInstruction();
            request.SystemInstruction.Parts.Add(new GeminiApiPart
            {
                Text = "You are generating dialogue for characters in a story. Respond with only the dialogue lines, without any thinking, reasoning, or meta-commentary. Do not include tags like <thinking> or explanations."
            });

            var content = new GeminiApiContent { Role = "user" };
            content.Parts.Add(new GeminiApiPart { Text = prompt });
            request.Contents.Add(content);
            return request;
        }

        protected override HttpRequestMessage CreateRequestMessage(string requestUrl, HttpContent content)
        {
            var request = base.CreateRequestMessage(requestUrl, content);
            if (!string.IsNullOrEmpty(_apiKey))
            {
                request.Headers.TryAddWithoutValidation("x-goog-api-key", _apiKey);
            }
            return request;
        }

        protected override string BuildRequestUrl()
        {
            string geminiModel = SocialInteractions.Settings.geminiModelName;
            if (string.IsNullOrEmpty(geminiModel))
            {
                geminiModel = "gemini-2.5-flash";
            }

            return string.Format("{0}/v1beta/models/{1}:generateContent", ApiUrl.TrimEnd('/'), geminiModel);
        }

        protected override string ExtractText(string responseBody)
        {
            GeminiApiResponse apiResponse = DeserializeJson<GeminiApiResponse>(responseBody);
            if (apiResponse != null && apiResponse.Candidates != null && apiResponse.Candidates.Length > 0)
            {
                var candidate = apiResponse.Candidates[0];
                if (candidate != null)
                {
                    if (candidate.FinishReason == "MAX_TOKENS")
                    {
                        SLog.Warning("[SocialInteractions] Gemini API response was truncated due to MAX_TOKENS. Consider increasing 'llmMaxTokens' in mod settings.");
                    }

                    if (candidate.Content != null && candidate.Content.Parts != null && candidate.Content.Parts.Count > 0)
                    {
                        return CleanChatResponse(candidate.Content.Parts[0].Text);
                    }
                }
            }

            return null;
        }
    }
}