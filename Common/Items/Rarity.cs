using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace Terralayer.Common.Items
{
	public enum Rarity : byte
	{
		Common,
		Uncommon,
		Rare,
		Epic,
		Legendary
	}

	public static class RarityDefs
	{
		public static string Name(Rarity r) => r.ToString();

		public static Color Color(Rarity r) => r switch {
			Rarity.Common => new Color(210, 210, 210),
			Rarity.Uncommon => new Color(110, 230, 110),
			Rarity.Rare => new Color(90, 160, 255),
			Rarity.Epic => new Color(200, 110, 255),
			_ => new Color(255, 175, 40)
		};

		/// <summary>Closest vanilla rarity, so item names and drop text get the right color everywhere.</summary>
		public static int Vanilla(Rarity r) => r switch {
			Rarity.Common => ItemRarityID.White,
			Rarity.Uncommon => ItemRarityID.Green,
			Rarity.Rare => ItemRarityID.Blue,
			Rarity.Epic => ItemRarityID.LightPurple,
			_ => ItemRarityID.Orange
		};

		public static float DamageBonus(Rarity r) => r switch {
			Rarity.Common => 0f,
			Rarity.Uncommon => 0.06f,
			Rarity.Rare => 0.14f,
			Rarity.Epic => 0.24f,
			_ => 0.36f
		};

		public static int CritBonus(Rarity r) => (int)r;

		public static int RuneSlots(Rarity r) => r switch {
			Rarity.Common => 1,
			Rarity.Uncommon => 1,
			Rarity.Rare => 2,
			Rarity.Epic => 2,
			_ => 3
		};

		/// <summary>How strongly a rune of this rarity works.</summary>
		public static float Potency(Rarity r) => r switch {
			Rarity.Common => 1.0f,
			Rarity.Uncommon => 1.3f,
			Rarity.Rare => 1.7f,
			Rarity.Epic => 2.2f,
			_ => 3.0f
		};

		/// <summary>Rolls a rarity. Each point of <paramref name="bias"/> shifts the odds one step up.</summary>
		public static Rarity Roll(int bias = 0, Random rng = null)
		{
			double roll = rng?.NextDouble() ?? Main.rand.NextDouble();
			Rarity r = roll switch {
				< 0.70 => Rarity.Common,
				< 0.92 => Rarity.Uncommon,
				< 0.985 => Rarity.Rare,
				< 0.999 => Rarity.Epic,
				_ => Rarity.Legendary
			};
			return (Rarity)Math.Clamp((int)r + bias, 0, (int)Rarity.Legendary);
		}

		public static string Tag(Rarity r, string text) => $"[c/{Color(r).Hex3()}:{text}]";
	}
}
