using System;
using Verse;

namespace SocialInteractions
{
    /// <summary>
    /// Production IModLogger that delegates to Verse.Log.
    /// Message calls are gated by a verbose-logging check.
    /// </summary>
    internal class VerseLogger : IModLogger
    {
        private readonly Func<bool> _verboseCheck;

        public VerseLogger(Func<bool> verboseCheck)
        {
            _verboseCheck = verboseCheck ?? throw new ArgumentNullException("verboseCheck");
        }

        public void Message(string text)
        {
            if (_verboseCheck())
            {
                Log.Message(text);
            }
        }

        public void Warning(string text)
        {
            Log.Warning(text);
        }

        public void Error(string text)
        {
            Log.Error(text);
        }
    }
}
