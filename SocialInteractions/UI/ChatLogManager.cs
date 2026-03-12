using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
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
        public string fallbackText; // Add fallback text for display in log

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

    public static class ChatLogManager
    {
        public static void AddMessage(ChatMessage message)
        {
            // Add the message to our own list
            chatLog.Add(message);
            // SLog.Message("[ChatLogManager] Added message to chat log: " + message.GetFormattedMessage());
        }
        
        // Method for adding date events with specific fallback texts
        public static void AddDateEvent(Pawn speaker, Pawn recipient, string message, string fallbackText)
        {
            ChatMessage chatMessage = new ChatMessage(speaker, recipient, message, MessageType.DateEvent, -1, new Color(1f, 0.7f, 0.7f), fallbackText); // Using pink color for dating/romance
            AddMessage(chatMessage);
        }
        
        // Method for adding game events with specific fallback texts
        public static void AddGameEvent(Pawn speaker, Pawn recipient, string message, string fallbackText)
        {
            ChatMessage chatMessage = new ChatMessage(speaker, recipient, message, MessageType.GameEvent, -1, Color.white, fallbackText);
            AddMessage(chatMessage);
        }
        
        // Method for adding drama events (like badmouthing) with specific fallback texts
        public static void AddDramaEvent(Pawn speaker, Pawn recipient, string message, string fallbackText)
        {
            ChatMessage chatMessage = new ChatMessage(speaker, recipient, message, MessageType.DramaEvent, -1, Color.red, fallbackText); // Using red color for drama
            AddMessage(chatMessage);
        }

        public static List<ChatMessage> GetChatLog()
        {
            // Filter out combat messages
            List<ChatMessage> filteredLog = new List<ChatMessage>();
            foreach (ChatMessage message in chatLog)
            {
                if (message.type != MessageType.CombatEvent)
                {
                    filteredLog.Add(message);
                }
            }
            return filteredLog;
        }

        public static void ClearChatLog()
        {
            chatLog.Clear();
        }

        public static int GetChatLogSize()
        {
            return chatLog.Count;
        }
        
        private static List<ChatMessage> chatLog = new List<ChatMessage>();
    }
}