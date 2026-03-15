using Verse;
using UnityEngine;

namespace SocialInteractions
{
    /// <summary>
    /// Abstraction over ChatLogManager so subsystems can log messages
    /// without a direct dependency on the UI namespace.
    /// </summary>
    public interface IChatLog
    {
        void AddMessage(ChatMessage message);
        void AddDateEvent(Pawn speaker, Pawn recipient, string message, string fallbackText);
        void AddGameEvent(Pawn speaker, Pawn recipient, string message, string fallbackText);
        void AddDramaEvent(Pawn speaker, Pawn recipient, string message, string fallbackText);
        void ClearChatLog();
    }
}
