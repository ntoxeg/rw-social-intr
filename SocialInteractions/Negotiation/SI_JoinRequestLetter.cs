using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace SocialInteractions
{
    /// <summary>
    /// A choice letter for when a visitor wants to join the colony.
    /// Provides Accept and Reject options.
    /// </summary>
    public class SI_JoinRequestLetter : ChoiceLetter
    {
        public Pawn joiner;

        public override bool CanDismissWithRightClick { get { return false; } }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly)
                {
                    yield return Option_Close;
                    yield break;
                }

                // Accept option
                DiaOption accept = new DiaOption("AcceptButton".Translate());
                accept.action = delegate
                {
                    if (joiner != null && joiner.Faction != Faction.OfPlayer)
                    {
                        joiner.SetFaction(Faction.OfPlayer);
                        Messages.Message("SI_PawnJoinedColony".Translate(joiner.LabelShort), joiner, MessageTypeDefOf.PositiveEvent);
                    }
                    Find.LetterStack.RemoveLetter(this);
                };
                accept.resolveTree = true;
                if (Find.AnyPlayerHomeMap == null)
                {
                    accept.Disable("CannotAcceptQuestNoMap".Translate());
                }

                // Reject option
                DiaOption reject = new DiaOption("RejectLetter".Translate());
                reject.action = delegate
                {
                    Find.LetterStack.RemoveLetter(this);
                };
                reject.resolveTree = true;

                yield return accept;
                yield return reject;
                
                if (lookTargets.IsValid())
                {
                    yield return Option_JumpToLocationAndPostpone;
                }
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref joiner, "joiner");
        }
    }
}
