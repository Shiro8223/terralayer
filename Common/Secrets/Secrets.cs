using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terralayer.Common.Depths;
using Terralayer.Common.Net;
using Terralayer.Common.Players;
using Terralayer.Common.World;
using Terralayer.Content.NPCs;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Terralayer.Common.Secrets
{
	/// <summary>
	/// Each character's progress through the secret quests:
	/// Key of Light (Hallowed Mimic) -> Empress of Light's chest -> Rod of Discord;
	/// Pirate Map (Wall of Flesh bridge) -> pirate siege -> the hidden sea -> the Angler's Golden Bug Net ->
	/// five golden creatures -> Golden Fishing Rod and Truffle Worm -> Duke Fishron -> the Tidecaller class.
	/// </summary>
	public class SecretPlayer : ModPlayer
	{
		public bool MetAngler;
		public bool RodOfDiscord;
		public bool AnglerRewarded;
		public bool FishronBaited;
		public bool DukeSlain;

		public const int GoldenCreaturesNeeded = 5;

		public override void SaveData(TagCompound tag)
		{
			tag["metAngler"] = MetAngler;
			tag["rodOfDiscord"] = RodOfDiscord;
			tag["anglerRewarded"] = AnglerRewarded;
			tag["fishronBaited"] = FishronBaited;
			tag["dukeSlain"] = DukeSlain;
		}

		public override void LoadData(TagCompound tag)
		{
			MetAngler = tag.GetBool("metAngler");
			RodOfDiscord = tag.GetBool("rodOfDiscord");
			AnglerRewarded = tag.GetBool("anglerRewarded");
			FishronBaited = tag.GetBool("fishronBaited");
			DukeSlain = tag.GetBool("dukeSlain");
		}

		/// <summary>The rewards are eternal: if one has gone missing (death, a dropped item), it comes back.</summary>
		public void EnsureEternals()
		{
			if (MetAngler)
				Ensure(ItemID.GoldenBugNet);
			if (RodOfDiscord)
				Ensure(ItemID.RodofDiscord);
			if (AnglerRewarded)
				Ensure(ItemID.GoldenFishingRod);
		}

		private void Ensure(int type)
		{
			if (Player.inventory.Any(i => i.type == type) || Main.mouseItem.type == type)
				return;
			Player.QuickSpawnItem(Player.GetSource_Misc("TerralayerSecret"), EternalItem.Make(type));
		}

		public override void OnEnterWorld()
		{
			if (TerralayerWorld.Active)
				EnsureEternals();
		}

		public override void PostUpdate()
		{
			if (!TerralayerWorld.Active || Player.whoAmI != Main.myPlayer || Player.dead)
				return;
			if (Main.GameUpdateCount % 120 == 0)
				EnsureEternals();

			// The Empress's chest: put the Key of Light in it and it turns into the Rod of Discord.
			int chest = Player.chest;
			if (chest >= 0 && chest == SecretEvents.Instance.LightChest && Main.chest[chest] is Chest c) {
				int key = Array.FindIndex(c.item, i => i.type == ItemID.LightKey);
				if (key >= 0 && !RodOfDiscord) {
					c.item[key] = EternalItem.Make(ItemID.RodofDiscord);
					RodOfDiscord = true;
					SoundEngine.PlaySound(SoundID.Unlock, Player.Center);
					Main.NewText("The Key of Light melts into the chest... and leaves behind the Rod of Discord. It is yours for good.", new Color(255, 180, 255));
				}
			}
		}

		public static int CountGoldenCreatures(Player p) => p.inventory.Where(i => GoldenCreatureItems.Contains(i.type)).Sum(i => i.stack);

		public static bool TakeGoldenCreatures(Player p, int count)
		{
			if (CountGoldenCreatures(p) < count)
				return false;
			for (int i = 0; i < p.inventory.Length && count > 0; i++) {
				Item it = p.inventory[i];
				if (!GoldenCreatureItems.Contains(it.type))
					continue;
				int take = Math.Min(count, it.stack);
				it.stack -= take;
				count -= take;
				if (it.stack <= 0)
					it.TurnToAir();
			}
			return true;
		}

		/// <summary>Golden critters (as NPCs) that may turn up in the Depths for a holder of the Golden Bug Net.</summary>
		public static readonly int[] GoldenCritters = {
			NPCID.GoldBunny, NPCID.GoldBird, NPCID.GoldButterfly, NPCID.GoldFrog, NPCID.GoldGrasshopper,
			NPCID.GoldMouse, NPCID.GoldWorm, NPCID.SquirrelGold, NPCID.GoldLadyBug, NPCID.GoldDragonfly,
		};

		private static HashSet<int> goldenItems;

		/// <summary>The items those critters become when caught.</summary>
		public static HashSet<int> GoldenCreatureItems => goldenItems ??= GoldenCritters
			.Select(t => ContentSamples.NpcsByNetId.TryGetValue(t, out NPC n) ? n.catchItem : 0).Where(t => t > 0).ToHashSet();
	}

	/// <summary>World-side state for the secrets: the Empress's chest, the pirate siege, the hidden sea and the Duke.</summary>
	public class SecretEvents : ModSystem
	{
		public static SecretEvents Instance => ModContent.GetInstance<SecretEvents>();

		/// <summary>The chest index of the Empress of Light's hallowed chest on this floor (-1 if none).</summary>
		public int LightChest = -1;

		public bool SiegeActive;
		public int SiegeKills;
		private int siegeSpawned;
		private int siegeTimer;
		private readonly List<int> pirates = new();

		public const int SiegeGoal = 50;

		private static readonly int[] PirateTypes = {
			NPCID.PirateDeckhand, NPCID.PirateDeckhand, NPCID.PirateDeckhand, NPCID.PirateCorsair, NPCID.PirateCorsair,
			NPCID.PirateDeadeye, NPCID.PirateDeadeye, NPCID.PirateCrossbower, NPCID.PirateCrossbower, NPCID.Parrot,
		};

		public override void ClearWorld()
		{
			LightChest = -1;
			SiegeActive = false;
			SiegeKills = 0;
			pirates.Clear();
		}

		// ------------------------------------------------------------------ the pirate siege

		/// <summary>Using the Pirate Map: only on the Wall of Flesh's bridge, once the Wall is dead.</summary>
		public bool TryStartSiege(Player p, out string why)
		{
			var depths = DepthsSystem.Instance;
			RoomPlan room = depths.Plan != null ? depths.RoomOf(p) : null;
			why = "";
			if (room == null || room.Id != depths.Plan.BossRoom || room.Arch != Archetype.HellBridge) {
				why = "The X on the map marks the far end of a bridge over a lake of fire...";
				return false;
			}
			if (depths.Guardian != GuardianState.Defeated) {
				why = "Not while the Wall still stands.";
				return false;
			}
			if (SiegeActive) {
				why = "The pirates are already here!";
				return false;
			}
			SiegeActive = true;
			SiegeKills = 0;
			siegeSpawned = 0;
			siegeTimer = 0;
			pirates.Clear();
			Chat.Announce("A ghostly ship's bell rings over the lava... PIRATES are boarding the bridge! Defeat 50 of them.", new Color(255, 200, 80));
			return true;
		}

		public void OnPirateKilled()
		{
			if (!SiegeActive)
				return;
			SiegeKills++;
			if (SiegeKills % 10 == 0 && SiegeKills < SiegeGoal)
				Chat.Announce($"Pirates defeated: {SiegeKills}/{SiegeGoal}", new Color(255, 200, 80));
		}

		public override void PostUpdateWorld()
		{
			if (!TerralayerWorld.Active || !SiegeActive)
				return;
			var depths = DepthsSystem.Instance;
			RoomPlan arena = depths.Plan?.Rooms[depths.Plan.BossRoom];
			var inside = arena == null ? new List<Player>() : Main.player.Where(pl => pl.active && !pl.dead && arena.Bounds.Contains(pl.Center.ToTileCoordinates())).ToList();
			if (inside.Count == 0) {
				EndSiege(false);
				return;
			}
			pirates.RemoveAll(i => !Main.npc[i].active || !Main.npc[i].GetGlobalNPC<DepthsNPC>().Pirate);
			if (SiegeKills >= SiegeGoal) {
				EndSiege(true, inside);
				return;
			}
			if (++siegeTimer % 40 == 0 && pirates.Count < 8 && siegeSpawned < SiegeGoal + 25)
				SpawnPirate(arena, inside[Main.rand.Next(inside.Count)]);
		}

		private void SpawnPirate(RoomPlan arena, Player near)
		{
			int bridge = Depths.Rooms.HellBridgeRoom.BridgeY(arena);
			int dir = Main.rand.NextBool() ? 1 : -1;
			int x = (int)(near.Center.X / 16) + dir * Main.rand.Next(28, 48);
			x = Math.Clamp(x, arena.Interior.X + 4, arena.Interior.Right - 4);
			int type = PirateTypes[Main.rand.Next(PirateTypes.Length)];
			int index = DepthsSystem.SpawnGate(() => NPC.NewNPC(Entity.GetSource_NaturalSpawn(), x * 16 + 8, (bridge - 2) * 16, type));
			if (index >= Main.maxNPCs)
				return;
			NPC npc = Main.npc[index];
			var g = npc.GetGlobalNPC<DepthsNPC>();
			g.FromDepths = true;
			g.Pirate = true;
			g.Floor = DepthsSystem.Instance.Floor;
			npc.lifeMax = Math.Max(1, (int)(npc.lifeMax * 0.6f * DepthsSystem.Instance.RunLifeMult));
			npc.life = npc.lifeMax;
			npc.damage = (int)(npc.damage * 0.75f);
			npc.netUpdate = true;
			pirates.Add(index);
			siegeSpawned++;
		}

		private void EndSiege(bool won, List<Player> inside = null)
		{
			SiegeActive = false;
			foreach (int i in pirates)
				if (Main.npc[i].active) {
					Main.npc[i].active = false;
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, i);
				}
			pirates.Clear();
			if (!won) {
				Chat.Announce("The pirates sail back into the smoke...", Color.Gray);
				return;
			}
			Chat.Announce("The last pirate falls. A salt wind blows across the bridge... and carries you away.", new Color(120, 220, 255));
			OpenSea();
			foreach (Player p in inside)
				TerralayerNet.SendTeleport(p, OceanArena.Arrival(p));
		}

		// ------------------------------------------------------------------ the hidden sea

		/// <summary>Makes sure the hidden sea is carved and the Angler is waiting there.</summary>
		public void OpenSea()
		{
			var w = TerralayerWorld.Instance;
			if (!w.SeaBuilt) {
				OceanArena.Build();
				w.SeaBuilt = true;
			}
			if (!NPC.AnyNPCs(ModContent.NPCType<LostAngler>())) {
				Point a = OceanArena.AnglerSpot;
				DepthsSystem.SpawnGate(() => NPC.NewNPC(Entity.GetSource_NaturalSpawn(), a.X * 16 + 8, a.Y * 16, ModContent.NPCType<LostAngler>()));
			}
		}

		/// <summary>Is anyone at the hidden sea? (Duke Fishron may only appear while someone is.)</summary>
		public static bool SomeoneAtSea() => Main.player.Any(p => p.active && !p.dead && OceanArena.Contains(p.Center));

		public void SummonDuke(Player p)
		{
			if (NPC.AnyNPCs(NPCID.DukeFishron))
				return;
			Vector2 at = OceanArena.DukeSpawn;
			DepthsSystem.SpawnGate(() => NPC.NewNPC(NPC.GetBossSpawnSource(p.whoAmI), (int)at.X, (int)at.Y, NPCID.DukeFishron, Target: p.whoAmI));
			Chat.AnnounceKey("Announcement.HasAwoken", new Color(175, 75, 255), Lang.GetNPCNameValue(NPCID.DukeFishron));
		}

		public void OnDukeSlain()
		{
			var w = TerralayerWorld.Instance;
			foreach (Player p in Main.player.Where(pl => pl.active && OceanArena.Contains(pl.Center)))
				p.GetModPlayer<SecretPlayer>().DukeSlain = true;
			if (!w.DukeRelic) {
				w.DukeRelic = true;
				Homestead.PlaceDukeRelic(w);
				TerralayerWorld.Sync();
			}
			Chat.Announce("Duke Fishron sinks beneath the waves! His relic rises beside the Shrine of Guardians, and a new class answers the tide: the Tidecaller (Skill Tree, Classes).", new Color(90, 220, 255));
		}
	}

	/// <summary>The Pirate Map can't start a vanilla invasion here: on the hellbridge it calls the pirate siege instead.</summary>
	public class SecretItems : GlobalItem
	{
		private static uint lastMessage;

		public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.PirateMap;

		public override bool CanUseItem(Item item, Player player)
		{
			if (!TerralayerWorld.Active)
				return true;
			if (player.whoAmI == Main.myPlayer && Main.netMode != NetmodeID.MultiplayerClient) {
				if (SecretEvents.Instance.TryStartSiege(player, out string why)) {
					if (--item.stack <= 0)
						item.TurnToAir();
				}
				else if (Main.GameUpdateCount - lastMessage > 60) {
					lastMessage = Main.GameUpdateCount;
					Main.NewText(why, new Color(230, 200, 150));
				}
			}
			return false;
		}
	}
}
