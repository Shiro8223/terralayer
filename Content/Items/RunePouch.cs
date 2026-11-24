using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terralayer.Common.Items;
using Terralayer.Common.Players;
using Terralayer.Common.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Content.Items
{
	/// <summary>A sealed bag from the Runeweaver: right-click it to find out which rune, of which rarity and odds, is inside.</summary>
	public class RunePouch : ModItem
	{
		public override string Texture => $"Terraria/Images/Item_{ItemID.GoodieBag}";

		public const long Price = 5 * Coins.Gold;

		public override void SetDefaults()
		{
			Item.width = 24;
			Item.height = 24;
			Item.maxStack = Item.CommonMaxStack;
			Item.rare = ItemRarityID.LightPurple;
			Item.value = 0;
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "Open", "Right-click to open: one random rune, weapon or armor, of any rarity and any odds") { OverrideColor = new Color(220, 180, 255) });
		}

		public override bool CanRightClick() => true;

		public override void RightClick(Player player)
		{
			var rng = new Random(Main.rand.Next());
			Item rune = Loot.RollRune(0, rng);
			int chance = rune.ModItem is RuneItem r ? r.Chance : rune.ModItem is ArmorRuneItem a ? a.Chance : 0;
			Rarity rarity = rune.ModItem is RuneItem r2 ? r2.Rarity : ((ArmorRuneItem)rune.ModItem).Rarity;
			player.QuickSpawnItem(player.GetSource_OpenItem(Type), rune);
			SoundEngine.PlaySound(SoundID.Item29, player.Center);
			CombatText.NewText(player.Hitbox, RarityDefs.Color(rarity), $"{RarityDefs.Name(rarity)} {rune.Name}! ({chance}%)", true);
		}
	}

	/// <summary>
	/// The Runeweaver's travelling stock: six random runes, each of a random rarity with random odds, sold once each.
	/// The stock changes every time you come back from a descent (or an heir falls).
	/// </summary>
	public static class RuneStock
	{
		public const int Size = 6;

		public static List<Item> Generate(int seed)
		{
			var rng = new Random(seed);
			var list = new List<Item>();
			for (int i = 0; i < Size; i++) {
				Rarity rarity = RarityDefs.Roll(rng.NextDouble() < 0.35 ? 1 : 0, rng);
				Item rune = Loot.RuneOfRarity(rarity, rng);
				int chance = rune.ModItem is RuneItem r ? r.Chance : ((ArmorRuneItem)rune.ModItem).Chance;
				long price = (long)(PriceFor(rarity) * (0.6 + chance / 100.0));
				rune.shopCustomPrice = (int)(price / (10 * Coins.Silver) * (10 * Coins.Silver)); // to the nearest 10 silver
				list.Add(rune);
			}
			return list;
		}

		public static long PriceFor(Rarity r) => r switch {
			Rarity.Common => 3 * Coins.Gold,
			Rarity.Uncommon => 7 * Coins.Gold,
			Rarity.Rare => 15 * Coins.Gold,
			Rarity.Epic => 30 * Coins.Gold,
			_ => 60 * Coins.Gold
		};

		/// <summary>Do two rune items match exactly (type, rarity, odds)?</summary>
		public static bool Same(Item a, Item b) =>
			a.type == b.type && (a.ModItem, b.ModItem) switch {
				(RuneItem x, RuneItem y) => x.Rarity == y.Rarity && x.Chance == y.Chance,
				(ArmorRuneItem x, ArmorRuneItem y) => x.Rarity == y.Rarity && x.Chance == y.Chance,
				_ => false
			};
	}
}
