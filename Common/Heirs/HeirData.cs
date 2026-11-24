using System;
using System.Collections.Generic;
using System.Linq;
using Terralayer.Common.Items;
using Terraria;
using Terraria.ModLoader.IO;

namespace Terralayer.Common.Heirs
{
	/// <summary>A candidate heir: name, class (an armor kit on a path), traits, and the runes on their gear.</summary>
	public class HeirData
	{
		public string Name = "";
		public string KitKey = "";
		/// <summary>Which flavour of the class this heir is (e.g. "Slime Tamer"); empty or the class name for the default.</summary>
		public string Variant = "";
		public HeirClass Class;
		public List<HeirTrait> Traits = new();
		public int WeaponType;
		public Rarity WeaponRarity;
		public int WeaponLevel = 1;

		/// <summary>Runes inscribed on this heir's weapon at the Runeforge. Kept while the heir lives.</summary>
		public List<RuneSocket> Runes = new();

		/// <summary>Armor runes on this heir's head, body and legs pieces (-1 = empty socket).</summary>
		public int[] ArmorRunes = { -1, -1, -1 };

		public ClassKit Kit => ClassKits.Get(KitKey) ?? ClassKits.Starter(Class);

		public string FullName => $"{ClassDefs.Get(Class).Title} {Name}";

		/// <summary>"Acolyte" or "Acolyte (Slime Tamer)".</summary>
		public string ClassLabel => string.IsNullOrEmpty(Variant) || Variant == Kit.Name ? Kit.Name : $"{Kit.Name} ({Variant})";

		public float GoldBonus => Traits.Sum(t => TraitDefs.Get(t).GoldBonus);

		public Item CreateWeapon() => RefinedWeapon.Create(WeaponType, WeaponRarity, WeaponLevel, Runes);

		/// <summary>The kit's armor piece for slot 0/1/2 with this heir's armor rune applied, or null.</summary>
		public Item CreateArmor(int slot)
		{
			int type = Kit.Armor[slot];
			if (type <= 0)
				return null;
			var item = new Item(type);
			var r = RunedArmor.Of(item);
			if (r != null && ArmorRunes[slot] >= 0) {
				r.HasRune = true;
				r.Rune = ArmorSocket.Decode(ArmorRunes[slot]);
			}
			return item;
		}

		public TagCompound Save() => new() {
			["name"] = Name,
			["kit"] = KitKey,
			["variant"] = Variant,
			["class"] = (int)Class,
			["traits"] = Traits.Select(t => (int)t).ToList(),
			["weapon"] = WeaponType,
			["wrarity"] = (byte)WeaponRarity,
			["wlevel"] = WeaponLevel,
			["runes"] = Runes.Select(r => (int)r.Type * 16 + (int)r.Rarity).ToList(),
			["armorRunes"] = ArmorRunes.ToList(),
		};

		public static HeirData Load(TagCompound tag)
		{
			var heir = new HeirData {
				Name = tag.GetString("name"),
				KitKey = tag.GetString("kit"),
				Variant = tag.ContainsKey("variant") ? tag.GetString("variant") : "",
				Class = (HeirClass)Math.Clamp(tag.GetInt("class"), 0, (int)HeirClass.Count - 1),
				Traits = tag.GetList<int>("traits").Select(t => (HeirTrait)t).Where(t => t < HeirTrait.Count).ToList(),
				WeaponType = tag.GetInt("weapon"),
				WeaponRarity = (Rarity)tag.GetByte("wrarity"),
				WeaponLevel = Math.Max(1, tag.GetInt("wlevel")),
				Runes = tag.GetList<int>("runes").Select(v => new RuneSocket((RuneType)(v / 16), (Rarity)(v % 16))).ToList(),
			};
			var armor = tag.GetList<int>("armorRunes");
			for (int i = 0; i < 3 && i < armor.Count; i++)
				heir.ArmorRunes[i] = armor[i];

			// Saves from before classes were armor kits: use the path's starter class.
			if (ClassKits.Get(heir.KitKey) == null)
				heir.KitKey = ClassKits.Starter(heir.Class).Key;
			if (tag.GetBool("hasRune") && heir.Runes.Count == 0) {
				int rune = tag.GetInt("rune");
				heir.Runes.Add(new RuneSocket((RuneType)(rune / 16), (Rarity)(rune % 16)));
			}
			return heir;
		}
	}

	public class AncestorRecord
	{
		public string Name = "";
		public HeirClass Class;
		public int Generation;
		public string Fate = "";

		public TagCompound Save() => new() { ["name"] = Name, ["class"] = (int)Class, ["gen"] = Generation, ["fate"] = Fate };

		public static AncestorRecord Load(TagCompound tag) => new() {
			Name = tag.GetString("name"),
			Class = (HeirClass)tag.GetInt("class"),
			Generation = tag.GetInt("gen"),
			Fate = tag.GetString("fate"),
		};
	}

	public static class HeirGenerator
	{
		private static readonly string[] Names = {
			"Aldric", "Berrin", "Cassia", "Doran", "Elowen", "Fenwick", "Garrick", "Hesper", "Isolde", "Jorund", "Kaela",
			"Linnea", "Maren", "Nyssa", "Orrin", "Pell", "Quill", "Rowena", "Soren", "Tamsin", "Ulric", "Vesper", "Wren",
			"Yara", "Zephyr", "Adela", "Bram", "Corin", "Dagny", "Edric", "Freya", "Gideon", "Hollis", "Ilse", "Jessamy",
		};

		/// <summary>Rolls heirs from the unlocked classes, preferring higher tiers a little so unlocks feel earned.</summary>
		public static List<HeirData> Roll(IReadOnlyCollection<string> unlockedKits, int count, int startLevel, float runeAffinity)
		{
			var rng = new Random(Main.rand.Next());
			var pool = unlockedKits.Select(ClassKits.Get).Where(k => k != null).ToList();
			if (pool.Count == 0)
				pool = ClassKits.All.Where(ClassKits.IsStarter).ToList();

			var result = new List<HeirData>();
			var usedNames = new HashSet<string>();
			var usedKits = new HashSet<string>();
			for (int i = 0; i < count; i++) {
				var choices = pool.Where(k => !usedKits.Contains(k.Key)).ToList();
				if (choices.Count == 0)
					choices = pool;
				int totalWeight = choices.Sum(k => 2 + k.Tier);
				int roll = rng.Next(totalWeight);
				ClassKit kit = choices[0];
				foreach (ClassKit k in choices) {
					roll -= 2 + k.Tier;
					if (roll < 0) {
						kit = k;
						break;
					}
				}
				usedKits.Add(kit.Key);
				var variants = ClassKits.VariantsOf(kit);
				ClassVariant variant = variants[rng.Next(variants.Count)];

				string name;
				do {
					name = Names[rng.Next(Names.Length)];
				} while (!usedNames.Add(name));

				var traits = new List<HeirTrait>();
				int traitCount = 1 + (rng.NextDouble() < 0.55 ? 1 : 0) + (rng.NextDouble() < 0.2 ? 1 : 0);
				int guard = 0;
				while (traits.Count < traitCount && guard++ < 50) {
					var t = (HeirTrait)rng.Next((int)HeirTrait.Count);
					if (!traits.Contains(t) && !traits.Any(o => TraitDefs.Conflicts(o, t)))
						traits.Add(t);
				}

				var heir = new HeirData {
					Name = name,
					KitKey = kit.Key,
					Variant = variant.Name,
					Class = kit.Path,
					Traits = traits,
					WeaponType = variant.Weapon,
					WeaponRarity = rng.NextDouble() < 0.15 ? Rarity.Uncommon : Rarity.Common,
					WeaponLevel = startLevel,
				};
				if (rng.NextDouble() < runeAffinity)
					heir.Runes.Add(new RuneSocket((RuneType)rng.Next((int)RuneType.Count), Rarity.Common));
				result.Add(heir);
			}
			return result;
		}
	}
}
