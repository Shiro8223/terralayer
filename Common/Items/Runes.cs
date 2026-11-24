using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terralayer.Common.Players;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Common.Items
{
	public enum RuneType : byte
	{
		Reaping,
		Splitting,
		Ember,
		Frost,
		Storm,
		Vampiric,
		Starfall,
		Hive,
		Venom,
		Midas,
		Quake,
		Count
	}

	public record RuneInfo(string Name, string Summary, Color Color);

	public static class RuneDefs
	{
		private static readonly RuneInfo[] Infos = {
			new("Reaping", "Attacks also hurl a demon scythe", new Color(170, 80, 255)),
			new("Splitting", "Attacks split into extra projectiles", new Color(120, 220, 255)),
			new("Ember", "Hits set foes ablaze and may burst into fireballs", new Color(255, 120, 40)),
			new("Frost", "Hits frostburn and slow foes", new Color(140, 220, 255)),
			new("Storm", "Hits may arc lightning to nearby foes", new Color(255, 245, 120)),
			new("Vampiric", "Hits heal you", new Color(220, 40, 60)),
			new("Starfall", "Hits may call down falling stars", new Color(255, 230, 150)),
			new("Hive", "Hits may release bees", new Color(250, 200, 40)),
			new("Venom", "Hits poison foes", new Color(120, 230, 80)),
			new("Midas", "Hits gild foes: they drop more gold", new Color(255, 210, 60)),
			new("Quake", "Every 4th hit erupts in an explosion", new Color(200, 140, 90)),
		};

		public static RuneInfo Get(RuneType t) => Infos[(int)t];

		public static string Describe(RuneSocket r)
		{
			float p = RarityDefs.Potency(r.Rarity);
			return r.Type switch {
				RuneType.Reaping => $"Attacks hurl a demon scythe ({(int)(60 * p)}% damage)",
				RuneType.Splitting => $"Fires {(r.Rarity >= Rarity.Epic ? 2 : 1)} extra projectile(s) ({(int)(40 * Math.Min(p, 2.5f))}% damage)",
				RuneType.Ember => $"Ignites foes; {(int)(12 * p)}% chance to burst a fireball",
				RuneType.Frost => $"Frostburns and slows foes; {(int)(10 * p)}% chance of an ice bolt",
				RuneType.Storm => $"{(int)Math.Min(60, 20 * p)}% chance to arc to {1 + (int)p} foes",
				RuneType.Vampiric => $"Heal {3 * p:0.#}% of damage dealt",
				RuneType.Starfall => $"{(int)Math.Min(60, 15 * p)}% chance to call a falling star",
				RuneType.Hive => $"{(int)Math.Min(70, 25 * p)}% chance to release bees",
				RuneType.Venom => p >= 1.7f ? "Envenoms foes" : "Poisons foes",
				RuneType.Midas => $"Gilds foes for {(int)(5 * p)}s (more coins)",
				RuneType.Quake => $"Every 4th hit explodes ({(int)(80 * p)}% damage)",
				_ => ""
			};
		}
	}

	/// <summary>What runes actually do. Called on the attacking player's own client.</summary>
	public static class RuneEffects
	{
		private static IEntitySource Source(Player p) => p.GetSource_Misc("TerralayerRune");

		public static void OnUse(Player player, Item weapon, List<RuneSocket> runes)
		{
			var lp = player.GetModPlayer<LegacyPlayer>();
			int dmg = player.GetWeaponDamage(weapon);
			Vector2 aim = player.DirectionTo(Main.MouseWorld);

			foreach (RuneSocket r in runes) {
				float p = RarityDefs.Potency(r.Rarity);
				switch (r.Type) {
					case RuneType.Reaping:
						if (lp.RuneReady(RuneType.Reaping, (int)(45 / Math.Sqrt(p)))) {
							Projectile.NewProjectile(Source(player), player.Center, aim * 7f, ProjectileID.DemonScythe, (int)(dmg * 0.6f * p), 3f, player.whoAmI);
						}
						break;

					case RuneType.Splitting:
						// Melee weapons that don't fire anything get a sword beam instead.
						if (weapon.shoot <= ProjectileID.None && lp.RuneReady(RuneType.Splitting, 20))
							Projectile.NewProjectile(Source(player), player.Center, aim * 11f, ProjectileID.EnchantedBeam, (int)(dmg * 0.4f * Math.Min(p, 2.5f)), 2f, player.whoAmI);
						break;
				}
			}
		}

		public static void OnShoot(Player player, Item weapon, List<RuneSocket> runes, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			foreach (RuneSocket r in runes) {
				if (r.Type != RuneType.Splitting)
					continue;
				float p = RarityDefs.Potency(r.Rarity);
				int extra = r.Rarity >= Rarity.Epic ? 2 : 1;
				for (int i = 0; i < extra; i++) {
					float angle = MathHelper.ToRadians(7f * (i + 1)) * (i % 2 == 0 ? 1 : -1);
					Projectile.NewProjectile(Source(player), position, velocity.RotatedBy(angle), type, (int)(damage * 0.4f * Math.Min(p, 2.5f)), knockback, player.whoAmI);
				}
			}
		}

		public static void OnHit(Player player, NPC target, int damageDone, int weaponDamage, List<RuneSocket> runes)
		{
			if (target.friendly || target.lifeMax <= 5 || target.immortal)
				return;
			var lp = player.GetModPlayer<LegacyPlayer>();
			int baseDmg = Math.Max(1, weaponDamage);

			foreach (RuneSocket r in runes) {
				float p = RarityDefs.Potency(r.Rarity);
				switch (r.Type) {
					case RuneType.Ember:
						target.AddBuff(p >= 1.7f ? BuffID.OnFire3 : BuffID.OnFire, 180);
						if (Main.rand.NextFloat() < 0.12f * p && lp.RuneReady(RuneType.Ember, 15))
							Projectile.NewProjectile(Source(player), target.Center, Main.rand.NextVector2Circular(4, 4), ProjectileID.BallofFire, (int)(baseDmg * 0.5f * p), 1f, player.whoAmI);
						break;

					case RuneType.Frost:
						target.AddBuff(p >= 1.7f ? BuffID.Frostburn2 : BuffID.Frostburn, 180);
						if (!target.boss)
							target.velocity *= 0.85f;
						if (Main.rand.NextFloat() < 0.10f * p && lp.RuneReady(RuneType.Frost, 15))
							Projectile.NewProjectile(Source(player), player.Center, player.DirectionTo(target.Center) * 10f, ProjectileID.FrostBoltSword, (int)(baseDmg * 0.5f * p), 1f, player.whoAmI);
						break;

					case RuneType.Storm:
						if (Main.rand.NextFloat() < Math.Min(0.6f, 0.2f * p) && lp.RuneReady(RuneType.Storm, 10))
							ChainLightning(player, target, 1 + (int)p, (int)(baseDmg * 0.5f * p));
						break;

					case RuneType.Vampiric:
						if (lp.RuneReady(RuneType.Vampiric, 20)) {
							int heal = Math.Clamp((int)(damageDone * 0.03f * p), 1, 6 + (int)(2 * p));
							player.Heal(heal);
						}
						break;

					case RuneType.Starfall:
						if (Main.rand.NextFloat() < Math.Min(0.6f, 0.15f * p) && lp.RuneReady(RuneType.Starfall, 20)) {
							Vector2 from = target.Center + new Vector2(Main.rand.Next(-120, 121), -600);
							Projectile.NewProjectile(Source(player), from, Vector2.Normalize(target.Center - from) * 16f, ProjectileID.Starfury, (int)(baseDmg * 0.7f * p), 2f, player.whoAmI);
						}
						break;

					case RuneType.Hive:
						if (Main.rand.NextFloat() < Math.Min(0.7f, 0.25f * p) && lp.RuneReady(RuneType.Hive, 20)) {
							int count = 1 + Main.rand.Next(1 + (int)p);
							for (int i = 0; i < count; i++)
								Projectile.NewProjectile(Source(player), player.Center, Main.rand.NextVector2Circular(3, 3), p >= 2.2f ? ProjectileID.GiantBee : ProjectileID.Bee, (int)(baseDmg * 0.3f * p), 0f, player.whoAmI);
						}
						break;

					case RuneType.Venom:
						target.AddBuff(p >= 1.7f ? BuffID.Venom : BuffID.Poisoned, 240);
						break;

					case RuneType.Midas:
						target.AddBuff(BuffID.Midas, (int)(300 * p));
						break;

					case RuneType.Quake:
						if (++lp.QuakeCounter >= 4) {
							lp.QuakeCounter = 0;
							Projectile.NewProjectile(Source(player), target.Center, Vector2.Zero, ProjectileID.SolarWhipSwordExplosion, (int)(baseDmg * 0.8f * p), 6f, player.whoAmI, 0f, 0.85f + Main.rand.NextFloat() * 1.15f);
						}
						break;
				}
			}
		}

		private static void ChainLightning(Player player, NPC origin, int jumps, int damage)
		{
			var hit = new HashSet<int> { origin.whoAmI };
			NPC from = origin;
			for (int j = 0; j < jumps; j++) {
				NPC next = null;
				float best = 22 * 16;
				foreach (NPC n in Main.ActiveNPCs) {
					if (hit.Contains(n.whoAmI) || n.friendly || n.dontTakeDamage || n.lifeMax <= 5)
						continue;
					float d = Vector2.Distance(n.Center, from.Center);
					if (d < best) {
						best = d;
						next = n;
					}
				}
				if (next == null)
					return;

				for (float t = 0; t <= 1f; t += 0.08f) {
					Dust d = Dust.NewDustPerfect(Vector2.Lerp(from.Center, next.Center, t) + Main.rand.NextVector2Circular(4, 4), DustID.Electric);
					d.noGravity = true;
					d.scale = 0.9f;
				}
				next.SimpleStrikeNPC(damage, Math.Sign(next.Center.X - from.Center.X), damageType: DamageClass.Generic);
				hit.Add(next.whoAmI);
				from = next;
			}
		}
	}
}
