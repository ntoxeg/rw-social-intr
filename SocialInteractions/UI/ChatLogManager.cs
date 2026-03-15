using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;
using SocialInteractions;

namespace SocialInteractions.UI
{
    // ChatMessage and MessageType have been moved to SocialInteractions namespace
    // (Core/ChatMessage.cs) to eliminate lateral coupling from Speech/Negotiation → UI.

    public class ChatLogManager : GameComponent, IChatLog
    {
        public static ChatLogManager Current => Verse.Current.Game?.GetComponent<ChatLogManager>();

        private readonly List<ChatMessage> chatLog = new List<ChatMessage>();

        public ChatLogManager(Game game)
        {
            Services.ChatLog = this;
        }

        public void AddMessage(ChatMessage message)
        {
            // Add the message to our own list
            chatLog.Add(message);
            // SLog.Message("[ChatLogManager] Added message to chat log: " + message.GetFormattedMessage());
        }

        // Method for adding date events with specific fallback texts
        public void AddDateEvent(Pawn speaker, Pawn recipient, string message, string fallbackText)
        {
            ChatMessage chatMessage = new ChatMessage(speaker, recipient, message, MessageType.DateEvent, -1, new Color(1f, 0.7f, 0.7f), fallbackText); // Using pink color for dating/romance
            AddMessage(chatMessage);
        }

        // Method for adding game events with specific fallback texts
        public void AddGameEvent(Pawn speaker, Pawn recipient, string message, string fallbackText)
        {
            ChatMessage chatMessage = new ChatMessage(speaker, recipient, message, MessageType.GameEvent, -1, Color.white, fallbackText);
            AddMessage(chatMessage);
        }

        // Method for adding drama events (like badmouthing) with specific fallback texts
        public void AddDramaEvent(Pawn speaker, Pawn recipient, string message, string fallbackText)
        {
            ChatMessage chatMessage = new ChatMessage(speaker, recipient, message, MessageType.DramaEvent, -1, Color.red, fallbackText); // Using red color for drama
            AddMessage(chatMessage);
        }

        public IReadOnlyList<ChatMessage> GetChatLog()
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

        public void ClearChatLog()
        {
            chatLog.Clear();
        }

        public int GetChatLogSize()
        {
            return chatLog.Count;
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }
    }
}
