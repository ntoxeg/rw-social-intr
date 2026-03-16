using System;
using Verse;

namespace SocialInteractions
{
    /// <summary>
    /// Lightweight service locator for cross-namespace decoupling.
    /// Populated by GameComponent constructors, consumed via null-conditional access.
    /// </summary>
    public static class Services
    {
        public static ISpeechService Speech { get; set; }
        public static IChatLog ChatLog { get; set; }

        /// <summary>
        /// Pawn memory system for storing and managing pawn memories and daily event buffers.
        /// Set by the PawnMemory_GameComponent during initialization.
        /// </summary>
        public static Memory.PawnMemory_GameComponent Memory { get; set; }

        /// <summary>
        /// Resolves the partner a pawn is currently on a date with, if any.
        /// Set by the Dating subsystem during initialization.
        /// </summary>
        public static Func<Pawn, Pawn> GetDatePartner { get; set; }

        public static void Reset()
        {
            Speech = null;
            ChatLog = null;
            Memory = null;
            GetDatePartner = null;
        }
    }
}
