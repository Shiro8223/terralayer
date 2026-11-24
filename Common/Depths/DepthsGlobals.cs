using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terralayer.Common.Config;
using Terralayer.Common.Heirs;
using Terralayer.Common.Items;
using Terralayer.Common.Players;
using Terralayer.Common.Utilities;
using Terralayer.Common.World;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace Terralayer.Common.Depths
{
	/// <summary>What makes a champion dangerous, shown as the colour of its aura.</summary>
	public enum ChampionAffix : byte
	{
		Brutal,       // hits much harder (red)
		Armored,      // heavy defense and takes 20% less damage (steel blue)
		Vampiric,     // heals when it hits you (magenta)
		Regenerating, // steadily regains life (green)
		Count
	}

	/// <summary>
	/// Tags Depths enemies (room, champion, guardian), pays out gold, and enforces the world rules for NPCs:
	/// no natural spawns, no vanilla townsfolk, no bosses that Terralayer didn't summon.
	/// </summary>
	public class DepthsNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		public bool FromDepths;
		public bool Guardian;
		public bool Champion;
		public bool Mimic;
		public bool Trial;
		public ChampionAffix Affix;
		/// <summary>Secret quests: a pirate of the siege; the rare Hallowed Mimic that carries the Key of Light.</summary>
		public bool Pirate;
		public bool HallowedMimic;
		/// <summary>What the enemy was worth when the Depths spawned it; bonus gold is based on this alone.</summary>
		public float SpawnValue;
		public int Room = -1;
		public int Floor;
		private bool grown;

		public const float ChampionScale = 1.3f;

		public static Color AffixColor(ChampionAffix a) => a switch {
			ChampionAffix.Brutal => new Color(255, 70, 40),
			ChampionAffix.Armored => new Color(120, 175, 255),
			ChampionAffix.Vampiric => new Color(215, 40, 150),
			_ => new Color(80, 255, 120)
		};

		/// <summary>Makes an NPC bigger, hitbox and all, keeping its feet where they were.</summary>
		public static void Grow(NPC npc, float factor)
		{
			var g = npc.GetGlobalNPC<DepthsNPC>();
			if (g.grown)
				return;
			g.grown = true;
			Vector2 bottom = npc.Bottom;
			npc.scale *= factor;
			npc.width = (int)(npc.width * factor);
			npc.height = (int)(npc.height * factor);
			npc.Bottom = bottom;
		}

		/// <summary>Room enemies never despawn: you can fall back and come back to finish the fight.</summary>
		public override bool CheckActive(NPC npc) => !(FromDepths && !Guardian && TerralayerWorld.Active);

		public override void OnSpawn(NPC npc, IEntitySource source)
		{
			if (!TerralayerWorld.Active || DepthsSystem.Spawning)
				return;

			bool vanillaTownsfolk = npc.townNPC && npc.ModNPC == null;
			// Duke Fishron is welcome at the hidden sea (baited there); any other stray boss is not.
			bool strayBoss = npc.boss && source is not EntitySource_Parent
				&& !(npc.type == NPCID.DukeFishron && Secrets.SecretEvents.SomeoneAtSea());
			if (vanillaTownsfolk || strayBoss) {
				npc.active = false;
				if (Main.netMode == NetmodeID.Server)
					NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
			}
		}

		public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
		{
			if (TerralayerWorld.Active)
				maxSpawns = 0;
		}

		public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
		{
			if (TerralayerWorld.Active)
				pool.Clear();
		}

		public override void ModifyTypeName(NPC npc, ref string typeName)
		{
			if (Champion)
				typeName = $"{Affix} Champion {typeName}";
		}

		/// <summary>Wisps of the affix colour rising off a champion.</summary>
		public override void DrawEffects(NPC npc, ref Color drawColor)
		{
			if (!Champion || !Main.rand.NextBool(2))
				return;
			Vector2 at = npc.Center + Main.rand.NextVector2CircularEdge(npc.width * 0.7f, npc.height * 0.6f);
			Dust d = Dust.NewDustPerfect(at, DustID.RainbowMk2, new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.6f)), 0, AffixColor(Affix), 1.1f);
			d.noGravity = true;
			d.fadeIn = 0.5f;
		}

		/// <summary>The aura: a pulsing glow in the affix colour behind the champion.</summary>
		public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (!Champion)
				return true;
			Texture2D glow = UI.GenTextures.Glow;
			Color c = AffixColor(Affix);
			float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f + npc.whoAmI);
			float size = Math.Max(npc.width, npc.height) * 2.6f / glow.Width;
			Vector2 at = npc.Center - screenPos + new Vector2(0f, npc.gfxOffY);
			spriteBatch.Draw(glow, at, null, new Color(c.R, c.G, c.B, 0) * (0.55f * pulse), 0f, glow.Size() / 2f, size * pulse, SpriteEffects.None, 0f);
			spriteBatch.Draw(glow, at, null, new Color(c.R, c.G, c.B, 0) * 0.35f, 0f, glow.Size() / 2f, size * 0.6f, SpriteEffects.None, 0f);
			return true;
		}

		public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
		{
			if (Champion && Affix == ChampionAffix.Armored)
				modifiers.FinalDamage *= 0.8f;
		}

		public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
		{
			if (!Champion || Affix != ChampionAffix.Vampiric || Main.netMode == NetmodeID.MultiplayerClient)
				return;
			int heal = Math.Max(1, npc.lifeMax / 12);
			npc.life = Math.Min(npc.lifeMax, npc.life + heal);
			npc.HealEffect(heal);
			npc.netUpdate = true;
		}

		public override void PostAI(NPC npc)
		{
			if (!Champion || Affix != ChampionAffix.Regenerating || Main.netMode == NetmodeID.MultiplayerClient)
				return;
			if (npc.life < npc.lifeMax && (Main.GameUpdateCount + npc.whoAmI) % 60 == 0) {
				int heal = Math.Max(1, npc.lifeMax / 240);
				npc.life = Math.Min(npc.lifeMax, npc.life + heal);
				npc.HealEffect(heal);
			}
		}

		/// <summary>Clients need to know a champion is one, to draw its aura and size.</summary>
		public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
		{
			bitWriter.WriteBit(Champion);
			if (Champion)
				binaryWriter.Write((byte)Affix);
		}

		public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
		{
			bool champion = bitReader.ReadBit();
			if (!champion)
				return;
			Champion = true;
			Affix = (ChampionAffix)binaryReader.ReadByte();
			Grow(npc, ChampionScale);
		}

		public override void OnKill(NPC npc)
		{
			if (!TerralayerWorld.Active)
				return;

			var depths = DepthsSystem.Instance;
			if (Guardian)
				depths.OnGuardianKilled();
			if (npc.type == NPCID.MoonLordCore)
				TerralayerWorld.Instance.OnMoonLordSlain();
			if (!Guardian)
				depths.OnEnemyKilled(FromDepths ? Room : -1, npc.whoAmI);
			if (Pirate)
				Secrets.SecretEvents.Instance.OnPirateKilled();
			if (npc.type == NPCID.DukeFishron && Secrets.OceanArena.Contains(npc.Center))
				Secrets.SecretEvents.Instance.OnDukeSlain();
			if (HallowedMimic) {
				Item.NewItem(new EntitySource_Misc("TerralayerSecret"), npc.Hitbox, ItemID.LightKey);
				Net.Chat.Announce("The Hallowed Mimic drops a Key of Light. Something, somewhere, is waiting to be unlocked...", new Color(255, 180, 255));
			}

			if (npc.friendly || npc.townNPC || npc.lifeMax <= 5 || npc.value <= 0)
				return;

			// Gold find (heir traits, Fortune, the run streak) adds coins on top of the normal drop: based on what the
			// enemy was worth when it spawned (never on coins it picked up) and capped at double.
			npc.extraValue = 0;
			float goldFind = 1f;
			HeirClass cls = HeirClass.Knight;
			if (npc.lastInteraction is >= 0 and < Main.maxPlayers && Main.player[npc.lastInteraction].active) {
				var lp = Main.player[npc.lastInteraction].GetModPlayer<LegacyPlayer>();
				goldFind = lp.GoldFind;
				cls = lp.Class;
			}
			float mult = Math.Clamp(goldFind * depths.StreakMultiplier * TerralayerConfig.Instance.GoldMultiplier - 1f, 0f, 2f);
			long extra = FromDepths ? (long)(SpawnValue * mult) : 0;
			if (extra > 0)
				Coins.Drop(npc.GetSource_Loot(), npc.Hitbox, extra);

			var rng = new Random(Main.rand.Next());
			var source = new EntitySource_Misc("Terralayer");
			if (Champion)
				Item.NewItem(source, npc.Hitbox, Trial ? Loot.RollWeapon(Floor, cls, 2, rng) : Loot.RollMain(Floor, cls, 1, rng, 0.5, 0.25));
			if (Mimic)
				DungeonBuilder.DropLoot(npc.Hitbox, ChestKind.Gold, Floor, cls, goldFind * depths.StreakMultiplier);
		}
	}

	/// <summary>Per-projectile balance for enemy attacks that hit too hard for where they appear.</summary>
	public class HostileProjectileTuning : GlobalProjectile
	{
		/// <summary>No fallen stars: they'd litter the homestead with free mana between descents.</summary>
		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (TerralayerWorld.Active && projectile.type == ProjectileID.FallingStar)
				projectile.active = false;
		}

		public override void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers)
		{
			if (!TerralayerWorld.Active)
				return;
			if (projectile.type == ProjectileID.JavelinHostile)
				modifiers.SourceDamage *= 0.3f; // Hoplite javelins: -70%
		}
	}

	/// <summary>Monsters drop only hearts, mana stars and coins. Real loot comes from Terralayer's chests and champions.</summary>
	public class LootFilter : GlobalItem
	{
		private static readonly HashSet<int> Allowed = new() {
			ItemID.Heart, ItemID.CandyApple, ItemID.CandyCane, ItemID.Star, ItemID.SoulCake, ItemID.SugarPlum,
			ItemID.CopperCoin, ItemID.SilverCoin, ItemID.GoldCoin, ItemID.PlatinumCoin,
		};

		public override void OnSpawn(Item item, IEntitySource source)
		{
			if (!TerralayerWorld.Active)
				return;
			if (Homestead.IsRelicItem(item.type)) {
				item.TurnToAir();
				item.active = false;
				return;
			}
			if (source is EntitySource_Loot loot && loot.Entity is NPC && !Allowed.Contains(item.type)) {
				item.TurnToAir();
				item.active = false;
			}
		}
	}
}
