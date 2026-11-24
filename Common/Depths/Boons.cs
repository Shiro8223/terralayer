using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Terralayer.Common.Depths
{
	/// <summary>Small stat boosts picked after clearing a room. They stack, and last until the descent ends.</summary>
	public enum BoonStat
	{
		Damage,
		Life,
		Defense,
		Crit,
		Speed,
		AttackSpeed,
		Regen,
		Mana,
		GoldFind,
		Count
	}

	public record BoonInfo(string Name, string PerStack, Color Color);

	public static class Boons
	{
		private static readonly BoonInfo[] Infos = {
			new("Might", "+4% damage", new Color(255, 120, 90)),
			new("Vigor", "+15 max life", new Color(230, 70, 90)),
			new("Stone Skin", "+2 defense", new Color(170, 170, 200)),
			new("Precision", "+3% crit chance", new Color(255, 200, 90)),
			new("Fleetness", "+5% movement speed", new Color(120, 230, 200)),
			new("Frenzy", "+4% attack speed", new Color(255, 160, 60)),
			new("Renewal", "+1 life regeneration", new Color(120, 230, 110)),
			new("Insight", "+20 max mana", new Color(120, 140, 255)),
			new("Greed", "+6% gold found", new Color(255, 215, 60)),
		};

		public static BoonInfo Get(BoonStat s) => Infos[(int)s];

		/// <summary>Everything except life and mana (those go through ModifyMaxStats).</summary>
		public static void Apply(Player p, int[] stacks, ref float goldFind)
		{
			p.GetDamage(DamageClass.Generic) += 0.04f * stacks[(int)BoonStat.Damage];
			p.statDefense += 2 * stacks[(int)BoonStat.Defense];
			p.GetCritChance(DamageClass.Generic) += 3 * stacks[(int)BoonStat.Crit];
			p.moveSpeed += 0.05f * stacks[(int)BoonStat.Speed];
			p.GetAttackSpeed(DamageClass.Generic) += 0.04f * stacks[(int)BoonStat.AttackSpeed];
			p.lifeRegen += 2 * stacks[(int)BoonStat.Regen];
			goldFind += 0.06f * stacks[(int)BoonStat.GoldFind];
		}

		/// <summary>"+8% damage, +30 max life" style summary of the boons held.</summary>
		public static string Summary(int[] stacks)
		{
			var parts = new List<string>();
			if (stacks[(int)BoonStat.Damage] > 0) parts.Add($"+{4 * stacks[(int)BoonStat.Damage]}% dmg");
			if (stacks[(int)BoonStat.Life] > 0) parts.Add($"+{15 * stacks[(int)BoonStat.Life]} life");
			if (stacks[(int)BoonStat.Defense] > 0) parts.Add($"+{2 * stacks[(int)BoonStat.Defense]} def");
			if (stacks[(int)BoonStat.Crit] > 0) parts.Add($"+{3 * stacks[(int)BoonStat.Crit]}% crit");
			if (stacks[(int)BoonStat.Speed] > 0) parts.Add($"+{5 * stacks[(int)BoonStat.Speed]}% speed");
			if (stacks[(int)BoonStat.AttackSpeed] > 0) parts.Add($"+{4 * stacks[(int)BoonStat.AttackSpeed]}% atk speed");
			if (stacks[(int)BoonStat.Regen] > 0) parts.Add($"+{stacks[(int)BoonStat.Regen]} regen");
			if (stacks[(int)BoonStat.Mana] > 0) parts.Add($"+{20 * stacks[(int)BoonStat.Mana]} mana");
			if (stacks[(int)BoonStat.GoldFind] > 0) parts.Add($"+{6 * stacks[(int)BoonStat.GoldFind]}% gold");
			return string.Join(", ", parts);
		}

		public static BoonStat Random(Random rng) => (BoonStat)rng.Next((int)BoonStat.Count);
	}
}
