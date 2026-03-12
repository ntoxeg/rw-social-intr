using Verse;

namespace SocialInteractions
{
    public static class SLog
    {
        public static void Message(string text)
        {
            if (SocialInteractions.Settings != null && SocialInteractions.Settings.verboseLogging)
            {
                Verse.Log.Message(text);
            }
        }

        public static void Warning(string text)
        {
            Verse.Log.Warning(text);
        }

        public static void Error(string text)
        {
            Verse.Log.Error(text);
        }
    }
}
