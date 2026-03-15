using System.Collections.Generic;
using SocialInteractions.Api;
using SocialInteractions.Tests.TestHelpers;
using Xunit;

namespace SocialInteractions.Tests
{
    public class LlmClientBaseTests : ApiTestBase
    {
        [Fact]
        public void CleanChatResponse_RemovesThinkingTags()
        {
            const string input = "<thinking>analysis</thinking>Visible <think>hidden</think> text [thinking]more[/thinking]";
            string cleaned = TestableLlmClientBase.InvokeCleanChatResponse(input);

            Assert.Equal("Visible  text", cleaned);
        }

        [Fact]
        public void CleanChatResponse_EmptyString_RemainsEmpty()
        {
            string cleaned = TestableLlmClientBase.InvokeCleanChatResponse(string.Empty);

            Assert.Equal(string.Empty, cleaned);
        }

        [Fact]
        public void CleanChatResponse_Null_RemainsNull()
        {
            string cleaned = TestableLlmClientBase.InvokeCleanChatResponse(null);

            Assert.Null(cleaned);
        }

        [Fact]
        public void CleanChatResponse_NestedTags_AreRemovedWithOuterBlock()
        {
            const string input = "<thinking>outer <think>inner</think> block</thinking>Final line";
            string cleaned = TestableLlmClientBase.InvokeCleanChatResponse(input);

            Assert.Equal("Final line", cleaned);
        }

        [Theory]
        [InlineData("abc123", true)]
        [InlineData("Bearer token-with-dash", true)]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("bad\rvalue", false)]
        [InlineData("bad\nvalue", false)]
        public void IsValidHeaderValue_ValidatesExpectedCases(string value, bool expected)
        {
            bool actual = TestableLlmClientBase.InvokeIsValidHeaderValue(value);

            Assert.Equal(expected, actual);
        }

        private sealed class TestableLlmClientBase : LlmClientBase
        {
            public TestableLlmClientBase() : base("http://test", new LlmClientConfig())
            {
            }

            public override string Name => "Test";

            protected override object BuildRequestBody(string prompt, int? maxLength, float? temperature, List<string> stopSequence, bool? enableXtcSampling, int? topK, float? topP, float? minP, float? repetitionPenalty)
            {
                return new object();
            }

            protected override string ExtractText(string responseBody)
            {
                return responseBody;
            }

            public static string InvokeCleanChatResponse(string response)
            {
                return CleanChatResponse(response);
            }

            public static bool InvokeIsValidHeaderValue(string value)
            {
                return IsValidHeaderValue(value);
            }
        }
    }
}
