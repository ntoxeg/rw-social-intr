using System;
using System.Collections.Generic;

namespace SocialInteractions.Api
{
    /// <summary>
    /// Plain-old-data carrier for LLM generation parameters.
    /// Constructed once from mod settings; consumed by API clients
    /// without any direct dependency on the settings or Unity types.
    /// </summary>
    public class LlmClientConfig
    {
        public int MaxTokens { get; set; } = 1024;
        public float Temperature { get; set; } = 0.7f;
        public int TopK { get; set; } = 40;
        public float TopP { get; set; } = 1.0f;
        public float MinP { get; set; } = 0.05f;
        public float RepetitionPenalty { get; set; } = 1.0f;
        public bool EnableXtcSampling { get; set; }
        public bool DisableThinking { get; set; } = true;
        public bool ForceChatCompletion { get; set; } = true;
        public string GeminiModelName { get; set; } = "";
        public List<string> DefaultStopSequences { get; set; } = new List<string>();

        /// <summary>
        /// Builds a config from live mod settings. Only call site that touches
        /// SocialInteractionsModSettings — keeps the dependency in one place.
        /// </summary>
        public static LlmClientConfig FromSettings(SocialInteractionsModSettings settings)
        {
            if (settings == null)
            {
                return new LlmClientConfig();
            }

            var api = settings.Api;
            var config = new LlmClientConfig
            {
                MaxTokens = api.llmMaxTokens,
                Temperature = api.llmTemperature,
                TopK = api.llmTopK,
                TopP = api.llmTopP,
                MinP = api.llmMinP,
                RepetitionPenalty = api.llmRepetitionPenalty,
                EnableXtcSampling = api.enableXtcSampling,
                DisableThinking = api.disableLlmThinking,
                ForceChatCompletion = api.forceChatCompletion,
                GeminiModelName = api.geminiModelName
            };

            if (!string.IsNullOrEmpty(settings.Prompts?.llmStoppingStrings))
            {
                config.DefaultStopSequences = new List<string>(
                    settings.Prompts.llmStoppingStrings.Split(
                        new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            }

            return config;
        }
    }
}
