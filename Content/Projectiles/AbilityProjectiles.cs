using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terralayer.Common.Heirs;
using Terralayer.Common.Players;
using Terralayer.UI;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Content.Projectiles
{
	/// <summary>Arcane Orbit: one of three orbs circling the caster while the ability is on. Burns what it touches and fires bolts.</summary>
	public class ArcaneOrb : ModProjectile
	{
		// Drawn in code (a glow), so the texture is only a placeholder.
		public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.WoodenArrowFriendly}";

		public override void SetDefaults()
		{
			Projectile.width = 22;
			Projectile.height = 22;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Magic;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 30;
			Projectile.timeLeft = 2;
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			var lp = owner.GetModPlayer<LegacyPlayer>();
			// Only the caster's client knows whether the ability is still on; everyone else just follows along.
			bool mine = Projectile.owner == Main.myPlayer;
			if (!owner.active || owner.dead || mine && (!lp.AbilityOn || lp.AbilityClass != HeirClass.Arcanist)) {
				Projectile.Kill();
				return;
			}
			Projectile.timeLeft = 2;
			float angle = Main.GlobalTimeWrappedHourly * 3f + Projectile.ai[0] * MathHelper.TwoPi / 3f;
			Projectile.Center = owner.Center + angle.ToRotationVector2() * 64f;
			Projectile.velocity = Vector2.Zero;
			Lighting.AddLight(Projectile.Center, 0.6f, 0.3f, 0.9f);

			if (Projectile.owner != Main.myPlayer || ++Projectile.localAI[0] < 45)
				return;
			Projectile.localAI[0] = Projectile.ai[0] * 8; // stagger the three orbs
			NPC target = null;
			float best = 28 * 16;
			foreach (NPC n in Main.ActiveNPCs) {
				if (!n.CanBeChasedBy(Projectile))
					continue;
				float d = Vector2.Distance(n.Center, Projectile.Center);
				if (d < best && Collision.CanHitLine(Projectile.Center, 1, 1, n.Center, 1, 1)) {
					best = d;
					target = n;
				}
			}
			if (target != null)
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.DirectionTo(target.Center) * 11f, ProjectileID.AmethystBolt, Projectile.damage, 1f, Projectile.owner);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D glow = GenTextures.Glow;
			Vector2 at = Projectile.Center - Main.screenPosition;
			float pulse = 1f + 0.12f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f + Projectile.ai[0]);
			// Alpha 0 under premultiplied blending draws additively.
			Main.EntitySpriteDraw(glow, at, null, new Color(160, 80, 255, 0) * 0.9f, 0f, glow.Size() / 2f, 0.85f * pulse, SpriteEffects.None);
			Main.EntitySpriteDraw(glow, at, null, new Color(255, 240, 255, 0), 0f, glow.Size() / 2f, 0.35f * pulse, SpriteEffects.None);
			return false;
		}
	}

	/// <summary>Spirit Pack: a spectral wolf that hunts the nearest foe for ten seconds, passing through walls.</summary>
	public class SpiritWolf : ModProjectile
	{
		public override string Texture => $"Terraria/Images/NPC_{NPCID.Wolf}";

		public override void SetDefaults()
		{
			Projectile.width = 40;
			Projectile.height = 26;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Summon;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
			Projectile.timeLeft = 600;
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (!owner.active || owner.dead) {
				Projectile.Kill();
				return;
			}

			NPC target = null;
			float best = 60 * 16;
			foreach (NPC n in Main.ActiveNPCs) {
				if (!n.CanBeChasedBy(Projectile))
					continue;
				float d = Vector2.Distance(n.Center, owner.Center);
				if (d < best) {
					best = d;
					target = n;
				}
			}

			Vector2 goal;
			float speed;
			if (target != null) {
				goal = target.Center;
				speed = 15f;
			}
			else {
				goal = owner.Center + new Vector2((Projectile.ai[0] - 1) * 40f - owner.direction * 40f, -30f);
				speed = Vector2.Distance(goal, Projectile.Center) > 200 ? 14f : 6f;
			}
			Vector2 want = Projectile.DirectionTo(goal) * speed;
			if (Vector2.Distance(goal, Projectile.Center) < 12f && target == null)
				want = Vector2.Zero;
			Projectile.velocity = (Projectile.velocity * 14f + want) / 15f;
			if (Math.Abs(Projectile.velocity.X) > 0.3f)
				Projectile.spriteDirection = Projectile.velocity.X > 0 ? 1 : -1;

			if (++Projectile.frameCounter >= 5) {
				Projectile.frameCounter = 0;
				Projectile.frame++;
			}
			Lighting.AddLight(Projectile.Center, 0.3f, 0.6f, 1f);
			if (Main.rand.NextBool(4)) {
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.IceTorch);
				d.velocity *= 0.3f;
				d.noGravity = true;
			}
		}

		public override void OnKill(int timeLeft)
		{
			for (int i = 0; i < 14; i++) {
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.IceTorch);
				d.velocity *= 1.5f;
				d.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Main.instance.LoadNPC(NPCID.Wolf);
			Texture2D tex = TextureAssets.Npc[NPCID.Wolf].Value;
			int frames = Math.Max(1, Main.npcFrameCount[NPCID.Wolf]);
			int fh = tex.Height / frames;
			// Skip the first (standing) frame so the pack always looks like it's running.
			int frame = frames > 2 ? 1 + Projectile.frame % (frames - 1) : 0;
			var src = new Rectangle(0, frame * fh, tex.Width, fh);
			SpriteEffects fx = Projectile.spriteDirection > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			Vector2 at = Projectile.Center - Main.screenPosition;
			float fade = Math.Min(1f, Projectile.timeLeft / 30f);

			Main.EntitySpriteDraw(tex, at - Projectile.velocity * 1.5f, src, new Color(80, 160, 255, 0) * 0.35f * fade, 0f, src.Size() / 2f, 1f, fx);
			Main.EntitySpriteDraw(tex, at, src, new Color(140, 210, 255, 0) * 0.9f * fade, 0f, src.Size() / 2f, 1f, fx);
			return false;
		}
	}
}
