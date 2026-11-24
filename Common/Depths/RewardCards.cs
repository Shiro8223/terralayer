using System;
using System.Collections.Generic;
using System.Linq;
using Terralayer.Common.Items;
using Terralayer.Content.Items;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Common.Depths
{
	/// <summary>
	/// What a room-clear card can be. Each belongs to a rarity: Common cards are restoration (the only way life and
	/// ability charge come back in the Depths), Uncommon are refining and small supplies, Rare bring new gear, Epic
	/// include the Mystery Cache, and Legendary are the rare jackpots.
	/// </summary>
	public enum CardType : byte
	{
		// Common: restoration
		Mend, Recharge, CatchBreath, Bandage, Clarity, Rations, SparePotion, Tonic,
		// Uncommon
		Refine, Reforge, Boon, CoinPurse, Satchel, FullRestore, Charm,
		// Rare
		Arsenal, TwinBoon, RuneStone, Armory, Trinket, DoubleRefine,
		// Epic
		Mystery, AscendantRune, Triumph, Masterwork, GildedArsenal,
		// Legendary
		Ascension, LineBlessing, LegendaryRune,
		Count
	}

	public record CardInfo(string Name, Rarity Rarity, Func<int> Icon);

	/// <summary>One rolled card. Item and Boon hold whatever the card was rolled with (a weapon, an accessory, a boon).</summary>
	public struct RewardCard
	{
		public CardType Type;
		public BoonStat Boon;
		public int Item;
		public Rarity ItemRarity;
	}

	public static class RewardCards
	{
		private static readonly CardInfo[] Infos = {
			new("Mend", Rarity.Common, () => ItemID.Heart),
			new("Recharge", Rarity.Common, () => ItemID.FallenStar),
			new("Catch Your Breath", Rarity.Common, () => ItemID.BottledWater),
			new("Bandage", Rarity.Common, () => ItemID.AdhesiveBandage),
			new("Clarity", Rarity.Common, () => ItemID.Star),
			new("Field Rations", Rarity.Common, () => ItemID.CookedFish),
			new("Spare Potion", Rarity.Common, () => ItemID.LesserHealingPotion),
			new("Ironskin Tonic", Rarity.Common, () => ItemID.IronskinPotion),

			new("Refine", Rarity.Uncommon, () => ItemID.IronAnvil),
			new("Reforge", Rarity.Uncommon, () => ItemID.IronHammer),
			new("Boon", Rarity.Uncommon, () => ItemID.StarinaBottle),
			new("Coin Purse", Rarity.Uncommon, () => ItemID.GoldCoin),
			new("Potion Satchel", Rarity.Uncommon, () => ItemID.RegenerationPotion),
			new("Full Restore", Rarity.Uncommon, () => ItemID.LifeFruit),
			new("Charm", Rarity.Uncommon, () => ItemID.Aglet),

			new("Arsenal", Rarity.Rare, () => ItemID.WeaponRack),
			new("Twin Boon", Rarity.Rare, () => ItemID.StarinaBottle),
			new("Rune Stone", Rarity.Rare, () => ModContent.ItemType<EmberRune>()),
			new("Armory", Rarity.Rare, () => ItemID.IronChainmail),
			new("Trinket", Rarity.Rare, () => ItemID.HermesBoots),
			new("Double Refine", Rarity.Rare, () => ItemID.MythrilAnvil),

			new("Mystery Cache", Rarity.Epic, () => ItemID.WoodenCrate),
			new("Ascendant Rune", Rarity.Epic, () => ModContent.ItemType<StarfallRune>()),
			new("Triumph", Rarity.Epic, () => ItemID.FallenStar),
			new("Masterwork", Rarity.Epic, () => ItemID.AdamantiteForge),
			new("Gilded Arsenal", Rarity.Epic, () => ItemID.GoldBar),

			new("Ascension", Rarity.Legendary, () => ItemID.LifeCrystal),
			new("Blessing of the Line", Rarity.Legendary, () => ItemID.SoulofLight),
			new("Legendary Rune", Rarity.Legendary, () => ModContent.ItemType<QuakeRune>()),
		};

		public static CardInfo Get(CardType t) => Infos[(int)t];

		public static IEnumerable<CardType> OfRarity(Rarity r) =>
			Enumerable.Range(0, (int)CardType.Count).Select(i => (CardType)i).Where(t => Get(t).Rarity == r);

		/// <summary>Rarity for a free card slot: Common 35%, Uncommon 33%, Rare 21%, Epic 9%, Legendary 2%.</summary>
		public static Rarity RollRarity(Random rng)
		{
			double r = rng.NextDouble();
			return r switch {
				< 0.35 => Rarity.Common,
				< 0.68 => Rarity.Uncommon,
				< 0.89 => Rarity.Rare,
				< 0.98 => Rarity.Epic,
				_ => Rarity.Legendary
			};
		}

		public static bool IsRestoration(CardType t) => Get(t).Rarity == Rarity.Common || t == CardType.FullRestore;
	}
}
