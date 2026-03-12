using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SocialInteractions
{
    public class JobDriver_ChildPlayWithWeapon : JobDriver
    {
        private const int BaseWeaponPlayDuration = 1800; // 30 seconds in ticks

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            SLog.Message(string.Format("[SocialInteractions] JobDriver_ChildPlayWithWeapon: TryMakePreToilReservations for {0}", pawn.LabelShort));
            // Child should be able to reserve the weapon
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail if the weapon is destroyed or null
            this.FailOnDestroyedOrNull(TargetIndex.A);
            
            // Fail if the weapon is forbidden (only if spawned)
            this.FailOn(() => {
                Thing weapon = job.GetTarget(TargetIndex.A).Thing;
                if (weapon != null && weapon.Spawned && weapon.IsForbidden(pawn))
                {
                    return true;
                }
                return false;
            });

            // Fail if child is captured or recruited to another faction
            this.FailOn(() => pawn.HostFaction != null || (pawn.Faction != null && pawn.Faction != Faction.OfPlayer));
            // Fail if child gets drafted
            this.FailOn(() => pawn.Drafted);

            // If the child doesn't have the weapon equipped, go pick it up
            if (pawn.equipment.Primary != job.GetTarget(TargetIndex.A).Thing)
            {
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
                yield return Toils_General.Do(delegate
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_ChildPlayWithWeapon: Picking up weapon for {0}", pawn.LabelShort));
                    Thing weapon = job.GetTarget(TargetIndex.A).Thing;
                    if (weapon != null && weapon.Spawned)
                    {
                        ThingWithComps weaponComp = weapon as ThingWithComps;
                        if (weaponComp != null)
                        {
                            pawn.equipment.MakeRoomFor(weaponComp);
                            // DeSpawn the weapon from the map before adding it to the equipment tracker
                            // This resolves the "already in another container" error
                            if (weaponComp.Spawned)
                            {
                                weaponComp.DeSpawn();
                            }
                            pawn.equipment.AddEquipment(weaponComp);
                        }
                    }
                });
            }

            // Create the main weapon play toil where the child shoots at something
            Toil findTargetAndShootToil = new Toil();
            findTargetAndShootToil.initAction = delegate
            {
                Thing weapon = pawn.equipment.Primary;

                if (weapon == null || !weapon.def.IsRangedWeapon)
                {
                    SLog.Warning("[SocialInteractions] JobDriver_ChildPlayWithWeapon: No ranged weapon equipped, ending job");
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Find a random target to shoot at
                Thing target = FindRandomTarget(pawn, weapon);
                
                if (target != null)
                {
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_ChildPlayWithWeapon: Child {0} is shooting at {1} with {2}", 
                        pawn.LabelShort, target.Label, weapon.Label));

                    // Trigger LLM interaction about playing with the weapon
                    string subject = string.Format("playing with {0} and going to shoot at {1}!", weapon.Label, target.Label);
                    SocialInteractions.HandleMonologue(pawn, subject);

                    // Add reckless thought to the child
                    if (pawn.needs != null && pawn.needs.mood != null)
                    {
                        pawn.needs.mood.thoughts.memories.TryGainMemory(ChildThoughtDefOf.ChildReckless, null);
                    }

                    // Show message to player about the dangerous weapon play
                    Messages.Message(string.Format("{0} (child) is shooting at {1} with {2}!", pawn.LabelShort, target.Label, weapon.Label),
                        new LookTargets(pawn, target), MessageTypeDefOf.ThreatBig);

                    // Chance of self-harm (gun discharging)
                    if (Rand.Value < 0.2f) // 20% chance
                    {
                        SLog.Message(string.Format("[SocialInteractions] JobDriver_ChildPlayWithWeapon: Weapon discharged and hurt child {0}", pawn.LabelShort));
                        Messages.Message(string.Format("{0}'s weapon discharged and hurt them!", pawn.LabelShort),
                            new LookTargets(pawn), MessageTypeDefOf.NegativeEvent);
                        
                        // Deal damage to child - High AP to ensure it hurts
                        DamageInfo dinfo = new DamageInfo(DamageDefOf.Bullet, 10f, 999f, -1f, pawn, null, weapon.def);
                        pawn.TakeDamage(dinfo);
                        SLog.Message(string.Format("[SocialInteractions] Applied 10 damage to {0}", pawn.LabelShort));
                    }
                    else
                    {
                        // Shoot at the target
                        Verb attackVerb = pawn.TryGetAttackVerb(target, !pawn.IsColonist);
                        if (attackVerb != null && !attackVerb.verbProps.IsMeleeAttack)
                        {
                            attackVerb.TryStartCastOn(target);
                        }
                    }
                }
                else
                {
                    // No target found, just shoot in the air (ground nearby)
                    IntVec3 randomCell = GenRadial.RadialCellsAround(pawn.Position, 5, true).RandomElement();
                    Verb attackVerb = pawn.TryGetAttackVerb(null, !pawn.IsColonist); // Get default ranged verb
                     if (attackVerb != null && !attackVerb.verbProps.IsMeleeAttack)
                    {
                         attackVerb.TryStartCastOn(randomCell);
                    }
                    
                    SLog.Message(string.Format("[SocialInteractions] JobDriver_ChildPlayWithWeapon: Child {0} shot in the air with {1}", 
                        pawn.LabelShort, weapon.Label));
                }
            };
            
            yield return findTargetAndShootToil;
            
            // Short delay after shooting
            yield return Toils_General.Wait(60);
        }

        private Thing FindRandomTarget(Pawn child, Thing weapon)
        {
            float range = weapon.def.Verbs[0].range;
            List<Thing> potentialTargets = new List<Thing>();

            foreach (Thing t in GenRadial.RadialDistinctThingsAround(child.Position, child.Map, range, true))
            {
                Pawn p = t as Pawn;
                if (p != null && p != child && !p.Downed && !p.Dead)
                {
                    if (GenSight.LineOfSight(child.Position, p.Position, child.Map))
                    {
                        potentialTargets.Add(p);
                    }
                }
            }

            if (potentialTargets.Count > 0)
            {
                return potentialTargets.RandomElement();
            }
            return null;
        }
    }
}