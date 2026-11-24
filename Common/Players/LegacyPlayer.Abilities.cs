using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terralayer.Common.Depths;
using Terralayer.Common.Heirs;
using Terralayer.Common.Systems;
using Terralayer.Common.World;
using Terralayer.Content.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Common.Players
{
	/// <summary>
	/// The eight class abilities. Each is meant to change how a fight plays, not just add a number:
	/// Aegis Charge (dash + guard), Arrowstorm (zone denial), Arcane Orbit (orbiting turrets), Shade Step (blink +
	/// ambush crit), Earthshatter (slam + shockwaves + rage), Spirit Pack (temporary wolves), Rift Jump (travel to any
	/// explored room) and Rewind (undo the last three seconds).
	/// </summary>
	public partial class LegacyPlayer
	{
		// Guardian
		private int chargeTicks;
		private int chargeDir;
		private readonly HashSet<int> chargeHit = new();
		public int BulwarkTicks;

		// Marksman
		private int stormTicks;
		private Vector2 stormTarget;
		private float stormTop;

		// Shadow
		public int AmbushTicks;

		// Warrior
		private int slamTicks;
		public int RageTicks;
		private int rageHealCooldown;
		private readonly List<(uint at, Vector2 pos, int damage)> quakes = new();

		// Beastmaster
		public int PackTicks;

		// Riftwalker
		/// <summary>The floor map is open waiting for a Rift Jump destination.</summary>
		public bool RiftSelecting;
		private int surgeTicks;

		// Chronomancer: a ring of snapshots, one every 6 ticks, covering the last 3 seconds.
		private const int TrailLength = 30;
		private const int TrailInterval = 6;
		private readonly Vector2[] trailPos = new Vector2[TrailLength];
		private readonly int[] trailLife = new int[TrailLength];
		private readonly int[] trailMana = new int[TrailLength];
		private int trailHead;
		private int trailCount;

		private IEntitySource AbilitySource => Player.GetSource_Misc("TerralayerAbility");

		private int AbilityDamage(DamageClass cls)
		{
			int floor = Math.Max(1, InRun ? RunFloor : 1);
			return (int)Player.GetDamage(cls).ApplyTo(12 + 12 * (floor - 1));
		}

		public void ResetAbilityState()
		{
			chargeTicks = stormTicks = slamTicks = 0;
			BulwarkTicks = AmbushTicks = RageTicks = PackTicks = surgeTicks = 0;
			RiftSelecting = false;
			quakes.Clear();
			trailCount = 0;
			Player.immuneNoBlink = false;
		}

		// ------------------------------------------------------------------ activation

		private void ToggleAbility()
		{
			if (Player.dead || PendingHeirChoice || Heir == null || Player.CCed || abilityTapCooldown > 0)
				return;
			abilityTapCooldown = 12;
			if (RiftSelecting) {
				RiftSelecting = false; // pressing it again closes the map
				return;
			}
			ClassInfo info = AbilityInfo;

			if (info.Kind == AbilityKind.Sustained) {
				if (AbilityOn) {
					AbilityOn = false;
					return;
				}
				if (Charge < ClassDefs.SustainMinimum) {
					CombatText.NewText(Player.Hitbox, Color.Gray, "Not enough charge");
					return;
				}
				AbilityOn = true;
			}
			else {
				if (Charge < info.Cost) {
					CombatText.NewText(Player.Hitbox, Color.Gray, "Not enough charge");
					return;
				}
				bool used = AbilityClass switch {
					HeirClass.Knight => AegisCharge(),
					HeirClass.Ranger => Arrowstorm(),
					HeirClass.Shadowblade => ShadeStep(),
					HeirClass.Berserker => Earthshatter(),
					HeirClass.Beastcaller => SpiritPack(),
					HeirClass.Riftwalker => RiftJump(),
					HeirClass.Chronomancer => Rewind(),
					HeirClass.Fishron => Typhoon(),
					_ => false
				};
				if (!used)
					return;
				Charge -= info.Cost;
			}
			Announce(info);
		}

		private void Announce(ClassInfo info)
		{
			CombatText.NewText(Player.Hitbox, info.Color, info.AbilityName + "!");
			SoundEngine.PlaySound(SoundID.Item4, Player.Center);
			for (int i = 0; i < 16; i++) {
				Dust d = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.MagicMirror);
				d.velocity *= 1.5f;
				d.noGravity = true;
			}
		}

		// ------------------------------------------------------------------ Guardian: Aegis Charge

		private bool AegisCharge()
		{
			chargeDir = Main.MouseWorld.X >= Player.Center.X ? 1 : -1;
			chargeTicks = 20;
			chargeHit.Clear();
			Player.direction = chargeDir;
			SoundEngine.PlaySound(SoundID.Item74, Player.Center);
			return true;
		}

		private void TickCharge()
		{
			if (chargeTicks <= 0)
				return;
			chargeTicks--;
			Player.immune = true;
			Player.immuneTime = Math.Max(Player.immuneTime, 6);
			Player.immuneNoBlink = true;

			Rectangle hit = Player.Hitbox;
			hit.Inflate(14, 8);
			int damage = (int)(AbilityDamage(DamageClass.Melee) * 2.5f);
			foreach (NPC n in Main.ActiveNPCs) {
				if (n.friendly || n.dontTakeDamage || n.lifeMax <= 5 || chargeHit.Contains(n.whoAmI) || !hit.Intersects(n.Hitbox))
					continue;
				chargeHit.Add(n.whoAmI);
				Player.ApplyDamageToNPC(n, damage, 10f, chargeDir, crit: false, DamageClass.Melee);
				SoundEngine.PlaySound(SoundID.NPCHit4, n.Center);
			}
			for (int i = 0; i < 3; i++) {
				Dust d = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.BlueTorch);
				d.velocity = new Vector2(-chargeDir * Main.rand.NextFloat(2f, 5f), Main.rand.NextFloat(-1f, 1f));
				d.noGravity = true;
				d.scale = 1.6f;
			}
			if (chargeTicks == 0) {
				Player.immuneNoBlink = false;
				BulwarkTicks = 180;
				CombatText.NewText(Player.Hitbox, new Color(150, 180, 255), "Bulwark!");
			}
		}

		// ------------------------------------------------------------------ Marksman: Arrowstorm

		private bool Arrowstorm()
		{
			Vector2 target = Main.MouseWorld;
			if (Vector2.Distance(target, Player.Center) > 60 * 16)
				target = Player.Center + Player.DirectionTo(target) * 60 * 16;
			stormTarget = target;
			stormTicks = 120;

			// Arrows fall from just under whatever ceiling is above the target.
			Point t = target.ToTileCoordinates();
			int up = 1;
			while (up < 40 && !WorldGen.SolidTile(t.X, t.Y - up))
				up++;
			stormTop = target.Y - (up - 1) * 16 + 8;

			int away = Main.MouseWorld.X >= Player.Center.X ? -1 : 1;
			Player.velocity = new Vector2(away * 7.5f, -7f);
			Player.immune = true;
			Player.immuneTime = Math.Max(Player.immuneTime, 20);
			return true;
		}

		private void TickStorm()
		{
			if (stormTicks <= 0)
				return;
			stormTicks--;
			if (stormTicks % 3 != 0)
				return;
			var from = new Vector2(stormTarget.X + Main.rand.Next(-150, 151), stormTop);
			if (Collision.SolidCollision(from - new Vector2(4), 8, 8))
				return;
			var to = new Vector2(stormTarget.X + Main.rand.Next(-80, 81), stormTarget.Y);
			Vector2 vel = Vector2.Normalize(to - from) * 18f;
			Projectile.NewProjectile(AbilitySource, from, vel, ProjectileID.FireArrow, (int)(AbilityDamage(DamageClass.Ranged) * 0.75f), 2f, Player.whoAmI);
		}

		// ------------------------------------------------------------------ Shadow: Shade Step

		private bool ShadeStep()
		{
			if (!Blink(20 * 16, 45))
				return false;
			AmbushTicks = 120;
			return true;
		}

		/// <summary>Teleports toward the cursor, stopping at walls. False if there was no room to move.</summary>
		private bool Blink(float range, int immunity)
		{
			Vector2 dir = Player.DirectionTo(Main.MouseWorld);
			float maxDist = Math.Min(range, Vector2.Distance(Player.Center, Main.MouseWorld));
			Vector2 target = Player.position;
			for (float d = 8; d <= maxDist; d += 8) {
				Vector2 next = Player.position + dir * d;
				if (Collision.SolidCollision(next, Player.width, Player.height))
					break;
				target = next;
			}
			if (Vector2.Distance(target, Player.position) < 16)
				return false;
			Teleport(target, TeleportationStyleID.RodOfDiscord);
			Player.immune = true;
			Player.immuneTime = Math.Max(Player.immuneTime, immunity);
			return true;
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			if (AmbushTicks <= 0 || target.friendly)
				return;
			AmbushTicks = 0;
			modifiers.SetCrit();
			modifiers.FinalDamage *= 1.5f;
			for (int i = 0; i < 20; i++) {
				Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.Shadowflame);
				d.velocity *= 2f;
				d.noGravity = true;
			}
			CombatText.NewText(target.Hitbox, new Color(200, 160, 255), "Ambush!", true);
		}

		// ------------------------------------------------------------------ Warrior: Earthshatter

		private bool Earthshatter()
		{
			if (Player.velocity.Y == 0f)
				Shatter();
			else
				slamTicks = 60; // plunge first; the shockwave hits on landing
			return true;
		}

		private void Shatter()
		{
			int damage = (int)(AbilityDamage(DamageClass.Melee) * 1.6f);
			Main.instance.CameraModifiers.Add(new PunchCameraModifier(Player.Center, new Vector2(0f, 1f), 12f, 6f, 24, 1200f, "TerralayerEarthshatter"));
			SoundEngine.PlaySound(SoundID.Item14, Player.Center);
			for (int i = 0; i < 30; i++) {
				Dust d = Dust.NewDustDirect(Player.BottomLeft - new Vector2(16, 8), Player.width + 32, 10, DustID.Smoke);
				d.velocity = new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-3f, 0f));
			}

			// Shockwaves roll outward along the ground both ways, stopping at walls and drops.
			uint now = Main.GameUpdateCount;
			int footY = (int)(Player.Bottom.Y / 16);
			foreach (int dir in new[] { -1, 1 }) {
				int groundY = footY;
				for (int k = 1; k <= 7; k++) {
					int x = (int)(Player.Center.X / 16) + dir * k * 3;
					if (WorldGen.SolidTile(x, groundY - 1) && WorldGen.SolidTile(x, groundY - 2))
						break; // a wall
					int found = -1;
					for (int dy = -2; dy <= 4; dy++)
						if (WorldGen.SolidOrSlopedTile(x, groundY + dy) && !WorldGen.SolidTile(x, groundY + dy - 1)) {
							found = groundY + dy;
							break;
						}
					if (found < 0)
						break; // a drop
					groundY = found;
					quakes.Add((now + (uint)(k * 4), new Vector2(x * 16 + 8, groundY * 16 - 12), damage));
				}
			}
			quakes.Add((now, Player.Bottom - new Vector2(0, 12), damage));
			RageTicks = 300;
			CombatText.NewText(Player.Hitbox, new Color(255, 110, 90), "Rage!");
		}

		private void TickSlam()
		{
			if (slamTicks > 0) {
				slamTicks--;
				Player.immune = true;
				Player.immuneTime = Math.Max(Player.immuneTime, 6);
				if (Player.velocity.Y == 0f || slamTicks == 0) {
					slamTicks = 0;
					Shatter();
				}
			}
			for (int i = quakes.Count - 1; i >= 0; i--) {
				if (quakes[i].at > Main.GameUpdateCount)
					continue;
				Projectile.NewProjectile(AbilitySource, quakes[i].pos, Vector2.Zero, ProjectileID.SolarWhipSwordExplosion, quakes[i].damage, 7f, Player.whoAmI, 0f, 1.1f);
				quakes.RemoveAt(i);
			}
		}

		// ------------------------------------------------------------------ Beastmaster: Spirit Pack

		private bool SpiritPack()
		{
			int damage = (int)(AbilityDamage(DamageClass.Summon) * 1.1f);
			int type = ModContent.ProjectileType<SpiritWolf>();
			for (int i = 0; i < 3; i++)
				Projectile.NewProjectile(AbilitySource, Player.Center + new Vector2((i - 1) * 24, -8), new Vector2((i - 1) * 3f, -4f), type, damage, 3f, Player.whoAmI, i);
			PackTicks = 600;
			SoundEngine.PlaySound(SoundID.Item44, Player.Center);
			return true;
		}

		// ------------------------------------------------------------------ Riftwalker: Rift Jump

		private bool RiftJump()
		{
			var depths = DepthsSystem.Instance;
			RoomPlan room = depths.Plan != null ? depths.RoomOf(Player) : null;
			if (!InRun || room == null)
				return Blink(12 * 16, 30); // at home there are no rooms to jump between

			if (room.Id == depths.Plan.BossRoom && depths.Guardian is GuardianState.Awakening or GuardianState.Fighting) {
				CombatText.NewText(Player.Hitbox, Color.Gray, "The arena holds you fast");
				return false;
			}
			if (!depths.Plan.Rooms.Any(r => CanRiftTo(r.Id))) {
				CombatText.NewText(Player.Hitbox, Color.Gray, "No other explored rooms yet");
				return false;
			}
			RiftSelecting = true; // opens the full map in pick-a-room mode
			CombatText.NewText(Player.Hitbox, ClassDefs.Get(HeirClass.Riftwalker).Color, "Choose a room on the map");
			return false; // the charge is paid once a room is picked
		}

		/// <summary>Can a Rift Jump land in this room right now? Any room someone has entered, except the one you're in.</summary>
		public bool CanRiftTo(int id)
		{
			var depths = DepthsSystem.Instance;
			if (depths.Plan == null || id < 0 || id >= depths.Rooms.Length)
				return false;
			RoomPlan here = depths.RoomOf(Player);
			if (here == null || here.Id == id || !depths.Rooms[id].Entered)
				return false;
			return !(id == depths.Plan.BossRoom && depths.Guardian is GuardianState.Awakening or GuardianState.Fighting);
		}

		public void CompleteRiftJump(int id)
		{
			RiftSelecting = false;
			ClassInfo info = AbilityInfo;
			if (AbilityClass != HeirClass.Riftwalker || !CanRiftTo(id) || Charge < info.Cost || Player.dead)
				return;
			var depths = DepthsSystem.Instance;
			RiftPlan landing = depths.Plan.Rifts.FirstOrDefault(r => r.Room == id && r.Kind == RiftKind.Link)
				?? depths.Plan.Rifts.FirstOrDefault(r => r.Room == id);
			Point at = landing?.Pos ?? depths.Plan.Rooms[id].Feature;
			Charge -= info.Cost;
			Teleport(FloorPlan.StandPosition(at, Player), TeleportationStyleID.TeleporterTile);
			Player.immune = true;
			Player.immuneTime = Math.Max(Player.immuneTime, 60);
			riftCooldown = 30;
			Announce(info);
			if (Class == HeirClass.Riftwalker)
				RiftSurge();
		}

		/// <summary>Riftwalker passive: stepping out of a rift gives two seconds of extra speed.</summary>
		private void RiftSurge() => surgeTicks = 120;

		private void TickRiftSelect()
		{
			if (!RiftSelecting)
				return;
			if (Player.dead || !InRun || Main.playerInventory || Main.mapFullscreen || AbilityClass != HeirClass.Riftwalker)
				RiftSelecting = false;
		}

		// ------------------------------------------------------------------ Chronomancer: Rewind

		private void RecordTrail()
		{
			if (Player.dead) {
				trailCount = 0;
				return;
			}
			if (Main.GameUpdateCount % TrailInterval != 0)
				return;
			trailPos[trailHead] = Player.position;
			trailLife[trailHead] = Player.statLife;
			trailMana[trailHead] = Player.statMana;
			trailHead = (trailHead + 1) % TrailLength;
			trailCount = Math.Min(trailCount + 1, TrailLength);
		}

		private bool Rewind()
		{
			if (trailCount < 5) {
				CombatText.NewText(Player.Hitbox, Color.Gray, "Nothing to rewind yet");
				return false;
			}
			var depths = DepthsSystem.Instance;
			RoomPlan here = depths.Plan != null ? depths.RoomOf(Player) : null;

			// The oldest snapshot first (about three seconds ago), newer ones if that spot is now somewhere else.
			for (int k = trailCount; k >= 5; k--) {
				int idx = ((trailHead - k) % TrailLength + TrailLength) % TrailLength;
				Vector2 pos = trailPos[idx];
				Point tile = (pos + Player.Size / 2f).ToTileCoordinates();
				if (here != null && depths.Plan.RoomAt(tile)?.Id != here.Id)
					continue;
				if (Collision.SolidCollision(pos, Player.width, Player.height))
					continue;

				for (float t = 0; t <= 1f; t += 0.05f) {
					Dust d = Dust.NewDustPerfect(Vector2.Lerp(Player.Center, pos + Player.Size / 2f, t), DustID.GoldFlame, Vector2.Zero, 100, default, 1.4f);
					d.noGravity = true;
				}
				Teleport(pos, TeleportationStyleID.PotionOfReturn);
				if (trailLife[idx] > Player.statLife) {
					int heal = trailLife[idx] - Player.statLife;
					Player.statLife = Math.Min(Player.statLifeMax2, trailLife[idx]);
					Player.HealEffect(heal);
				}
				if (trailMana[idx] > Player.statMana) {
					int mana = trailMana[idx] - Player.statMana;
					Player.statMana = Math.Min(Player.statManaMax2, trailMana[idx]);
					Player.ManaEffect(mana);
				}
				for (int b = Player.buffType.Length - 1; b >= 0; b--) {
					int type = Player.buffType[b];
					if (type > 0 && Main.debuff[type] && type != BuffID.PotionSickness && type != BuffID.ManaSickness && !BuffID.Sets.NurseCannotRemoveDebuff[type])
						Player.DelBuff(b);
				}
				Player.immune = true;
				Player.immuneTime = Math.Max(Player.immuneTime, 45);
				SoundEngine.PlaySound(SoundID.Item6, Player.Center);
				trailCount = 0;
				return true;
			}
			CombatText.NewText(Player.Hitbox, Color.Gray, "Time won't bend here");
			return false;
		}

		// ------------------------------------------------------------------ Tidecaller: Typhoon

		private bool Typhoon()
		{
			Vector2 aim = Player.DirectionTo(Main.MouseWorld);
			int damage = (int)(AbilityDamage(DamageClass.Magic) * 0.9f);
			for (int i = -3; i < 3; i++)
				Projectile.NewProjectile(AbilitySource, Player.Center, aim.RotatedBy(MathHelper.ToRadians(12 * i + 6)) * 9f, ProjectileID.Typhoon, damage, 4f, Player.whoAmI);
			SoundEngine.PlaySound(SoundID.Item84, Player.Center);
			return true;
		}

		// ------------------------------------------------------------------ per tick

		/// <summary>Local player, every tick (from PostUpdate).</summary>
		private void UpdateAbilities()
		{
			TickCharge();
			TickStorm();
			TickSlam();
			TickRiftSelect();
			RecordTrail();
			if (BulwarkTicks > 0) BulwarkTicks--;
			if (AmbushTicks > 0) AmbushTicks--;
			if (RageTicks > 0) RageTicks--;
			if (PackTicks > 0) PackTicks--;
			if (surgeTicks > 0) surgeTicks--;
			if (rageHealCooldown > 0) rageHealCooldown--;

			if (AbilityOn && AbilityClass == HeirClass.Arcanist && !Player.dead)
				KeepOrbs();
			if (RageTicks > 0 && Main.rand.NextBool(3)) {
				Dust d = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.Blood);
				d.velocity *= 0.5f;
			}
			if (BulwarkTicks > 0 && Main.rand.NextBool(2)) {
				Dust d = Dust.NewDustDirect(Player.position - new Vector2(6), Player.width + 12, Player.height + 12, DustID.BlueCrystalShard);
				d.velocity *= 0.2f;
				d.noGravity = true;
			}
		}

		private void KeepOrbs()
		{
			int type = ModContent.ProjectileType<ArcaneOrb>();
			var have = new bool[3];
			foreach (Projectile p in Main.ActiveProjectiles)
				if (p.owner == Player.whoAmI && p.type == type && p.ai[0] is >= 0 and < 3)
					have[(int)p.ai[0]] = true;
			int damage = (int)(AbilityDamage(DamageClass.Magic) * 0.6f);
			for (int i = 0; i < 3; i++)
				if (!have[i])
					Projectile.NewProjectile(AbilitySource, Player.Center, Vector2.Zero, type, damage, 2f, Player.whoAmI, i);
		}

		public override void PreUpdateMovement()
		{
			if (Player.whoAmI != Main.myPlayer)
				return;
			if (chargeTicks > 0) {
				Player.velocity.X = chargeDir * 15f;
				if (Player.velocity.Y > 0f)
					Player.velocity.Y *= 0.4f;
			}
			if (slamTicks > 0) {
				Player.velocity.X *= 0.85f;
				Player.velocity.Y = 22f;
			}
		}

		/// <summary>Stat effects of the timed abilities (from PostUpdateEquips).</summary>
		private void ApplyAbilityEffects()
		{
			if (BulwarkTicks > 0) {
				Player.endurance += 0.4f;
				Player.noKnockback = true;
			}
			if (RageTicks > 0) {
				Player.GetDamage(DamageClass.Generic) += 0.20f;
				Player.GetAttackSpeed(DamageClass.Melee) += 0.15f;
			}
			if (PackTicks > 0)
				Player.GetDamage(DamageClass.Summon) += 0.25f;
			if (surgeTicks > 0)
				Player.moveSpeed += 0.35f;
		}

		/// <summary>Rage's lifesteal (from OnHitNPC).</summary>
		private void OnAbilityHit(NPC target, int damageDone)
		{
			if (RageTicks > 0 && rageHealCooldown <= 0) {
				rageHealCooldown = 15;
				Player.Heal(Math.Clamp(damageDone / 20, 1, 8));
			}
		}
	}
}
