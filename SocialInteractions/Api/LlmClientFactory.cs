using System;

namespace SocialInteractions.Api
{
    public static class LlmClientFactory
    {
        public static ILlmClient Create(SocialInteractionsModSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            return Create(settings.Api.llmApiType, settings);
        }

        public static ILlmClient Create(LlmApiType apiType, SocialInteractionsModSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            var config = LlmClientConfig.FromSettings(settings);

            switch (apiType)
            {
                case LlmApiType.KoboldCpp:
                    return new KoboldApiClient(settings.Api.llmApiUrl, settings.Api.llmApiKey, config);
                case LlmApiType.Ollama:
                    return new OllamaApiClient(settings.Api.llmApiUrl, settings.Api.ollamaModelName, config);
                case LlmApiType.LMStudio:
                    return new LMStudioApiClient(settings.Api.llmApiUrl, settings.Api.lmStudioModelName, config);
                case LlmApiType.OpenAI:
                    return new OpenAiApiClient(settings.Api.llmApiUrl, settings.Api.openAiModelName, settings.Api.llmApiKey, config);
                case LlmApiType.Gemini:
                    return new GeminiApiClient(settings.Api.llmApiUrl, settings.Api.llmApiKey, config);
                case LlmApiType.Qwen:
                    return new QwenApiClient(settings.Api.llmApiUrl, settings.Api.qwenModelName, settings.Api.llmApiKey, config);
                case LlmApiType.Deepseek:
                    return new DeepseekApiClient(settings.Api.llmApiUrl, settings.Api.deepseekModelName, settings.Api.llmApiKey, config);
                case LlmApiType.Grok:
                    return new GrokApiClient(settings.Api.llmApiUrl, settings.Api.grokModelName, settings.Api.llmApiKey, config);
                case LlmApiType.Claude:
                    return new ClaudeApiClient(settings.Api.llmApiUrl, settings.Api.claudeModelName, settings.Api.llmApiKey, config);
                case LlmApiType.Player2:
                    return new Player2ApiClient(settings.Api.llmApiUrl, settings.Api.player2ModelName, settings.Api.llmApiKey, settings.Api.player2GameClientId, config);
                default:
                    throw new ArgumentOutOfRangeException("apiType", apiType, "Unknown API type");
            }
        }

        public static void UpdatePlayer2Heartbeat(SocialInteractionsModSettings settings, bool requireLlmInteractionsEnabled = false)
        {
            if (settings == null)
            {
                Player2ApiClient.StopHealthHeartbeat();
                return;
            }

            bool isPlayer2 = settings.Api.llmApiType == LlmApiType.Player2;
            bool hasClientId = !string.IsNullOrEmpty(settings.Api.player2GameClientId);
            bool isEnabled = !requireLlmInteractionsEnabled || settings.Features.llmInteractionsEnabled;

            if (isPlayer2 && hasClientId && isEnabled)
            {
                Player2ApiClient.StartHealthHeartbeat(settings.Api.llmApiUrl, settings.Api.player2GameClientId);
            }
            else
            {
                Player2ApiClient.StopHealthHeartbeat();
            }
        }
    }
}
