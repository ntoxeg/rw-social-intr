using RimWorld;
using Verse;
using System.Collections.Generic;
using UnityEngine;

namespace SocialInteractions
{
    /// <summary>
    /// GameComponent to handle persistent negotiation cooldowns for pawns and factions.
    /// </summary>
    public class NegotiationCooldown_GameComponent : GameComponent
    {
        // Dictionaries to store cooldown expiration ticks
        // Key: Pawn.thingIDNumber or Faction.loadID
        private Dictionary<int, int> pawnCooldowns = new Dictionary<int, int>();
        private Dictionary<int, int> factionCooldowns = new Dictionary<int, int>();

        public NegotiationCooldown_GameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pawnCooldowns, "pawnCooldowns", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref factionCooldowns, "factionCooldowns", LookMode.Value, LookMode.Value);

            if (pawnCooldowns == null) pawnCooldowns = new Dictionary<int, int>();
            if (factionCooldowns == null) factionCooldowns = new Dictionary<int, int>();
        }

        public void SetCooldown(Pawn pawn, float hours)
        {
            if (pawn == null) return;
            int durationTicks = Mathf.RoundToInt(hours * 2500f);
            pawnCooldowns[pawn.thingIDNumber] = Find.TickManager.TicksGame + durationTicks;
            SLog.Message(string.Format("[Negotiation] Set cooldown for pawn {0} for {1} hours ({2} ticks)", pawn.LabelShort, hours, durationTicks));
        }

        public void SetCooldown(Faction faction, float hours)
        {
            if (faction == null) return;
            int durationTicks = Mathf.RoundToInt(hours * 2500f);
            factionCooldowns[faction.loadID] = Find.TickManager.TicksGame + durationTicks;
            SLog.Message(string.Format("[Negotiation] Set cooldown for faction {0} for {1} hours ({2} ticks)", faction.Name, hours, durationTicks));
        }

        public bool IsOnCooldown(Pawn pawn, Faction faction, out string reason)
        {
            reason = string.Empty;
            int ticksGame = Find.TickManager.TicksGame;

            // Check pawn cooldown
            int pawnTick;
            if (pawn != null && pawnCooldowns.TryGetValue(pawn.thingIDNumber, out pawnTick))
            {
                if (ticksGame < pawnTick)
                {
                    float hoursRemaining = (pawnTick - ticksGame) / 2500f;
                    reason = string.Format("{0} is not ready to negotiate again yet ({1:F1}h remaining).", pawn.LabelShort, hoursRemaining);
                    return true;
                }
                else
                {
                    pawnCooldowns.Remove(pawn.thingIDNumber);
                }
            }

            // Check faction cooldown
            int factionTick;
            if (faction != null && !faction.IsPlayer && factionCooldowns.TryGetValue(faction.loadID, out factionTick))
            {
                if (ticksGame < factionTick)
                {
                    float hoursRemaining = (factionTick - ticksGame) / 2500f;
                    reason = string.Format("Faction {0} is not interested in further negotiations right now ({1:F1}h remaining).", faction.Name, hoursRemaining);
                    return true;
                }
                else
                {
                    factionCooldowns.Remove(faction.loadID);
                }
            }

            return false;
        }

        public float GetHoursRemaining(int id, bool isFaction)
        {
            int ticksGame = Find.TickManager.TicksGame;
            int expireTick = 0;
            
            if (isFaction)
            {
                if (!factionCooldowns.TryGetValue(id, out expireTick)) return 0f;
            }
            else
            {
                if (!pawnCooldowns.TryGetValue(id, out expireTick)) return 0f;
            }

            if (expireTick <= ticksGame) return 0f;
            return (expireTick - ticksGame) / 2500f;
        }
    }
}
