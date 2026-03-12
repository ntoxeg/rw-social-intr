using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SocialInteractions
{
    public class ChildrenMisbehaviorTracker_MapComponent : MapComponent
    {
        private int lastChildMisbehaviorCheckTick = 0;
        private const int ChildMisbehaviorCheckInterval = 600; // Check every 600 ticks (~1 minute)

        public ChildrenMisbehaviorTracker_MapComponent(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            // Only check for child misbehavior periodically to avoid performance impact
            if (Find.TickManager.TicksGame - lastChildMisbehaviorCheckTick >= ChildMisbehaviorCheckInterval)
            {
                ProcessChildrenMisbehavior();
                lastChildMisbehaviorCheckTick = Find.TickManager.TicksGame;
            }

            // Cleanup periodically
            if (Find.TickManager.TicksGame % 3000 == 0) // Every 3000 ticks
            {
                ChildrenMisbehaviorManager.Cleanup();
            }
        }

        private void ProcessChildrenMisbehavior()
        {
            // Iterate through all colonist children on the map, checking for misbehavior in a single pass
            // Use ToList() to create a copy of the collection to avoid "Collection was modified" exception
            foreach (Pawn pawn in map.mapPawns.FreeColonists.ToList())
            {
                // Validate pawn in single check
                if (pawn != null &&
                    pawn.ageTracker != null &&
                    ChildrenMisbehaviorManager.IsChild(pawn) &&  // Use existing IsChild method for consistency
                    pawn.RaceProps.Humanlike &&
                    !pawn.Dead &&
                    pawn.Spawned &&
                    pawn.Awake())
                {
                    float misbehaviorLevel;
                    if (ChildrenMisbehaviorManager.ShouldChildMisbehave(pawn, out misbehaviorLevel))
                    {
                        ChildrenMisbehaviorManager.ExecuteMisbehavior(pawn, misbehaviorLevel);
                    }
                }
            }
        }
    }
}