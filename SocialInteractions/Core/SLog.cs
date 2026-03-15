namespace SocialInteractions
{
    /// <summary>
    /// Centralized logging facade. Delegates to an IModLogger instance.
    /// When Logger is null (e.g. in tests before setup), calls are silent no-ops.
    /// </summary>
    public static class SLog
    {
        /// <summary>
        /// Set to a VerseLogger in production, or a TestLogger / NullLogger in tests.
        /// </summary>
        public static IModLogger Logger { get; set; }

        public static void Message(string text)
        {
            Logger?.Message(text);
        }

        public static void Warning(string text)
        {
            Logger?.Warning(text);
        }

        public static void Error(string text)
        {
            Logger?.Error(text);
        }
    }
}
