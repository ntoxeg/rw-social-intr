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

            return Create(settings.llmApiType, settings);
        }

        public static ILlmClient Create(LlmApiType apiType, SocialInteractionsModSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            switch (apiType)
            {
                case LlmApiType.KoboldCpp:
                    return new KoboldApiClient(settings.llmApiUrl, settings.llmApiKey);
                case LlmApiType.Ollama:
                    return new OllamaApiClient(settings.llmApiUrl, settings.ollamaModelName);
                case LlmApiType.LMStudio:
                    return new LMStudioApiClient(settings.llmApiUrl, settings.lmStudioModelName);
                case LlmApiType.OpenAI:
                    return new OpenAiApiClient(settings.llmApiUrl, settings.openAiModelName, settings.llmApiKey);
                case LlmApiType.Gemini:
                    return new GeminiApiClient(settings.llmApiUrl, settings.llmApiKey);
                case LlmApiType.Qwen:
                    return new QwenApiClient(settings.llmApiUrl, settings.qwenModelName, settings.llmApiKey);
                case LlmApiType.Deepseek:
                    return new DeepseekApiClient(settings.llmApiUrl, settings.deepseekModelName, settings.llmApiKey);
                case LlmApiType.Grok:
                    return new GrokApiClient(settings.llmApiUrl, settings.grokModelName, settings.llmApiKey);
                case LlmApiType.Claude:
                    return new ClaudeApiClient(settings.llmApiUrl, settings.claudeModelName, settings.llmApiKey);
                case LlmApiType.Player2:
                    return new Player2ApiClient(settings.llmApiUrl, settings.player2ModelName, settings.llmApiKey, settings.player2GameClientId);
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

            bool isPlayer2 = settings.llmApiType == LlmApiType.Player2;
            bool hasClientId = !string.IsNullOrEmpty(settings.player2GameClientId);
            bool isEnabled = !requireLlmInteractionsEnabled || settings.llmInteractionsEnabled;

            if (isPlayer2 && hasClientId && isEnabled)
            {
                Player2ApiClient.StartHealthHeartbeat(settings.llmApiUrl, settings.player2GameClientId);
            }
            else
            {
                Player2ApiClient.StopHealthHeartbeat();
            }
        }
    }
}
