using SocialInteractions;
using SocialInteractions.Tests.TestHelpers;
using Xunit;

namespace SocialInteractions.Tests
{
    public class SLogTests
    {
        [Fact]
        public void Message_WithNullLogger_DoesNotThrow()
        {
            SLog.Logger = null;

            var ex = Record.Exception(() => SLog.Message("test"));

            Assert.Null(ex);
        }

        [Fact]
        public void Warning_WithTestLogger_CapturesMessage()
        {
            var logger = new TestLogger();
            SLog.Logger = logger;

            SLog.Warning("test-warning");

            Assert.Single(logger.Warnings);
            Assert.Equal("test-warning", logger.Warnings[0]);
        }
    }
}
