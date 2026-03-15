using System.Collections.Generic;
using SocialInteractions;

namespace SocialInteractions.Tests.TestHelpers
{
    public class TestLogger : IModLogger
    {
        public List<string> Messages { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();

        public void Message(string text) => Messages.Add(text);
        public void Warning(string text) => Warnings.Add(text);
        public void Error(string text) => Errors.Add(text);

        public void Clear()
        {
            Messages.Clear();
            Warnings.Clear();
            Errors.Clear();
        }
    }
}
