using SocialInteractions;

namespace SocialInteractions.Tests.TestHelpers
{
    public class NullLogger : IModLogger
    {
        public void Message(string text) { }
        public void Warning(string text) { }
        public void Error(string text) { }
    }
}
