using SocialInteractions;
using Xunit;

namespace SocialInteractions.Tests
{
    public class SmokeTests
    {
        [Fact]
        public void CanReferenceMainProjectTypes()
        {
            var apiType = LlmApiType.KoboldCpp;

            Assert.Equal("KoboldCpp", apiType.ToString());
        }
    }
}
