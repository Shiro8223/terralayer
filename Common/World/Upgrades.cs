using System;
using Terralayer.Common.Utilities;
using Terraria.ID;

namespace Terralayer.Common.World
{
	public enum UpgradeType
	{
		Might,
		ForgeMastery,
		Vitality,
		Bulwark,
		Fleetfoot,
		RuneAffinity,
		Arcana,
		FieldKit,
		SecondWind,
		Scouting,
		TollRelief,
		Fortune,
		AncestralBlessing,
		ChargeMastery,
		LegacyOfKings,
		Count
	}

	/// <summary>
	/// A skill-tree node. <paramref name="Parent"/> must have at least one level before this node can be bought
	/// (-1 = branch root). Col/Row place it on the tree; Icon is the vanilla item drawn on the node.
	/// </summary>
	public record UpgradeInfo(string Name, string Description, int MaxLevel, long BaseCost, double Growth, string Structure,
		int Parent, int Col, int Row, int Icon);

	/// <summary>Permanent, per-character upgrades bought at the Estate. Each needs its building to exist first.</summary>
	public static class Upgrades
	{
		private static readonly UpgradeInfo[] Infos = {
			new("Might", "+5% damage per level", 10, 3 * Coins.Gold, 1.45, "Smithy", -1, 0, 0, ItemID.WarriorEmblem),
			new("Forge Mastery", "Heirs start with +1 weapon refinement per level", 4, 8 * Coins.Gold, 2.0, "Smithy", (int)UpgradeType.Might, 0, 1, ItemID.IronAnvil),
			new("Vitality", "+15 max life per level", 10, 3 * Coins.Gold, 1.45, "Yard", -1, 1, 0, ItemID.LifeCrystal),
			new("Bulwark", "+2 defense per level", 8, 4 * Coins.Gold, 1.5, "Yard", (int)UpgradeType.Vitality, 1, 1, ItemID.CobaltShield),
			new("Fleetfoot", "+4% movement speed per level", 5, 4 * Coins.Gold, 1.6, "Yard", -1, 2, 0, ItemID.HermesBoots),
			new("Rune Affinity", "+25% chance per level that an heir starts with a rune", 4, 10 * Coins.Gold, 1.8, "Sanctum", (int)UpgradeType.Arcana, 3, 1, ItemID.SpellTome),
			new("Arcana", "+20 max mana per level", 8, 4 * Coins.Gold, 1.5, "Sanctum", -1, 3, 0, ItemID.ManaCrystal),
			new("Field Kit", "+2 healing potions at the start of every descent per level", 3, 12 * Coins.Gold, 2.0, "Infirmary", (int)UpgradeType.Fleetfoot, 2, 1, ItemID.HealingPotion),
			new("Second Wind", "Once per descent, survive a killing blow with 30% life", 1, 60 * Coins.Gold, 1.0, "Infirmary", (int)UpgradeType.Bulwark, 1, 2, ItemID.AnkhCharm),
			new("Scouting", "The map shows what kind of room lies behind every rift", 1, 40 * Coins.Gold, 1.0, "Tower", (int)UpgradeType.TollRelief, 4, 2, ItemID.Compass),
			new("Toll Relief", "The Gatekeeper takes 8% less per level", 5, 20 * Coins.Gold, 1.6, "Treasury", (int)UpgradeType.Fortune, 4, 1, ItemID.PiggyBank),
			new("Fortune", "+8% gold found per level", 10, 10 * Coins.Gold, 1.45, "Treasury", -1, 4, 0, ItemID.GoldCoin),
			new("Ancestral Blessing", "Every descent starts with a random Boon", 1, 90 * Coins.Gold, 1.0, "Reliquary", (int)UpgradeType.RuneAffinity, 3, 2, ItemID.StarinaBottle),
			new("Charge Mastery", "+15% ability charge rate per level", 5, 25 * Coins.Gold, 1.6, "Keep", (int)UpgradeType.FieldKit, 2, 2, ItemID.FallenStar),
			new("Legacy of Kings", "+5% damage and +5% max life per level", 3, 120 * Coins.Gold, 1.8, "Keep", (int)UpgradeType.ForgeMastery, 0, 2, ItemID.GoldCrown),
		};

		public static UpgradeInfo Get(UpgradeType t) => Infos[(int)t];

		public static readonly string[] Branches = { "Might", "Survival", "Agility", "Arcane", "Fortune" };

		public static long CostFor(UpgradeType t, int level)
		{
			UpgradeInfo info = Get(t);
			return (long)Math.Round(info.BaseCost * Math.Pow(info.Growth, level) / Coins.Silver) * Coins.Silver;
		}
	}
}
