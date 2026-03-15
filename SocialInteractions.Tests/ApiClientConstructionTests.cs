using SocialInteractions.Api;
using SocialInteractions.Tests.TestHelpers;
using Xunit;

namespace SocialInteractions.Tests
{
    public class ApiClientConstructionTests : ApiTestBase
    {
        [Fact]
        public void KoboldApiClient_CanConstruct()
        {
            var config = new LlmClientConfig();
            var client = new KoboldApiClient("http://test:5001", "test-key", config);
            Assert.Equal("KoboldCpp", client.Name);
        }

        [Fact]
        public void OllamaApiClient_CanConstruct()
        {
            var config = new LlmClientConfig();
            var client = new OllamaApiClient("http://test:11434", "test-model", config);
            Assert.Equal("Ollama", client.Name);
        }

        [Fact]
        public void LMStudioApiClient_CanConstruct()
        {
            var config = new LlmClientConfig();
            var client = new LMStudioApiClient("http://test:1234", "test-model", config);
            Assert.Equal("LMStudio", client.Name);
        }

        [Fact]
        public void OpenAiApiClient_CanConstruct()
        {
            var config = new LlmClientConfig();
            var client = new OpenAiApiClient("http://test:8000", "test-model", "test-key", config);
            Assert.Equal("OpenAI", client.Name);
        }

        [Fact]
        public void GeminiApiClient_CanConstruct()
        {
            var config = new LlmClientConfig();
            var client = new GeminiApiClient("http://test:8001", "test-key", config);
            Assert.Equal("Gemini", client.Name);
        }

        [Fact]
        public void QwenApiClient_CanConstruct()
        {
            var config = new LlmClientConfig();
            var client = new QwenApiClient("http://test:8002", "test-model", "test-key", config);
            Assert.Equal("Qwen", client.Name);
        }

        [Fact]
        public void DeepseekApiClient_CanConstruct()
        {
            var config = new LlmClientConfig();
            var client = new DeepseekApiClient("http://test:8003", "test-model", "test-key", config);
            Assert.Equal("Deepseek", client.Name);
        }

        [Fact]
        public void GrokApiClient_CanConstruct()
        {
            var config = new LlmClientConfig();
            var client = new GrokApiClient("http://test:8004", "test-model", "test-key", config);
            Assert.Equal("Grok", client.Name);
        }

        [Fact]
        public void ClaudeApiClient_CanConstruct()
        {
            var config = new LlmClientConfig();
            var client = new ClaudeApiClient("http://test:8005", "test-model", "test-key", config);
            Assert.Equal("Claude", client.Name);
        }

        [Fact]
        public void Player2ApiClient_CanConstruct()
        {
            var config = new LlmClientConfig();
            var client = new Player2ApiClient("http://test:8006", "test-model", "test-key", "test-game-client", config);
            Assert.Equal("Player2", client.Name);
        }
    }
}
