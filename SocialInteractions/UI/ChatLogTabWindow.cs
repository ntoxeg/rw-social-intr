using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace SocialInteractions
{
    public class ChatLogTabWindow : MainTabWindow
    {
        private class ConversationGroup
        {
            public int conversationId;
            public List<ChatMessage> messages;
            public DateTime timestamp;
            public Pawn speaker;
            public Pawn recipient;
            public string title;
            private string cachedFullConversation = null; // Cache the full conversation to avoid recalculating

            public ConversationGroup(int conversationId, List<ChatMessage> messages)
            {
                this.conversationId = conversationId;
                this.messages = messages;
                this.timestamp = messages[0].timestamp;
                this.speaker = messages[0].speaker;
                this.recipient = messages[0].recipient;

                // Create a title from the fallback text or first message
                if (!string.IsNullOrEmpty(messages[0].fallbackText))
                {
                    this.title = messages[0].fallbackText;
                }
                else
                {
                    this.title = string.Format("{0} -> {1}",
                        speaker != null ? speaker.Name.ToStringShort : "Unknown",
                        recipient != null ? recipient.Name.ToStringShort : "Unknown");
                }

                // Sort messages by timestamp only once during initialization
                this.messages.Sort((x, y) => x.timestamp.CompareTo(y.timestamp));
            }

            public string GetFullConversation()
            {
                // If we have a cached version, return it
                if (cachedFullConversation != null)
                {
                    return cachedFullConversation;
                }

                // Build the full conversation with formatted messages and spacing
                // Start with the title as the first line
                string conversation = title + "\n\n";
                for (int i = 0; i < messages.Count; i++)
                {
                    ChatMessage message = messages[i];
                    // Use formatted message if available, otherwise use raw message
                    string messageText = !string.IsNullOrEmpty(message.formattedMessage) ? message.formattedMessage : message.message;
                    conversation += messageText + "\n\n"; // Add extra line break for spacing
                }

                // Cache the result
                cachedFullConversation = conversation;
                return conversation;
            }

            // Method to invalidate the cache when messages change
            public void InvalidateCache()
            {
                cachedFullConversation = null;
            }
        }
        
        private Vector2 messagesScrollPos = Vector2.zero;
        private Vector2 detailsScrollPos = Vector2.zero;
        private float messagesLastHeight;
        private int displayedGroupIndex = -1;
        private int hoveredGroupIndex = -1;
        private static QuickSearchWidget quickSearchWidget = new QuickSearchWidget();

        // UI constants
        private const float MessagesRowHeight = 30f;
        private const float SpaceBetweenColumns = 5f;

        private static readonly Vector2 SearchBarOffset = new Vector2(720f, 8f);

        private Dictionary<string, string> truncationCache = new Dictionary<string, string>();
        private List<ConversationGroup> conversationGroups = new List<ConversationGroup>();
        private List<ChatMessage> lastChatLog = new List<ChatMessage>(); // Cache the last retrieved chat log
        private bool needsRefresh = true; // Flag to indicate if conversation groups need to be refreshed
        
        public override Vector2 RequestedTabSize { get { return new Vector2(1010f, 640f); } }
        
        public override void PreOpen()
        {
            base.PreOpen();
            quickSearchWidget.Reset();
        }
        
        public override void DoWindowContents(Rect rect)
        {
            // Draw the chat log page
            DoChatLogPage(rect);
        }

        private void DoChatLogPage(Rect rect)
        {
            // Adjust rect for search bar
            Rect contentRect = rect;
            Rect searchRect = new Rect(contentRect.x + SearchBarOffset.x, contentRect.y + SearchBarOffset.y, Window.QuickSearchSize.x, Window.QuickSearchSize.y);
            quickSearchWidget.OnGUI(searchRect, Notify_SearchChanged);

            // Adjust rect for content
            contentRect.yMin = contentRect.yMin + 40f;

            // Get chat log and update conversation groups only if needed
            List<ChatMessage> currentChatLog = ChatLogManager.GetChatLog();

            // Check if the chat log has changed since last time
            if (currentChatLog.Count != lastChatLog.Count)
            {
                needsRefresh = true;
            }
            else
            {
                // Quick check if any message has changed (this is a basic check; could be more thorough if needed)
                for (int i = 0; i < currentChatLog.Count; i++)
                {
                    if (!ReferenceEquals(currentChatLog[i], lastChatLog[i]))
                    {
                        needsRefresh = true;
                        break;
                    }
                }
            }

            // Only regroup and sort if we need to refresh
            if (needsRefresh)
            {
                lastChatLog = new List<ChatMessage>(currentChatLog); // Create a copy for comparison later
                GroupMessages(currentChatLog);
                // Sort by timestamp, newest first
                conversationGroups.Sort((x, y) => y.timestamp.CompareTo(x.timestamp));
                needsRefresh = false;
            }

            // Split the rect into two parts: left for the list, right for details
            Rect outRect = contentRect;
            Rect viewRect = new Rect(0f, 0f, contentRect.width / 2f - 16f, messagesLastHeight);
            Rect detailsRect = new Rect(contentRect.x + contentRect.width / 2f + 10f, contentRect.y, contentRect.width / 2f - 10f - 16f, contentRect.height);

            hoveredGroupIndex = -1;
            quickSearchWidget.noResultsMatched = conversationGroups.Count == 0;

            // Draw the list of conversation groups
            Widgets.BeginScrollView(outRect, ref messagesScrollPos, viewRect);
            float num = 0f;
            for (int i = 0; i < conversationGroups.Count; i++)
            {
                ConversationGroup group = conversationGroups[i];
                bool matchesSearch = !quickSearchWidget.filter.Active || quickSearchWidget.filter.Matches(group.title);

                if (matchesSearch)
                {
                    if (num + MessagesRowHeight >= messagesScrollPos.y && num <= messagesScrollPos.y + outRect.height)
                    {
                        DoConversationGroupRow(new Rect(0f, num, viewRect.width - 5f, MessagesRowHeight), group, i);
                    }
                    num += MessagesRowHeight;
                }
            }
            messagesLastHeight = num;
            Widgets.EndScrollView();

            // Draw the details of the selected or hovered conversation group
            ConversationGroup displayGroup = null;
            if (displayedGroupIndex >= 0 && displayedGroupIndex < conversationGroups.Count)
            {
                displayGroup = conversationGroups[displayedGroupIndex];
            }
            else if (hoveredGroupIndex >= 0 && hoveredGroupIndex < conversationGroups.Count)
            {
                displayGroup = conversationGroups[hoveredGroupIndex];
            }

            if (displayGroup != null)
            {
                string details = displayGroup.GetFullConversation();
                // Draw scrollable details panel
                Rect detailsViewRect = new Rect(0f, 0f, detailsRect.width - 16f, Text.CalcHeight(details, detailsRect.width - 16f));
                Widgets.BeginScrollView(detailsRect, ref detailsScrollPos, detailsViewRect);
                Widgets.Label(detailsViewRect, details);
                Widgets.EndScrollView();
            }
            else
            {
                Widgets.NoneLabel(contentRect.yMin + 3f, contentRect.width, "(" + "NoMessages".Translate() + ")");
            }
        }
        
        private void GroupMessages(List<ChatMessage> chatLog)
        {
            conversationGroups.Clear();

            // Group messages by conversationId
            // Include LLMChat, DramaEvent, and DateEvent messages in conversation grouping
            var groupedMessages = chatLog
                .Where(m => m.type == MessageType.LLMChat || m.type == MessageType.DramaEvent || m.type == MessageType.DateEvent)
                .GroupBy(m => m.conversationId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Create conversation groups
            foreach (var kvp in groupedMessages)
            {
                if (kvp.Value.Count > 0)
                {
                    conversationGroups.Add(new ConversationGroup(kvp.Key, kvp.Value));
                }
            }

            // Add non-chat messages as individual groups
            // Exclude LLMChat, DramaEvent, and DateEvent messages from individual grouping since they're conversation-based
            var nonChatMessages = chatLog.Where(m => m.type != MessageType.LLMChat && m.type != MessageType.DramaEvent && m.type != MessageType.DateEvent).ToList();
            foreach (ChatMessage message in nonChatMessages)
            {
                List<ChatMessage> singleMessageList = new List<ChatMessage> { message };
                // Use a hash of the message content as the conversation ID for individual messages
                int messageId = message.GetFormattedMessage().GetHashCode();
                conversationGroups.Add(new ConversationGroup(messageId, singleMessageList));
            }
        }
        
        private void DoConversationGroupRow(Rect rect, ConversationGroup group, int index)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = false;
            
            bool flag = quickSearchWidget.filter.Active && quickSearchWidget.filter.Matches(group.title);
            if (flag)
            {
                Widgets.DrawTextHighlight(rect, 0f);
                if (quickSearchWidget.filter.Active && quickSearchWidget.CurrentlyFocused())
                {
                    displayedGroupIndex = index;
                }
            }
            else if (index % 2 == 1)
            {
                Widgets.DrawLightHighlight(rect);
            }
            
            // Handle mouse hover
            if (Mouse.IsOver(rect))
            {
                hoveredGroupIndex = index;
            }
            
            Widgets.DrawHighlightIfMouseover(rect);
            
            // Draw timestamp
            Rect dateRect = rect;
            dateRect.width = 90f;
            GUI.color = new Color(0.75f, 0.75f, 0.75f);
            Widgets.Label(dateRect, group.timestamp.ToString("HH:mm:ss"));
            GUI.color = Color.white;
            
            // Draw message count
            Rect countRect = rect;
            countRect.x = dateRect.xMax + 5f;
            countRect.width = 30f;
            Widgets.Label(countRect, group.messages.Count.ToString());
            
            // Draw title
            Rect titleRect = rect;
            titleRect.xMin = countRect.xMax + 5f;
            titleRect.xMax -= 5f;
            
            GUI.color = group.messages[0].color;
            Widgets.Label(titleRect, group.title.Truncate(titleRect.width));
            GUI.color = Color.white;
            
            GenUI.ResetLabelAlign();
            Text.WordWrap = true;
            
            // Handle clicking on the row (select the entry)
            if (Widgets.ButtonInvisible(rect))
            {
                displayedGroupIndex = index;
                detailsScrollPos = Vector2.zero; // Reset scroll position when selecting a new entry
            }
        }
        
        private void Notify_SearchChanged()
        {
            messagesScrollPos = Vector2.zero;
            detailsScrollPos = Vector2.zero;
            // Force refresh when search changes
            needsRefresh = true;
        }
        
        public override void Notify_ClickOutsideWindow()
        {
            quickSearchWidget.Unfocus();
        }
    }
}