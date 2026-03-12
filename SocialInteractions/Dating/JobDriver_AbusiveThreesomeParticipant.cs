using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JobDriver_AbusiveThreesomeParticipant : JobDriver
    {
        private Pawn Abuser
        {
            get { return (Pawn)this.job.targetA.Thing; }
        }

        private Pawn Victim
        {
            get { return (Pawn)this.job.targetB.Thing; }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // No reservation needed, we are being "used"
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {

            // Wait while the abuser does their things
            Toil wait = ToilMaker.MakeToil("Wait");
            wait.tickAction = () =>
            {
                if (Abuser == null || Abuser.Dead || Abuser.Downed ||
                    Abuser.CurJobDef == null || Abuser.CurJobDef.defName != "AbusiveThreesome")
                {
                    string reason = "Unknown";
                    if (Abuser == null) reason = "Abuser is null";
                    else if (Abuser.Dead) reason = "Abuser is dead";
                    else if (Abuser.Downed) reason = "Abuser is downed";
                    else if (Abuser.CurJobDef == null) reason = "Abuser CurJobDef is null";
                    else reason = "Abuser CurJobDef is " + Abuser.CurJobDef.defName;

                    SLog.Message(string.Format("[SocialInteractions] JobDriver_AbusiveThreesomeParticipant: {0} ending job because abuser {1} is no longer abusing. Reason: {2}", 
                        this.pawn.LabelShort, Abuser != null ? Abuser.LabelShort : "NULL", reason));
                    
                    this.EndJobWith(JobCondition.Succeeded);
                    return;
                }

                // Follow Abuser spot logic (if we aren't at target cell yet)
                if (this.pawn.IsHashIntervalTick(10) && this.job.targetC.IsValid)
                {
                    if (this.pawn.Position != this.job.targetC.Cell && !this.pawn.pather.Moving)
                    {
                        this.pawn.pather.StartPath(this.job.targetC, PathEndMode.OnCell);
                    }
                }

                // Face the victim or the abuser
                if (Victim != null && this.pawn != Victim)
                {
                    this.pawn.rotationTracker.FaceCell(Victim.Position);
                }
                else if (Abuser != null)
                {
                    this.pawn.rotationTracker.FaceCell(Abuser.Position);
                }

                // Throw heart flecks occasionally, synced with abuser
                if (this.pawn.IsHashIntervalTick(100))
                {
                    if (this.pawn.Position != null && this.pawn.Map != null)
                    {
                        FleckMaker.ThrowMetaIcon(this.pawn.Position, this.pawn.Map, FleckDefOf.Heart);
                    }
                }
            };
            wait.defaultCompleteMode = ToilCompleteMode.Never;
            wait.socialMode = RandomSocialMode.Off;
            yield return wait;
        }

        public override Vector3 ForcedBodyOffset
        {
            get
            {
                if (pawn == null || Abuser == null)
                {
                    return Vector3.zero;
                }

                if (Abuser.jobs == null)
                {
                    return Vector3.zero;
                }

                JobDriver_AbusiveThreesome abuserDriver = Abuser.jobs.curDriver as JobDriver_AbusiveThreesome;
                if (abuserDriver == null || abuserDriver.ticksLeft <= 0)
                {
                    return Vector3.zero;
                }

                int totalTicks = SocialInteractions.Settings.dateLovinTicks;
                if (totalTicks <= 0) return Vector3.zero;

                float progress = 1.0f - ((float)abuserDriver.ticksLeft / totalTicks);

                float animationSpeed = 1.0f;
                if (progress <= 0.90f)
                {
                    animationSpeed = 1.0f + (progress / 0.90f) * 0.75f;
                }
                else
                {
                    animationSpeed = 0.3f;
                }
                
                float baseTime = progress * 8.0f * (totalTicks / 60.0f);
                
                if (this.pawn == Victim)
                {
                    // Victim bounces vertically (standard lovin half-sine pulse)
                    float adjustedTime = baseTime * animationSpeed;
                    float num = Mathf.Sin(adjustedTime);
                    float z = Mathf.Max(Mathf.Pow((num + 1f) * 0.5f, 2f) * 0.2f - 0.06f, 0f);
                    return new Vector3(0f, 0f, z);
                }
                else
                {
                    // Partner bounces horizontally (exact standard lovin formula) with phase offset
                    float adjustedTime = (baseTime * animationSpeed) + 1.5f; // Fixed phase offset
                    float num = Mathf.Sin(adjustedTime);
                    float num2 = Mathf.Sign(num);
                    float x = EaseInOutQuad(Mathf.Abs(num) * 0.6f) * 0.09f * num2;
                    return new Vector3(x, 0f, 0f);
                }
            }
        }

        private float EaseInOutQuad(float v)
        {
            if (!((double)v < 0.5))
            {
                return 1f - Mathf.Pow(-2f * v + 2f, 4f) / 2f;
            }
            return 8f * v * v * v * v;
        }
    }
}
