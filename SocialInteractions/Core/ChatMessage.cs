using System;
using Verse;
using UnityEngine;

namespace SocialInteractions
{
    public enum MessageType
    {
        LLMChat,
        GameEvent,
        DateEvent,
        CombatEvent,
        DramaEvent
    }

    public class ChatMessage
    {
        public Pawn speaker;
        public Pawn recipient;
        public string message;
        public string formattedMessage;
        public DateTime timestamp;
        public MessageType type;
        public int conversationId;
        public Color color;
        public string fallbackText;

        public ChatMessage(Pawn speaker, Pawn recipient, string message, MessageType type, int conversationId = -1, Color? color = null, string fallbackText = null, string formattedMessage = null)
        {
            this.speaker = speaker;
            this.recipient = recipient;
            this.message = message;
            this.formattedMessage = formattedMessage;
            this.timestamp = DateTime.Now;
            this.type = type;
            this.conversationId = conversationId;
            this.color = color ?? Color.white;
            this.fallbackText = fallbackText;
        }

        public string GetFormattedMessage()
        {
            string speakerName = speaker != null ? speaker.Name.ToStringShort : "Unknown";
            string recipientName = recipient != null ? recipient.Name.ToStringShort : "Unknown";

            string prefix = "";
            switch (type)
            {
                case MessageType.LLMChat:
                    prefix = "[Chat]";
                    break;
                case MessageType.GameEvent:
                    prefix = "[Event]";
                    break;
                case MessageType.DateEvent:
                    prefix = "[Date]";
                    break;
                case MessageType.CombatEvent:
                    prefix = "[Combat]";
                    break;
                case MessageType.DramaEvent:
                    prefix = "[Drama]";
                    break;
            }

            return string.Format("{0} {1} {2} -> {3}: {4}",
                timestamp.ToString("HH:mm:ss"),
                prefix,
                speakerName,
                recipientName,
                message);
        }
    }
}
