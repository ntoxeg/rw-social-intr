namespace SocialInteractions
{
    /// <summary>
    /// Abstraction over logging to enable testability.
    /// Production implementation delegates to Verse.Log.
    /// </summary>
    public interface IModLogger
    {
        void Message(string text);
        void Warning(string text);
        void Error(string text);
    }
}
