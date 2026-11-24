using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terralayer.Common.World;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Terralayer.Common.Items
{
	/// <summary>
	/// Accessories never drop with a modifier. Two copies of the same accessory merge into one a forge stage higher,
	/// and each stage brings a better modifier from the same family: stage 1 green, stage 2 blue, stage 3 yellow.
	/// Innate accessories come from an heir's trait: they can be equipped and unequipped, but not merged or sold.
	/// </summary>
	public class AccessoryForge : GlobalItem
	{
		public const int MaxStage = 3;

		public int Stage;
		public int Family = -1;
		public bool Innate;

		public override bool InstancePerEntity => true;

		public override bool AppliesToEntity(Item entity, bool lateInstantiation) => lateInstantiation && entity.accessory && !entity.vanity;

		public static AccessoryForge Of(Item item) => item != null && !item.IsAir && item.TryGetGlobalItem(out AccessoryForge f) ? f : null;

		/// <summary>Modifier families, each climbing three tiers: defense, damage, movement speed, melee speed.</summary>
		private static readonly int[][] Families = {
			new int[] { PrefixID.Hard, PrefixID.Guarding, PrefixID.Warding },
			new int[] { PrefixID.Jagged, PrefixID.Spiked, PrefixID.Menacing },
			new int[] { PrefixID.Brisk, PrefixID.Fleeting, PrefixID.Quick },
			new int[] { PrefixID.Wild, PrefixID.Rash, PrefixID.Violent },
		};

		public static string FamilyName(int f) => f switch { 0 => "defense", 1 => "damage", 2 => "speed", _ => "melee speed" };

		public static int RarityFor(int stage) => stage switch { 1 => ItemRarityID.Green, 2 => ItemRarityID.Blue, 3 => ItemRarityID.Yellow, _ => -1 };

		public static Color ColorFor(int stage) => stage switch { 1 => new Color(110, 230, 110), 2 => new Color(90, 160, 255), _ => new Color(255, 230, 80) };

		/// <summary>A fresh copy of <paramref name="type"/> at a forge stage, carrying that stage's modifier.</summary>
		public static Item Forged(int type, int stage, int family)
		{
			var item = new Item(type);
			stage = Math.Clamp(stage, 0, MaxStage);
			if (stage > 0)
				item.Prefix(Families[family][stage - 1]);
			var f = Of(item);
			if (f != null) {
				f.Stage = stage;
				f.Family = family;
			}
			if (stage > 0)
				item.rare = RarityFor(stage);
			return item;
		}

		/// <summary>Merges <paramref name="b"/> into <paramref name="a"/>: one stage above the better of the two.</summary>
		public static Item Merge(Item a, Item b)
		{
			var fa = Of(a);
			var fb = Of(b);
			int family = fa.Family >= 0 ? fa.Family : fb.Family >= 0 ? fb.Family : Main.rand.Next(Families.Length);
			return Forged(a.type, Math.Max(fa.Stage, fb.Stage) + 1, family);
		}

		public static bool CanMerge(Item item)
		{
			var f = Of(item);
			return f != null && !f.Innate && f.Stage < MaxStage;
		}

		public override void UpdateInventory(Item item, Player player)
		{
			if (Stage > 0)
				item.rare = RarityFor(Stage);
		}

		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			if (!TerralayerWorld.Active)
				return;
			if (Innate) {
				tooltips.Add(new TooltipLine(Mod, "Innate", "Innate: part of your heir (a trait). Equip or unequip it freely; it can't be merged or sold.") { OverrideColor = new Color(170, 220, 255) });
				return;
			}
			TooltipLine name = tooltips.Find(t => t.Name == "ItemName" && t.Mod == "Terraria");
			if (name != null && Stage > 0)
				name.OverrideColor = ColorFor(Stage);
			string next = Stage >= MaxStage ? "Fully forged." : "Pick up another copy to merge them one stage higher.";
			tooltips.Add(new TooltipLine(Mod, "ForgeStage", $"Forge stage {Stage}/{MaxStage}" + (Family >= 0 ? $" ({FamilyName(Family)})" : "") + $". {next}") {
				OverrideColor = Stage > 0 ? ColorFor(Stage) : new Color(170, 170, 185)
			});
		}

		public override void SaveData(Item item, TagCompound tag)
		{
			if (Stage > 0)
				tag["accStage"] = Stage;
			if (Family >= 0)
				tag["accFamily"] = Family;
			if (Innate)
				tag["accInnate"] = true;
		}

		public override void LoadData(Item item, TagCompound tag)
		{
			Stage = tag.ContainsKey("accStage") ? Math.Clamp(tag.GetInt("accStage"), 0, MaxStage) : 0;
			Family = tag.ContainsKey("accFamily") ? tag.GetInt("accFamily") : -1;
			Innate = tag.ContainsKey("accInnate") && tag.GetBool("accInnate");
		}

		public override void NetSend(Item item, BinaryWriter writer)
		{
			writer.Write((byte)Stage);
			writer.Write((sbyte)Family);
			writer.Write(Innate);
		}

		public override void NetReceive(Item item, BinaryReader reader)
		{
			Stage = reader.ReadByte();
			Family = reader.ReadSByte();
			Innate = reader.ReadBoolean();
		}
	}
}
