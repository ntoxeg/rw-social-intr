using HarmonyLib;
using RimWorld;
using Verse;
using System;
using UnityEngine;

namespace SocialInteractions
{
    public static class CombatTaunts
    {
        public static readonly string[] AttackingTaunts = new string[]
        {
            "Eat lead!",
            "You're finished!",
            "Die, you scum!",
            "RATATATATA!!",
			"Pew! Pew! Pew!!",
			"Your aim's as useless as your life!",
			"Looks like you lost the fight before it started, maggot!",
			"Time to put you in the dirt where you belong, worm!",
			"I've seen better shots from a blind monkey.",
			"Your sorry ass isn't worth the bullet, scum.",
			"If you were any more pathetic, you'd be a goblin.",
			"That shot was so bad, even grandma could do better.",
			"Get used to dying, because it's gonna be your new hobby.",
			"You're going down, bitch!",
			"Stick that in your ass, loser!",
			"Looks like your momma didn't raise no soldier!",
			"Gonna paint the floor red with your blood, fuckface!",
			"This is for my homies!",
			"Your mama must've dropped you on your head as a kid, dumbass.",
			"Time to send you back to whatever rock you crawled out from under!",
			"I'm gonna make you eat those bullets, maggot!",
			"You're gonna bleed out like a pig!",
			"Your mom didn't raise no killer!",
			"Fuck your sorry ass!",
			"Die, maggot!",
			"Your momma so fat... ah fuck it, just die!",
			"Crybaby needs a timeout!",
			"That shot won't save you from my lead!",
			"You're just a warm body to me now.",
			"My bullet's got your name on it.",
			"Looks like someone left their brains in their locker.",
			"You're about to meet your maker!",
			"I'm the one who'll be carving you up tonight.",
			"Wish you'd aimed better, asshole!",
			"You're just a target practice dummy!",
			"Your screams will be music to my ears!",
			"Time to send you back to whatever hell you crawled out of.",
			"You're about to get reamed... literally!",
			"This ain't no game, punk. You're dead meat!",
			"Your life just flashed before your eyes…and it was short.",
			"You shoulda stayed in bed, you useless sack of shit.",
			"Is that all you got? Pathetic!",
			"I'm gonna put a hole in your skull!",
			"Say hello to my little friend!",
			"You're already dead, you just don't know it yet!",
			"Another one bites the dust!",
			"Time to shut you up for good!",
			"I'm gonna enjoy this!",
			"Game over, motherfucker!",
			"You picked the wrong fight!",
			"Feel my wrath!",
			"Die screaming!",
			"This is for all the pain you've caused!",
			"I'm gonna end you!",
			"You're history!",
			"Time to meet your maker!",
			"You're about to get schooled!",
			"I've been waiting for this moment!",
			"Say goodbye to your sorry existence!",
			"I'm gonna make you suffer!",
			"This is payback!",
			"BOOM!",
            "BOOM! Headshot!",
			"Get. Fucked. Noob.",
			"You're going down hard!",
			"Time to put you out of your misery!",
			"I'm gonna blow you away!",
			"360 no-scope motherfucker!",
			"Sniped!",
			"Another casualty of war!",
			"You're just another body count!",
			"Time to add you to my kill list!",
			"I'm gonna make you regret ever being born!",
			"This is the last thing you'll ever see!",
			"I'm gonna send you straight to hell!",
			"Time to put you down like the dog you are!",
			"You're about to become a corpse!",
			"I'm gonna paint the walls with your blood!",
			"Time to end this!",
			"I'm gonna make you pay for everything!",
			"You're going to hell in a handbasket!",
			"I'm gonna put you out of your misery!",
			"This is the end of the line for you!",
			"I'm gonna make you wish you were never born!",
			"Time to put a bullet between your eyes!",
			"You're going to die screaming like a bitch!",
			"I'm gonna enjoy watching you bleed out!",
			"This is justice!",
			"Time to put you in the ground!",
			"I'm gonna make you suffer for this!",
			"You're going to pay with your life!",
			"I'm gonna end your sorry existence!",
			"Time to put you down for good!",
			"You're going to die like the animal you are!"
        };

        public static readonly string[] MeleeAttackingTaunts = new string[]
        {
            "Take this!",
            "Eat steel!",
            "Die by my blade!",
            "Feel my steel!",
            "You're mine!",
            "Taste my blade!",
            "Prepare to die!",
            "I'll gut you!",
            "Slice and dice!",
            "Cut to pieces!",
            "You're dead meat!",
            "Time to bleed!",
            "This is for my homies!",
            "Your blood will be my victory!",
            "I'll carve you up!",
            "You're finished!",
            "Die, scum!",
            "Feel the pain!",
            "I'll rip you apart!",
            "You're history!",
            "Time to meet your maker!",
            "This is gonna hurt!",
            "I'll end you!",
            "BONK!",
            "28 times!",
            "STOP! Hammer time!",
            "Get bonked looser!",
            "You're going down!",
            "This is for my family!",
            "Feel my wrath!",
            "You're nothing!",
            "I'll crush you!",
            "Die!!",
			"Become sashimi for me please!",
            "This is personal!",
            "You're dead!",
            "I'll make you pay!",
            "Time to die!",
            "You're finished, maggot!",
            "Eat steel, bitch!",
            "This is the end for you!",
            "I'll show you pain!",
            "Your time is up!",
            "I'll send you to hell!",
            "You're fucked!",
			"Feel my blade!",
			"I'm gonna slice you open!",
			"Time to spill some blood!",
			"I'll cut you to ribbons!",
			"Prepare to be diced!",
			"You're about to get filleted!",
			"I'm gonna mince you!",
			"Feel the edge of my weapon!",
			"I'm gonna carve a smile on your face!",
			"Time to butcher you!",
			"I'll chop you up!",
			"You're gonna be hamburger!",
			"I'm gonna dice you like vegetables!",
			"Time to slice and serve!",
			"I'll make you into sushi!",
			"You're about to get shredded!",
			"I'm gonna turn you into ground beef!",
			"Time to get chopped up!",
			"I'll cut you into little pieces!",
			"You're gonna be chopped liver!",
			"I'm gonna make you into a fine mist!",
			"Time to get diced!",
			"I'll slice you like bread!",
			"You're about to get gutted!",
			"I'm gonna fillet you!",
			"Time to get carved up!",
			"I'll make you into a pincushion!",
			"You're gonna be sliced and diced!",
			"I'm gonna turn you into swiss cheese!",
			"Time to get stabbed!",
			"I'll puncture you like a pin cushion!",
			"You're about to get skewered!",
			"I'm gonna run you through!",
			"Time to get impaled!",
			"I'll stick you like a pig!",
			"You're gonna be a human pincushion!",
			"I'm gonna make you into a colander!",
			"Time to get pierced!",
			"I'll make you into a human sieve!",
			"You're about to get stabbed repeatedly!",
			"I'm gonna make you into a pincushion!",
			"Time to get shish kebabed!",
			"I'll make you into a human pincushion!",
			"You're gonna get the point!",
			"I'm gonna make you into a pincushion!",
			"Time to get the business end!",
			"I'll make you into a pincushion!"
        };

        public static readonly string[] GettingHitComplaints = new string[]
        {
            "Argh!",
            "They got me!",
            "I'm hit!",
            "Gah!",
            "That stings!",
			"Ahh, shit!",
			"Ow, fuck!",
			"I'm hit!!",
			"Son of a bitch!",
			"Ngh, that hurt!",
			"It's just a flesh wound...",
			"Goddammit, not again!",
			"Take that, you bastard!",
			"Fucking ow, my side!",
			"Shit, I'm bleeding!",
			"Who the hell shot me?!",
			"Gah, that stings!",
			"I've been hit!",
			"Dammit, my leg!",
			"Ow! What the fuck?",
			"Shit, did he just-",
			"Ah! My side hurts!",
			"Gah, not again!",
			"Ugh, why me?!",
			"Oof! That hurts like a motherfucker!",
			"Fuck, is that blood?",
			"Oh no, not shot again...",
			"Ahh, my leg's on fire!",
			"What do you mean shoot back?! I'm a pacifist!",
			"Holy crap, that hurt like hell!",
			"I think I need a medic... or a mortician.",
			"Why'd he have to aim for my dick?!",
			"This isn't fun anymore, I hate this game!",
			"Oh man, I'm bleeding out fast!",
			"Fuck, I'm bleeding!",
			"Ow, that's gonna leave a mark!",
			"Damn, that stings!",
			"Shit, I'm hit again!",
			"Ow, fuck me!",
			"Gah, that's painful!",
			"Dammit, I'm bleeding!",
			"Shit, I'm hit bad!",
			"Ow, that's gonna hurt tomorrow!",
			"Fuck, I'm bleeding out!",
			"Shit, I'm gonna die!",
			"Ow, that's a bad one!",
			"Gah, I'm bleeding!",
			"Dammit, I'm hit!",
			"Shit, I'm bleeding bad!",
			"Ow, that's gonna be a scar!",
			"Fuck, I'm bleeding out fast!",
			"Shit, I'm gonna bleed out!",
			"Ow, that's a deep one!",
			"Gah, I'm bleeding badly!",
			"Dammit, I'm hit hard!",
			"Shit, I'm bleeding to death!",
			"Ow, that's gonna hurt!",
			"Fuck, I'm bleeding out quick!",
			"Shit, I'm gonna die here!",
			"Ow, that's a bad hit!",
			"Gah, I'm bleeding out!",
			"Dammit, I'm hit bad!",
			"Shit, I'm bleeding to death!",
			"Ow, that's gonna be painful!",
			"Fuck, I'm bleeding out now!",
			"Shit, I'm gonna bleed out!",
			"Ow, that's a nasty one!",
			"Gah, I'm bleeding out bad!",
			"Dammit, I'm hit real bad!",
			"Shit, I'm bleeding out quick!",
			"Ow, that's gonna be a big scar!",
			"Fuck, I'm bleeding out fast!",
			"Shit, I'm gonna bleed out quick!",
			"Ow, that's a deep cut!",
			"Gah, I'm bleeding out real bad!",
			"Dammit, I'm hit really bad!",
			"Shit, I'm bleeding to death fast!",
			"Ow, that's gonna hurt a lot!",
			"Fuck, I'm bleeding out now fast!",
			"Shit, I'm gonna bleed out now!"
        };

        public static readonly string[] DownedCallsForHelp = new string[]
        {
            "I'm down! Need help!",
            "Medic!",
            "HELP!!",
            "Can't go on...",
            "They got me good...",
            "Ugh... darkness...",
			"Please don't leave me...",
			"Someone help me, I'm dying here...",
			"Medic! Hurry up before I bleed out!",
			"I can't see straight, call a doc..",
			"This is it, I'm finished... so fucking unfair.",
			"Don't let me die, please... I've got family!",
			"My vision's going black, I'm slipping away...",
			"Get a medic over here, stat! I'm critical!",
			"I don't wanna die here, not like this...",
			"Someone help me up, I'm not done fighting yet!",
			"I can feel myself fading... this is so scary.",
			"Call an evac, get me the fuck outta here!",
			"I'm too young to die, there's still so much I wanna do...",
			"I can barely breathe, send a medic post-haste!",
			"I don't wanna be a corpse, not now... not ever.",
			"I'm bleeding out!",
			"Someone save me!",
			"I'm dying here!",
			"Help me!",
			"I'm gonna die!",
			"Medic, please!",
			"I'm in critical condition!",
			"I need immediate assistance!",
			"I'm bleeding to death!",
			"Someone get a medic!",
			"I'm slipping away!",
			"I'm about to die!",
			"I need help now!",
			"I'm in trouble!",
			"I'm not gonna make it!",
			"I'm in serious trouble!",
			"I'm going to die if nobody helps!",
			"I'm in dire need of assistance!",
			"I'm critically wounded!",
			"I need urgent medical attention!",
			"I'm in a life or death situation!",
			"I'm bleeding out fast!",
			"I need help immediately!",
			"I'm in deep shit!",
			"I'm not going to survive without help!",
			"I'm in mortal danger!",
			"I'm about to kick the bucket!",
			"I'm in desperate need of help!",
			"I'm not long for this world!",
			"I'm in extreme danger!",
			"I'm about to meet my maker!",
			"I'm in a critical state!",
			"I'm not going to last much longer!",
			"I'm in grave danger!",
			"I'm about to buy the farm!",
			"I'm in serious trouble!",
			"I'm not going to make it through this!",
			"I'm in mortal peril!",
			"I'm about to go to hell!",
			"I'm in imminent danger!",
			"I'm not going to survive this!",
			"I'm in extreme peril!",
			"I'm about to bite the dust!",
			"I'm in critical condition!",
			"I'm not going to live through this!",
			"I'm in mortal peril!",
			"I'm about to croak!",
			"I'm in dire straits!",
			"I'm not going to make it out alive!",
			"I'm in extreme danger!",
			"I'm about to check out!",
			"I'm in serious jeopardy!",
			"I'm not going to survive the day!",
			"I'm in mortal danger!",
			"I'm about to buy it!",
			"I'm in critical peril!",
			"I'm not going to make it past this!",
			"I'm in grave peril!",
			"I'm about to cash in my chips!",
			"I'm in serious danger!",
			"I'm not going to live much longer!",
			"I'm in extreme jeopardy!",
			"I'm about to kick the bucket!",
			"I'm in mortal peril!",
			"I'm not going to survive this ordeal!",
			"I'm in critical danger!",
			"I'm about to go to the great beyond!",
			"I'm in serious peril!"
        };
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Verb_MeleeAttack_TryCastShot_Patch
    {
        public static void Postfix(Verb_MeleeAttack __instance, bool __result)
        {
            if (__result && SocialInteractions.Settings.enableCombatTaunts && __instance.CasterIsPawn && __instance.CasterPawn.RaceProps.Humanlike && !ShamblerHelper.IsShambler(__instance.CasterPawn) && Rand.Value < SocialInteractions.Settings.meleeTauntProbability)
            {
                string taunt = CombatTaunts.MeleeAttackingTaunts.RandomElement();
                float duration = SocialInteractions.EstimateReadingTime(taunt);
                SpeechBubbleManager.EnqueueInstant(__instance.CasterPawn, taunt, duration, null, false); // Use standard mote for combat taunts
            }
        }
    }

    [HarmonyPatch(typeof(Verb_Shoot), "TryCastShot")]
    public static class Verb_Shoot_TryCastShot_Patch
    {
        public static void Postfix(Verb_Shoot __instance, bool __result)
        {
            if (__result && SocialInteractions.Settings.enableCombatTaunts && __instance.CasterIsPawn && __instance.CasterPawn.RaceProps.Humanlike && !ShamblerHelper.IsShambler(__instance.CasterPawn) && Rand.Value < SocialInteractions.Settings.shootTauntProbability)
            {
                Pawn casterPawn = __instance.CasterPawn;
                string taunt = CombatTaunts.AttackingTaunts.RandomElement();
                float duration = SocialInteractions.EstimateReadingTime(taunt);
                SpeechBubbleManager.EnqueueInstant(casterPawn, taunt, duration, null, false); // Use standard mote for combat taunts
            }
        }
    }
    [HarmonyPatch(typeof(Pawn_HealthTracker), "PostApplyDamage")]
    public static class Pawn_HealthTracker_PreApplyDamage_Patch
    {
        public static void Postfix(Pawn_HealthTracker __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            if (!SocialInteractions.Settings.enableCombatTaunts) return;

            Pawn pawn = (Pawn)AccessTools.Field(typeof(Pawn_HealthTracker), "pawn").GetValue(__instance);

            if (pawn == null || !pawn.Spawned || pawn.Downed || !pawn.Awake() || !pawn.RaceProps.Humanlike || ShamblerHelper.IsShambler(pawn)) return;

            if (dinfo.Instigator == null || dinfo.Instigator == pawn || !dinfo.Def.ExternalViolenceFor(pawn)) return;

            if (dinfo.Instigator.HostileTo(pawn))
            {
                if (Rand.Value < SocialInteractions.Settings.gettingHitComplaintProbability)
                {
                    string complaint = CombatTaunts.GettingHitComplaints.RandomElement();
                    float duration = SocialInteractions.EstimateReadingTime(complaint);
                    SpeechBubbleManager.EnqueueInstant(pawn, complaint, duration, Color.yellow, false); // Use standard mote for combat taunts
                }
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    public static class Pawn_HealthTracker_MakeDowned_Patch
    {
        public static void Postfix(Pawn_HealthTracker __instance)
        {
            if (!SocialInteractions.Settings.enableCombatTaunts) return;
            Pawn pawn = (Pawn)AccessTools.Field(typeof(Pawn_HealthTracker), "pawn").GetValue(__instance);
            if (pawn.Spawned && pawn.RaceProps.Humanlike && !ShamblerHelper.IsShambler(pawn) && Rand.Value < SocialInteractions.Settings.downedCallForHelpProbability)
            {
                string callForHelp = CombatTaunts.DownedCallsForHelp.RandomElement();
                float duration = SocialInteractions.EstimateReadingTime(callForHelp);
                SpeechBubbleManager.EnqueueInstant(pawn, callForHelp, duration, Color.red, false); // Use standard mote for combat taunts
            }
        }
    }
}

// Helper method to check if a pawn is a shambler
public static class ShamblerHelper
{
    public static bool IsShambler(Pawn pawn)
    {
        // Check if the ModsConfig.AnomalyActive is true first (shambler functionality requires Anomaly DLC)
        if (!ModsConfig.AnomalyActive) return false;
        
        // Check if the pawn has the IsShambler property
        try
        {
            // Use reflection to access the IsShambler property since it might not be available in all versions
            var isShamblerProperty = typeof(Pawn).GetProperty("IsShambler");
            if (isShamblerProperty != null)
            {
                return (bool)isShamblerProperty.GetValue(pawn, null);
            }
        }
        catch (Exception ex)
        {
            // If there's any exception, just return false to be safe
            SocialInteractions.SLog.Warning(string.Format("[SocialInteractions] Exception checking if pawn is shambler: {0}", ex.Message));
        }
        
        return false;
    }
}