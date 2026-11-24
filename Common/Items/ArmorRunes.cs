using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terralayer.Common.Players;
using Terralayer.Common.World;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Terralayer.Common.Items
{
	public enum ArmorRuneType : byte
	{
		Fortitude,
		Vigor,
		Swiftness,
		Warding,
		Renewal,
		Fury,
		Zephyr,
		Focus,
		Fortune,
		Thorns,
		Count
	}

	public struct ArmorSocket
	{
		public ArmorRuneType Type;
		public Rarity Rarity;

		public ArmorSocket(ArmorRuneType type, Rarity rarity)
		{
			Type = type;
			Rarity = rarity;
		}

		public int Encode() => (int)Type * 16 + (int)Rarity;

		public static ArmorSocket Decode(int v) => new((ArmorRuneType)(v / 16), (Rarity)(v % 16));
	}

	public record ArmorRuneInfo(string Name, Color Color);

	public static class ArmorRuneDefs
	{
		private static readonly ArmorRuneInfo[] Infos = {
			new("Fortitude", new Color(170, 170, 200)),
			new("Vigor", new Color(230, 70, 80)),
			new("Swiftness", new Color(120, 230, 200)),
			new("Warding", new Color(110, 160, 255)),
			new("Renewal", new Color(120, 230, 110)),
			new("Fury", new Color(255, 120, 60)),
			new("Zephyr", new Color(200, 240, 255)),
			new("Focus", new Color(160, 120, 255)),
			new("Fortune", new Color(255, 210, 60)),
			new("Thorns", new Color(160, 200, 90)),
		};

		public static ArmorRuneInfo Get(ArmorRuneType t) => Infos[(int)t];

		public static string Describe(ArmorSocket s)
		{
			float p = RarityDefs.Potency(s.Rarity);
			return s.Type switch {
				ArmorRuneType.Fortitude => $"+{(int)(2 * p)} defense",
				ArmorRuneType.Vigor => $"+{(int)(10 * p)} max life",
				ArmorRuneType.Swiftness => $"+{(int)(5 * p)}% movement speed",
				ArmorRuneType.Warding => $"{(int)(3 * p)}% less damage taken",
				ArmorRuneType.Renewal => $"+{p:0.#} life regeneration",
				ArmorRuneType.Fury => $"+{(int)(3 * p)}% crit chance",
				ArmorRuneType.Zephyr => p >= 1.7f ? "Higher jumps, no fall damage" : "Higher jumps",
				ArmorRuneType.Focus => $"+{(int)(15 * p)} max mana, faster mana regen",
				ArmorRuneType.Fortune => $"+{(int)(5 * p)}% gold found",
				ArmorRuneType.Thorns => $"Reflect {(int)(30 * p)}% of contact damage",
				_ => ""
			};
		}

		public static void Apply(Player player, ArmorSocket s)
		{
			float p = RarityDefs.Potency(s.Rarity);
			switch (s.Type) {
				case ArmorRuneType.Fortitude: player.statDefense += (int)(2 * p); break;
				case ArmorRuneType.Vigor: player.statLifeMax2 += (int)(10 * p); break;
				case ArmorRuneType.Swiftness: player.moveSpeed += 0.05f * p; break;
				case ArmorRuneType.Warding: player.endurance += 0.03f * p; break;
				case ArmorRuneType.Renewal: player.lifeRegen += (int)(2 * p); break;
				case ArmorRuneType.Fury: player.GetCritChance(DamageClass.Generic) += 3 * p; break;
				case ArmorRuneType.Zephyr:
					player.jumpSpeedBoost += 0.6f * p;
					if (p >= 1.7f)
						player.noFallDmg = true;
					break;
				case ArmorRuneType.Focus: player.statManaMax2 += (int)(15 * p); player.manaRegenBonus += (int)(10 * p); break;
				case ArmorRuneType.Fortune: player.GetModPlayer<LegacyPlayer>().ArmorGoldBonus += 0.05f * p; break;
				case ArmorRuneType.Thorns: player.thorns += 0.3f * p; break;
			}
		}
	}

	/// <summary>One armor-rune socket on every real armor piece (helmet, chest, legs).</summary>
	public class RunedArmor : GlobalItem
	{
		public override bool InstancePerEntity => true;

		public bool HasRune;
		public ArmorSocket Rune;

		public static bool IsArmor(Item item) => (item.headSlot >= 0 || item.bodySlot >= 0 || item.legSlot >= 0) && !item.vanity && !item.accessory;

		public override bool AppliesToEntity(Item entity, bool lateInstantiation) => lateInstantiation && IsArmor(entity);

		public static RunedArmor Of(Item item) => item != null && !item.IsAir && item.TryGetGlobalItem(out RunedArmor r) ? r : null;

		public override void UpdateEquip(Item item, Player player)
		{
			if (HasRune && TerralayerWorld.Active)
				ArmorRuneDefs.Apply(player, Rune);
		}

		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			if (!TerralayerWorld.Active)
				return;
			tooltips.Add(HasRune
				? new TooltipLine(Mod, "ArmorRune", $"{ArmorRuneDefs.Get(Rune.Type).Name} Rune ({RarityDefs.Name(Rune.Rarity)}): {ArmorRuneDefs.Describe(Rune)}") { OverrideColor = RarityDefs.Color(Rune.Rarity) }
				: new TooltipLine(Mod, "ArmorSocket", "Empty armor-rune socket") { OverrideColor = new Color(140, 140, 150) });
		}

		public override void SaveData(Item item, TagCompound tag)
		{
			if (HasRune)
				tag["armorRune"] = Rune.Encode();
		}

		public override void LoadData(Item item, TagCompound tag)
		{
			HasRune = tag.ContainsKey("armorRune");
			if (HasRune)
				Rune = ArmorSocket.Decode(tag.GetInt("armorRune"));
		}

		public override void NetSend(Item item, BinaryWriter writer)
		{
			writer.Write(HasRune);
			writer.Write((byte)Rune.Encode());
		}

		public override void NetReceive(Item item, BinaryReader reader)
		{
			HasRune = reader.ReadBoolean();
			Rune = ArmorSocket.Decode(reader.ReadByte());
		}
	}
}
