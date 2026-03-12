using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace SocialInteractions.Tests
{
    public class LlmClientFactoryTests
    {
        [Fact]
        public void Create_KoboldCpp_ReturnsKoboldApiClient()
        {
            AssertFactoryCase("KoboldCpp", "KoboldApiClient");
        }

        [Fact]
        public void Create_Ollama_ReturnsOllamaApiClient()
        {
            AssertFactoryCase("Ollama", "OllamaApiClient");
        }

        [Fact]
        public void Create_LMStudio_ReturnsLMStudioApiClient()
        {
            AssertFactoryCase("LMStudio", "LMStudioApiClient");
        }

        [Fact]
        public void Create_OpenAI_ReturnsOpenAiApiClient()
        {
            AssertFactoryCase("OpenAI", "OpenAiApiClient");
        }

        [Fact]
        public void Create_Gemini_ReturnsGeminiApiClient()
        {
            AssertFactoryCase("Gemini", "GeminiApiClient");
        }

        [Fact]
        public void Create_Qwen_ReturnsQwenApiClient()
        {
            AssertFactoryCase("Qwen", "QwenApiClient");
        }

        [Fact]
        public void Create_Deepseek_ReturnsDeepseekApiClient()
        {
            AssertFactoryCase("Deepseek", "DeepseekApiClient");
        }

        [Fact]
        public void Create_Grok_ReturnsGrokApiClient()
        {
            AssertFactoryCase("Grok", "GrokApiClient");
        }

        [Fact]
        public void Create_Claude_ReturnsClaudeApiClient()
        {
            AssertFactoryCase("Claude", "ClaudeApiClient");
        }

        [Fact]
        public void Create_Player2_ReturnsPlayer2ApiClient()
        {
            AssertFactoryCase("Player2", "Player2ApiClient");
        }

        private static void AssertFactoryCase(string apiTypeName, string expectedClientType)
        {
            string source = SourceFile.ReadApiFile("LlmClientFactory.cs");
            string pattern = string.Format(@"case\s+LlmApiType\.{0}\s*:\s*return\s+new\s+{1}\s*\(", apiTypeName, expectedClientType);

            Assert.Matches(pattern, source);
        }
    }

    public class ILlmClientContractTests
    {
        [Theory]
        [InlineData("KoboldApiClient.cs")]
        [InlineData("OllamaApiClient.cs")]
        [InlineData("LMStudioApiClient.cs")]
        [InlineData("OpenAiApiClient.cs")]
        [InlineData("GeminiApiClient.cs")]
        [InlineData("QwenApiClient.cs")]
        [InlineData("DeepseekApiClient.cs")]
        [InlineData("GrokApiClient.cs")]
        [InlineData("ClaudeApiClient.cs")]
        [InlineData("Player2ApiClient.cs")]
        public void EachClient_Name_IsNonEmpty(string clientFileName)
        {
            string source = SourceFile.ReadApiFile(clientFileName);
            Match nameMatch = Regex.Match(source, "public\\s+override\\s+string\\s+Name\\s*=>\\s*\"(?<name>[^\"]*)\"\\s*;");

            Assert.True(nameMatch.Success, "Name property was not found in " + clientFileName);
            Assert.False(string.IsNullOrWhiteSpace(nameMatch.Groups["name"].Value));
        }
    }

    public class LlmClientBaseErrorHandlingTests
    {
        [Fact]
        public void GenerateText_CatchesHttpRequestException_AndReturnsNull()
        {
            string source = SourceFile.ReadApiFile("LlmClientBase.cs");
            Assert.Matches(@"catch\s*\(\s*HttpRequestException\s+\w+\s*\)\s*\{[\s\S]*?return\s+null\s*;", source);
        }

        [Fact]
        public void GenerateText_CatchesTaskCanceledException_AndReturnsNull()
        {
            string source = SourceFile.ReadApiFile("LlmClientBase.cs");
            Assert.Matches(@"catch\s*\(\s*TaskCanceledException\s+\w+\s*\)\s*\{[\s\S]*?return\s+null\s*;", source);
        }

        [Fact]
        public void GenerateText_WhenResponseIsNotSuccess_ReturnsNull()
        {
            string source = SourceFile.ReadApiFile("LlmClientBase.cs");
            Assert.Matches(@"if\s*\(!response\.IsSuccessStatusCode\)\s*\{[\s\S]*?return\s+null\s*;", source);
        }
    }

    internal static class SourceFile
    {
        public static string ReadApiFile(string fileName)
        {
            string repoRoot = FindRepoRoot();
            string path = Path.Combine(repoRoot, "SocialInteractions", "Api", fileName);
            return File.ReadAllText(path);
        }

        private static string FindRepoRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current != null)
            {
                string socialInteractionsPath = Path.Combine(current.FullName, "SocialInteractions");
                string testsPath = Path.Combine(current.FullName, "SocialInteractions.Tests");

                if (Directory.Exists(socialInteractionsPath) && Directory.Exists(testsPath))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
        }
    }
}
