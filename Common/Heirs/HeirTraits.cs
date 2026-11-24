using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Common.Heirs
{
	public enum HeirTrait
	{
		// Boons
		Swift, Sturdy, Lucky, Featherweight, Bloodthirsty, NightEyes, QuickHands, EagleEye,
		TreasureSense, DangerSense, Gilled, CloudStep, WallCrawler, Magnetic, ThickSkin,
		// Burdens (pay out bonus gold)
		GlassBones, Myopic, Hemophiliac, Brittle, WeakArms, Clumsy, Heavy, FrailMind, CursedPurse, Sluggish,
		Count
	}

	/// <summary>GoldBonus is extra gold found, as a fraction. Burdens are worth taking for the gold.</summary>
	public record TraitInfo(string Name, string Description, float GoldBonus)
	{
		public bool Positive => GoldBonus <= 0f;
	}

	public static class TraitDefs
	{
		private static readonly TraitInfo[] Infos = {
			new("Swift", "+25% movement speed", 0f),
			new("Sturdy", "+8 defense", 0f),
			new("Lucky", "+0.3 luck: better loot rolls and crits", 0f),
			new("Featherweight", "Innate Lucky Horseshoe: no fall damage", 0f),
			new("Bloodthirsty", "Heal 5 life whenever you slay a foe", 0f),
			new("Night Eyes", "You give off a soft light", 0f),
			new("Quick Hands", "+12% attack speed", 0f),
			new("Eagle Eye", "+10% crit chance", 0f),
			new("Treasure Sense", "See treasure and chests through walls", 0f),
			new("Danger Sense", "See traps and hazards through walls", 0f),
			new("Gilled", "Innate Diving Gear: breathe and swim underwater", 0f),
			new("Cloud Step", "Innate Cloud in a Bottle: a double jump", 0f),
			new("Wall Crawler", "Innate Tiger Climbing Gear: cling to and climb walls", 0f),
			new("Magnetic", "Innate Treasure Magnet: items fly to you from afar", 0f),
			new("Thick Skin", "Reflect 50% of contact damage", 0f),

			new("Glass Bones", "-35% max life, +20% damage", 0.15f),
			new("Myopic", "Darkness clouds your vision", 0.30f),
			new("Hemophiliac", "Regeneration effects don't work; heals are 20% weaker", 0.20f),
			new("Brittle", "Take 25% more damage", 0.25f),
			new("Weak Arms", "-15% damage", 0.20f),
			new("Clumsy", "-15% attack speed", 0.15f),
			new("Heavy", "You fall faster and jump lower", 0.15f),
			new("Frail Mind", "-50% max mana", 0.10f),
			new("Cursed Purse", "-15% max life", 0.40f),
			new("Sluggish", "-15% movement speed", 0.15f),
		};

		public static TraitInfo Get(HeirTrait t) => Infos[(int)t];

		public static void Apply(Player p, HeirTrait t, ref float lifeMult, ref float manaMult)
		{
			switch (t) {
				case HeirTrait.Swift: p.moveSpeed += 0.25f; break;
				case HeirTrait.Sturdy: p.statDefense += 8; break;
				case HeirTrait.Lucky: p.luck += 0.3f; break;

				case HeirTrait.QuickHands: p.GetAttackSpeed(DamageClass.Generic) += 0.12f; break;
				case HeirTrait.EagleEye: p.GetCritChance(DamageClass.Generic) += 10; break;
				case HeirTrait.TreasureSense: p.findTreasure = true; break;
				case HeirTrait.DangerSense: p.dangerSense = true; break;

				case HeirTrait.ThickSkin: p.thorns += 0.5f; break;

				case HeirTrait.GlassBones: lifeMult -= 0.35f; p.GetDamage(DamageClass.Generic) += 0.20f; break;
				case HeirTrait.Myopic: p.AddBuff(BuffID.Darkness, 2); break;
				case HeirTrait.WeakArms: p.GetDamage(DamageClass.Generic) -= 0.15f; break;
				case HeirTrait.Clumsy: p.GetAttackSpeed(DamageClass.Generic) -= 0.15f; break;
				case HeirTrait.Heavy: p.jumpSpeedBoost -= 1.5f; p.maxFallSpeed += 4f; break;
				case HeirTrait.FrailMind: manaMult -= 0.5f; break;
				case HeirTrait.CursedPurse: lifeMult -= 0.15f; break;
				case HeirTrait.Sluggish: p.moveSpeed -= 0.15f; break;
			}
		}

		/// <summary>
		/// Traits that are an accessory in all but name: the heir gets that accessory, marked innate, equipped (so it
		/// shows in the slots, works with other gear, and can be taken off). 0 = none.
		/// </summary>
		public static int InnateItem(HeirTrait t) => t switch {
			HeirTrait.Featherweight => ItemID.LuckyHorseshoe,
			HeirTrait.Gilled => ItemID.DivingGear,
			HeirTrait.CloudStep => ItemID.CloudinaBottle,
			HeirTrait.WallCrawler => ItemID.TigerClimbingGear,
			HeirTrait.Magnetic => ItemID.TreasureMagnet,
			_ => 0
		};

		/// <summary>Two traits that make no sense together.</summary>
		public static bool Conflicts(HeirTrait a, HeirTrait b) =>
			(a == HeirTrait.Swift && b == HeirTrait.Sluggish) || (a == HeirTrait.Sluggish && b == HeirTrait.Swift) ||
			(a == HeirTrait.Featherweight && b == HeirTrait.Heavy) || (a == HeirTrait.Heavy && b == HeirTrait.Featherweight) ||
			(a == HeirTrait.QuickHands && b == HeirTrait.Clumsy) || (a == HeirTrait.Clumsy && b == HeirTrait.QuickHands) ||
			(a == HeirTrait.NightEyes && b == HeirTrait.Myopic) || (a == HeirTrait.Myopic && b == HeirTrait.NightEyes);
	}
}
