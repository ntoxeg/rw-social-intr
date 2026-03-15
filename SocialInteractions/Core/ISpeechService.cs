using System;
using Verse;
using UnityEngine;

namespace SocialInteractions
{
    /// <summary>
    /// Abstraction over SpeechBubbleManager so subsystems (Dating, Combat,
    /// Negotiation) can request speech without a direct dependency on the
    /// Speech namespace.
    /// </summary>
    public interface ISpeechService
    {
        // --- Conversation lifecycle ---
        int StartConversation();
        int GetNextConversationId();
        void EndConversation(int conversationId);
        bool IsConversationActive(int conversationId);
        bool IsLlmCurrentlyBusy();
        bool HasPendingSpeechBubbles(int conversationId);
        bool HasPendingSpeechBubblesForPawn(Pawn pawn);
        bool HasActiveConversations();
        void ClearQueues();
        void EnqueueJob(Action jobAction);

        // --- Display (combat taunts, negotiation) ---
        void EnqueueInstant(Pawn speaker, string text, float duration,
            Color? color = null, bool useCustomMote = false);

        // --- Utility ---
        float EstimateReadingTime(string text);

        // --- Date-related subject formatters ---
        string GetDateSubject(Pawn initiator, Pawn recipient, LocalTargetInfo joySpot);
        string GetDateRejectionSubject(Pawn initiator, Pawn recipient);
        string GetDateEndSubject(Pawn initiator, Pawn recipient);
        string GetDateLovinSubject(Pawn initiator, Pawn recipient);
        string GetPostDateLovinSubject(Pawn initiator, Pawn recipient);
        string GetDateWentBadlySubject(Pawn initiator, Pawn recipient);
    }
}
