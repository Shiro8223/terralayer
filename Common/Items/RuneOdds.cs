using System;
using Terraria;

namespace Terralayer.Common.Items
{
	/// <summary>
	/// Every loose rune stone carries an inscription chance: the odds it takes when the Runeforge sears it onto gear.
	/// A failed inscription shatters the rune (the fee is still paid). Rarer runes roll from a better range, and fusing
	/// three runes into the next rarity keeps the best of the three's odds and adds a little.
	/// </summary>
	public static class RuneOdds
	{
		/// <summary>Lowest chance a rune of this rarity can roll: never below 30% (Common), +5% per rarity.</summary>
		public static int Min(Rarity r) => 30 + 5 * (int)r;

		/// <summary>Highest chance a rune of this rarity can roll: Common 83%, then +4% per rarity (Legendary 99%).</summary>
		public static int Max(Rarity r) => Math.Min(99, 83 + 4 * (int)r);

		/// <summary>How much fusing three runes improves on the best of them.</summary>
		public const int FuseBonus = 6;

		/// <summary>Shops sell "unproven" stock: their runes top out 20 points lower.</summary>
		public const int ShopPenalty = 20;

		public static int Roll(Rarity r, Random rng = null, bool shop = false)
		{
			int min = Min(r);
			int max = shop ? Math.Max(min, Max(r) - ShopPenalty) : Max(r);
			return rng != null ? rng.Next(min, max + 1) : Main.rand.Next(min, max + 1);
		}

		/// <summary>The odds of a rune fused from three of the previous rarity whose best odds were <paramref name="best"/>.</summary>
		public static int Fused(Rarity result, int best) => Math.Clamp(Math.Max(best, Min(result)) + FuseBonus, Min(result), Max(result));

		public static int Clamp(Rarity r, int chance) => Math.Clamp(chance, Min(r), Max(r));

		/// <summary>"62% : 38%" with the success share in green and the failure share in red (chat tags).</summary>
		public static string Tag(int chance) => $"[c/5AF064:{chance}%] : [c/FF5A5A:{100 - chance}%]";

		/// <summary>Plain text version for places that don't render tags.</summary>
		public static string Plain(int chance) => $"{chance}% success : {100 - chance}% fail";
	}
}
