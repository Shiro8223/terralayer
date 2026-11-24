using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terralayer.Common.Depths;
using Terralayer.Common.Items;
using Terralayer.Content.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Terralayer.Common.Quests
{
	/// <summary>
	/// Challenges posted by the Herald. Floor-based ones (most of them) are judged at the moment that floor's guardian
	/// falls; the rest track a single descent or carry progress across descents.
	/// </summary>
	public enum QuestType : byte
	{
		Guardian,     // slay floor F's guardian
		Purist,       // ...using only the starting weapon, never evolved
		Flawless,     // ...taking at most 3 hits during the fight
		Swift,        // ...within M minutes of arriving on the floor
		Ascetic,      // ...without taking a Boon this descent
		Sober,        // ...without drinking a healing potion this descent
		Hoarder,      // come home with G gold earned in one descent
		Slayer,       // slay N champions (cumulative)
		Cartographer, // enter every room of floor F in one descent
		Burdened,     // slay floor F's guardian with an heir carrying 2+ burdens
		Pilgrim,      // use N shrines in one descent
		DeepDive,     // from floor 1, reach floor F in one descent
		Count
	}

	public enum RewardKind : byte
	{
		Gold,           // Amount = gold coins
		WeaponRune,     // a weapon rune of Rarity
		ArmorRune,      // an armor rune of Rarity
		Potions,        // Amount healing potions for the next descent
		Tempering,      // Amount refinement levels on the current heir's weapon (kept while the heir lives)
		Blessing        // Amount random boons at the start of the next descent
	}

	public struct QuestReward
	{
		public RewardKind Kind;
		public int Amount;
		public Rarity Rarity;

		public QuestReward(RewardKind kind, int amount, Rarity rarity = Rarity.Common)
		{
			Kind = kind;
			Amount = amount;
			Rarity = rarity;
		}

		public TagCompound Save() => new() { ["k"] = (byte)Kind, ["a"] = Amount, ["r"] = (byte)Rarity };

		public static QuestReward Load(TagCompound t) => new((RewardKind)t.GetByte("k"), t.GetInt("a"), (Rarity)t.GetByte("r"));
	}

	public class Quest
	{
		public QuestType Type;
		public int Param;
		public int Stars;
		public int Progress;
		public bool Complete;
		public List<QuestReward> Rewards = new();

		public TagCompound Save() => new() {
			["type"] = (byte)Type,
			["param"] = Param,
			["stars"] = Stars,
			["progress"] = Progress,
			["complete"] = Complete,
			["rewards"] = Rewards.Select(r => r.Save()).ToList(),
		};

		public static Quest Load(TagCompound t)
		{
			var q = new Quest {
				Type = (QuestType)t.GetByte("type"),
				Param = t.GetInt("param"),
				Stars = Math.Clamp(t.GetInt("stars"), 1, 5),
				Progress = t.GetInt("progress"),
				Complete = t.GetBool("complete"),
				Rewards = t.GetList<TagCompound>("rewards").Select(QuestReward.Load).ToList(),
			};
			return q.Type < QuestType.Count ? q : null;
		}

		public bool Same(Quest o) => o != null && o.Type == Type && o.Param == Param;
	}

	public static class QuestDefs
	{
		public const int BoardSize = 5;
		public const int MaxActive = 3;
		public const long RerollCost = 2 * Utilities.Coins.Gold;

		private static readonly int[] HoarderGold = { 25, 60, 120, 250 };
		private static readonly int[] SlayerCounts = { 3, 6, 10 };

		public static int SwiftMinutes(int floor) => 5 + 2 * floor;

		/// <summary>One colour per star rating: green, blue, purple, orange, crimson.</summary>
		public static Color StarColor(int stars) => stars switch {
			1 => new Color(130, 225, 120),
			2 => new Color(90, 190, 255),
			3 => new Color(195, 125, 255),
			4 => new Color(255, 165, 60),
			_ => new Color(255, 70, 95)
		};

		public static string Tier(int stars) => stars switch {
			1 => "Novice",
			2 => "Adept",
			3 => "Veteran",
			4 => "Heroic",
			_ => "Legendary"
		};

		public static string Title(Quest q) => q.Type switch {
			QuestType.Guardian => $"Bounty: Guardian of the {FloorDefs.Get(q.Param).Name}",
			QuestType.Purist => "The Purist",
			QuestType.Flawless => "Untouchable",
			QuestType.Swift => "Swift Descent",
			QuestType.Ascetic => "The Ascetic",
			QuestType.Sober => "Teetotal",
			QuestType.Hoarder => "Hoarder",
			QuestType.Slayer => "Champion Hunter",
			QuestType.Cartographer => "Cartographer",
			QuestType.Burdened => "Burdened Blood",
			QuestType.Pilgrim => "Pilgrim",
			QuestType.DeepDive => "Deep Dive",
			_ => "?"
		};

		/// <param name="weapon">The current heir's starting weapon, for the Purist's text.</param>
		public static string Describe(Quest q, string weapon)
		{
			int f = q.Param;
			string guardian = $"Slay floor {f}'s guardian ({FloorDefs.Get(f).Name})";
			return q.Type switch {
				QuestType.Guardian => $"{guardian}.",
				QuestType.Purist => $"{guardian} using only your starting weapon ({weapon}). It may not evolve on the way.",
				QuestType.Flawless => $"{guardian}, taking no more than 3 hits during the fight.",
				QuestType.Swift => $"{guardian} within {SwiftMinutes(f)} minutes of arriving on the floor.",
				QuestType.Ascetic => $"{guardian} without taking a single Boon on that descent.",
				QuestType.Sober => $"{guardian} without drinking a healing potion on that descent.",
				QuestType.Hoarder => $"Come home alive with at least {q.Param} gold earned in a single descent.",
				QuestType.Slayer => $"Slay {q.Param} champions. Progress carries over between descents.",
				QuestType.Cartographer => $"Set foot in every room of floor {f} ({FloorDefs.Get(f).Name}) in a single descent.",
				QuestType.Burdened => $"{guardian} with an heir who carries at least 2 burdens.",
				QuestType.Pilgrim => $"Use {q.Param} shrines in a single descent.",
				QuestType.DeepDive => $"Starting from floor 1, reach floor {f} ({FloorDefs.Get(f).Name}) in a single descent.",
				_ => ""
			};
		}

		/// <summary>Quests whose progress is a count (shown as x/y).</summary>
		public static int Target(Quest q) => q.Type switch {
			QuestType.Slayer => q.Param,
			QuestType.Pilgrim => q.Param,
			_ => 1
		};

		// ------------------------------------------------------------------ generation

		/// <summary>Rolls a quest that isn't already on the board or accepted. Floors stay within reach: up to one past the deepest opened.</summary>
		public static Quest Generate(Random rng, int maxFloor, IEnumerable<Quest> avoid)
		{
			int top = Math.Clamp(maxFloor + 1, 1, FloorDefs.Count);
			var taken = avoid.Where(a => a != null).ToList();
			for (int attempt = 0; attempt < 60; attempt++) {
				var q = new Quest { Type = (QuestType)rng.Next((int)QuestType.Count) };
				int f = rng.Next(1, top + 1);
				switch (q.Type) {
					case QuestType.Guardian:
						q.Param = f;
						q.Stars = f;
						break;
					case QuestType.Purist:
					case QuestType.Flawless:
					case QuestType.Swift:
					case QuestType.Sober:
					case QuestType.Cartographer:
					case QuestType.Burdened:
						q.Param = f;
						q.Stars = Math.Min(5, f + 1);
						break;
					case QuestType.Ascetic:
						q.Param = f;
						q.Stars = f;
						break;
					case QuestType.Hoarder: {
						int i = rng.Next(Math.Min(HoarderGold.Length, top));
						q.Param = HoarderGold[i];
						q.Stars = i + 1;
						break;
					}
					case QuestType.Slayer: {
						int i = rng.Next(SlayerCounts.Length);
						q.Param = SlayerCounts[i];
						q.Stars = i + 1;
						break;
					}
					case QuestType.Pilgrim:
						if (top < 2)
							continue; // a shrine per floor at most: needs two floors
						q.Param = top >= 3 && rng.NextDouble() < 0.4 ? 3 : 2;
						q.Stars = q.Param == 3 ? 4 : 3;
						break;
					case QuestType.DeepDive:
						if (top < 2)
							continue;
						q.Param = rng.Next(2, top + 1);
						q.Stars = q.Param;
						break;
				}
				if (taken.Any(t => t.Same(q)))
					continue;
				q.Rewards = RollRewards(q.Stars, rng);
				return q;
			}
			// Fallback: a plain bounty.
			var bounty = new Quest { Type = QuestType.Guardian, Param = 1, Stars = 1 };
			bounty.Rewards = RollRewards(1, rng);
			return bounty;
		}

		/// <summary>An array of rewards that grows with the star rating.</summary>
		public static List<QuestReward> RollRewards(int stars, Random rng)
		{
			int[] goldBase = { 0, 6, 15, 35, 70, 140 };
			var list = new List<QuestReward> {
				new(RewardKind.Gold, Math.Max(1, (int)Math.Round(goldBase[stars] * (0.85 + rng.NextDouble() * 0.3))))
			};
			if (stars >= 2 || rng.NextDouble() < 0.6)
				list.Add(new(rng.Next(2) == 0 ? RewardKind.WeaponRune : RewardKind.ArmorRune, 1, (Rarity)Math.Clamp(stars - 1, 0, (int)Rarity.Legendary)));
			if (stars >= 2 && rng.NextDouble() < 0.6)
				list.Add(new(RewardKind.Potions, 1 + stars));
			if (stars >= 3)
				list.Add(new(RewardKind.Tempering, stars - 2));
			if (stars >= 4)
				list.Add(new(RewardKind.Blessing, stars - 3));
			return list;
		}

		// ------------------------------------------------------------------ reward display

		public static int RewardIcon(QuestReward r) => r.Kind switch {
			RewardKind.Gold => ItemID.GoldCoin,
			RewardKind.WeaponRune => ModContent.ItemType<StarfallRune>(),
			RewardKind.ArmorRune => ModContent.ItemType<FortitudeRune>(),
			RewardKind.Potions => ItemID.HealingPotion,
			RewardKind.Tempering => ItemID.IronAnvil,
			RewardKind.Blessing => ItemID.FallenStar,
			_ => ItemID.None
		};

		/// <summary>Short label drawn next to the icon.</summary>
		public static string RewardShort(QuestReward r) => r.Kind switch {
			RewardKind.Gold => $"{r.Amount}g",
			RewardKind.WeaponRune or RewardKind.ArmorRune => RarityDefs.Name(r.Rarity),
			RewardKind.Potions => $"x{r.Amount}",
			RewardKind.Tempering => $"+{r.Amount}",
			RewardKind.Blessing => $"x{r.Amount}",
			_ => ""
		};

		public static Color RewardColor(QuestReward r) => r.Kind switch {
			RewardKind.Gold => new Color(255, 215, 90),
			RewardKind.WeaponRune or RewardKind.ArmorRune => RarityDefs.Color(r.Rarity),
			RewardKind.Blessing => new Color(255, 215, 140),
			_ => new Color(230, 230, 230)
		};

		private static string Article(string word) => "AEIOU".Contains(char.ToUpperInvariant(word[0])) ? "An" : "A";

		/// <summary>Full description, for hover text.</summary>
		public static string RewardText(QuestReward r) => r.Kind switch {
			RewardKind.Gold => $"{r.Amount} gold",
			RewardKind.WeaponRune => $"{Article(RarityDefs.Name(r.Rarity))} {RarityDefs.Name(r.Rarity)} weapon rune",
			RewardKind.ArmorRune => $"{Article(RarityDefs.Name(r.Rarity))} {RarityDefs.Name(r.Rarity)} armor rune",
			RewardKind.Potions => $"{r.Amount} healing potions for your next descent",
			RewardKind.Tempering => $"+{r.Amount} refinement on your heir's weapon, kept while the heir lives (gold instead if it's maxed)",
			RewardKind.Blessing => $"{r.Amount} random Boon(s) at the start of your next descent",
			_ => ""
		};
	}
}
