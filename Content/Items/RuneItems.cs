using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terralayer.Common.Items;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Terralayer.Content.Items
{
	/// <summary>
	/// A rune stone. Carry it home and inscribe it at the Runeforge onto your heir's weapon or armor.
	/// Three identical runes (same type and rarity) fuse into one of the next rarity automatically.
	/// </summary>
	public abstract class RuneItem : ModItem
	{
		public abstract RuneType Kind { get; }

		public Rarity Rarity = Rarity.Common;

		/// <summary>Odds (1-99%) that this rune takes when inscribed. A failure shatters it.</summary>
		public int Chance = -1;

		public override string Texture => $"Terralayer/Assets/Runes/{Kind}";

		public override void SetDefaults()
		{
			Item.width = 24;
			Item.height = 24;
			Item.maxStack = 1;
			Item.value = Item.buyPrice(silver: 50);
			Item.rare = RarityDefs.Vanilla(Rarity);
			if (Chance < 0)
				Chance = RuneOdds.Roll(Rarity);
		}

		public RuneSocket Socket => new(Kind, Rarity);

		public static Item Create(RuneType type, Rarity rarity, System.Random rng = null, int chance = -1, bool shop = false)
		{
			var item = new Item(TypeFor(type));
			if (item.ModItem is RuneItem rune) {
				rune.Rarity = rarity;
				rune.Chance = chance >= 0 ? RuneOdds.Clamp(rarity, chance) : RuneOdds.Roll(rarity, rng, shop);
				item.rare = RarityDefs.Vanilla(rarity);
			}
			return item;
		}

		public static int TypeFor(RuneType type) => type switch {
			RuneType.Reaping => ModContent.ItemType<ReapingRune>(),
			RuneType.Splitting => ModContent.ItemType<SplittingRune>(),
			RuneType.Ember => ModContent.ItemType<EmberRune>(),
			RuneType.Frost => ModContent.ItemType<FrostRune>(),
			RuneType.Storm => ModContent.ItemType<StormRune>(),
			RuneType.Vampiric => ModContent.ItemType<VampiricRune>(),
			RuneType.Starfall => ModContent.ItemType<StarfallRune>(),
			RuneType.Hive => ModContent.ItemType<HiveRune>(),
			RuneType.Venom => ModContent.ItemType<VenomRune>(),
			RuneType.Midas => ModContent.ItemType<MidasRune>(),
			_ => ModContent.ItemType<QuakeRune>()
		};

		public override void UpdateInventory(Player player) => Item.rare = RarityDefs.Vanilla(Rarity);

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			TooltipLine name = tooltips.Find(t => t.Name == "ItemName" && t.Mod == "Terraria");
			if (name != null) {
				name.Text = $"{RarityDefs.Name(Rarity)} {name.Text}";
				name.OverrideColor = RarityDefs.Color(Rarity);
			}
			tooltips.Add(new TooltipLine(Mod, "Effect", RuneDefs.Describe(Socket)) { OverrideColor = RuneDefs.Get(Kind).Color });
			tooltips.Add(new TooltipLine(Mod, "Odds", $"Inscription odds: {RuneOdds.Tag(Chance)}  (a failure shatters the rune)"));
			tooltips.Add(new TooltipLine(Mod, "How", "Inscribe it at the Runeforge on the homestead (costs gold)"));
			tooltips.Add(new TooltipLine(Mod, "Keep", "Kept if you return home alive; lost if you die") { OverrideColor = new Color(150, 150, 170) });
			if (Rarity < Rarity.Legendary)
				tooltips.Add(new TooltipLine(Mod, "Fuse", "Carry 3 identical runes to fuse them into a higher rarity (keeps the best odds, +6%)") { OverrideColor = new Color(150, 150, 170) });
		}

		public override void SaveData(TagCompound tag)
		{
			tag["rarity"] = (byte)Rarity;
			tag["odds"] = Chance;
		}

		public override void LoadData(TagCompound tag)
		{
			Rarity = (Rarity)tag.GetByte("rarity");
			Chance = tag.ContainsKey("odds") ? RuneOdds.Clamp(Rarity, tag.GetInt("odds")) : RuneOdds.Roll(Rarity);
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write((byte)Rarity);
			writer.Write((byte)Chance);
		}

		public override void NetReceive(BinaryReader reader)
		{
			Rarity = (Rarity)reader.ReadByte();
			Chance = reader.ReadByte();
		}
	}

	public class ReapingRune : RuneItem { public override RuneType Kind => RuneType.Reaping; }
	public class SplittingRune : RuneItem { public override RuneType Kind => RuneType.Splitting; }
	public class EmberRune : RuneItem { public override RuneType Kind => RuneType.Ember; }
	public class FrostRune : RuneItem { public override RuneType Kind => RuneType.Frost; }
	public class StormRune : RuneItem { public override RuneType Kind => RuneType.Storm; }
	public class VampiricRune : RuneItem { public override RuneType Kind => RuneType.Vampiric; }
	public class StarfallRune : RuneItem { public override RuneType Kind => RuneType.Starfall; }
	public class HiveRune : RuneItem { public override RuneType Kind => RuneType.Hive; }
	public class VenomRune : RuneItem { public override RuneType Kind => RuneType.Venom; }
	public class MidasRune : RuneItem { public override RuneType Kind => RuneType.Midas; }
	public class QuakeRune : RuneItem { public override RuneType Kind => RuneType.Quake; }
}

namespace Terralayer.Content.Items
{
	/// <summary>An armor rune. Inscribe it onto a helmet, chestpiece or leggings at the Runeforge.</summary>
	public abstract class ArmorRuneItem : ModItem
	{
		public abstract ArmorRuneType Kind { get; }

		public Rarity Rarity = Rarity.Common;

		/// <summary>Odds (1-99%) that this rune takes when inscribed. A failure shatters it.</summary>
		public int Chance = -1;

		public override string Texture => $"Terralayer/Assets/ArmorRunes/{Kind}";

		public ArmorSocket Socket => new(Kind, Rarity);

		public override void SetDefaults()
		{
			Item.width = 24;
			Item.height = 24;
			Item.maxStack = 1;
			Item.value = Item.buyPrice(silver: 50);
			Item.rare = RarityDefs.Vanilla(Rarity);
			if (Chance < 0)
				Chance = RuneOdds.Roll(Rarity);
		}

		public static Item Create(ArmorRuneType type, Rarity rarity, System.Random rng = null, int chance = -1, bool shop = false)
		{
			var item = new Item(TypeFor(type));
			if (item.ModItem is ArmorRuneItem rune) {
				rune.Rarity = rarity;
				rune.Chance = chance >= 0 ? RuneOdds.Clamp(rarity, chance) : RuneOdds.Roll(rarity, rng, shop);
				item.rare = RarityDefs.Vanilla(rarity);
			}
			return item;
		}

		public static int TypeFor(ArmorRuneType type) => type switch {
			ArmorRuneType.Fortitude => ModContent.ItemType<FortitudeRune>(),
			ArmorRuneType.Vigor => ModContent.ItemType<VigorRune>(),
			ArmorRuneType.Swiftness => ModContent.ItemType<SwiftnessRune>(),
			ArmorRuneType.Warding => ModContent.ItemType<WardingRune>(),
			ArmorRuneType.Renewal => ModContent.ItemType<RenewalRune>(),
			ArmorRuneType.Fury => ModContent.ItemType<FuryRune>(),
			ArmorRuneType.Zephyr => ModContent.ItemType<ZephyrRune>(),
			ArmorRuneType.Focus => ModContent.ItemType<FocusRune>(),
			ArmorRuneType.Fortune => ModContent.ItemType<FortuneRune>(),
			_ => ModContent.ItemType<ThornsRune>()
		};

		public override void UpdateInventory(Player player) => Item.rare = RarityDefs.Vanilla(Rarity);

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			TooltipLine name = tooltips.Find(t => t.Name == "ItemName" && t.Mod == "Terraria");
			if (name != null) {
				name.Text = $"{RarityDefs.Name(Rarity)} {name.Text}";
				name.OverrideColor = RarityDefs.Color(Rarity);
			}
			tooltips.Add(new TooltipLine(Mod, "Effect", "Armor rune: " + ArmorRuneDefs.Describe(Socket)) { OverrideColor = ArmorRuneDefs.Get(Kind).Color });
			tooltips.Add(new TooltipLine(Mod, "Odds", $"Inscription odds: {RuneOdds.Tag(Chance)}  (a failure shatters the rune)"));
			tooltips.Add(new TooltipLine(Mod, "How", "Inscribe it onto armor at a Runeforge (costs gold)"));
			tooltips.Add(new TooltipLine(Mod, "Keep", "Kept if you return home alive; lost if you die") { OverrideColor = new Color(150, 150, 170) });
			if (Rarity < Rarity.Legendary)
				tooltips.Add(new TooltipLine(Mod, "Fuse", "Carry 3 identical runes to fuse them into a higher rarity (keeps the best odds, +6%)") { OverrideColor = new Color(150, 150, 170) });
		}

		public override void SaveData(TagCompound tag)
		{
			tag["rarity"] = (byte)Rarity;
			tag["odds"] = Chance;
		}

		public override void LoadData(TagCompound tag)
		{
			Rarity = (Rarity)tag.GetByte("rarity");
			Chance = tag.ContainsKey("odds") ? RuneOdds.Clamp(Rarity, tag.GetInt("odds")) : RuneOdds.Roll(Rarity);
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write((byte)Rarity);
			writer.Write((byte)Chance);
		}

		public override void NetReceive(BinaryReader reader)
		{
			Rarity = (Rarity)reader.ReadByte();
			Chance = reader.ReadByte();
		}
	}

	public class FortitudeRune : ArmorRuneItem { public override ArmorRuneType Kind => ArmorRuneType.Fortitude; }
	public class VigorRune : ArmorRuneItem { public override ArmorRuneType Kind => ArmorRuneType.Vigor; }
	public class SwiftnessRune : ArmorRuneItem { public override ArmorRuneType Kind => ArmorRuneType.Swiftness; }
	public class WardingRune : ArmorRuneItem { public override ArmorRuneType Kind => ArmorRuneType.Warding; }
	public class RenewalRune : ArmorRuneItem { public override ArmorRuneType Kind => ArmorRuneType.Renewal; }
	public class FuryRune : ArmorRuneItem { public override ArmorRuneType Kind => ArmorRuneType.Fury; }
	public class ZephyrRune : ArmorRuneItem { public override ArmorRuneType Kind => ArmorRuneType.Zephyr; }
	public class FocusRune : ArmorRuneItem { public override ArmorRuneType Kind => ArmorRuneType.Focus; }
	public class FortuneRune : ArmorRuneItem { public override ArmorRuneType Kind => ArmorRuneType.Fortune; }
	public class ThornsRune : ArmorRuneItem { public override ArmorRuneType Kind => ArmorRuneType.Thorns; }
}
