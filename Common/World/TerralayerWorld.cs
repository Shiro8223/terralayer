using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terralayer.Common.Net;
using Terraria;
using Terraria.Enums;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.WorldBuilding;

namespace Terralayer.Common.World
{
	/// <summary>
	/// Terralayer replaces world generation entirely: a homestead valley around spawn, and solid rock everywhere else
	/// that the Depths are carved out of. Every Terralayer rule only applies in worlds generated this way.
	/// </summary>
	public class TerralayerWorld : ModSystem
	{
		public const int LobbyHalfWidth = 500;

		public static TerralayerWorld Instance => ModContent.GetInstance<TerralayerWorld>();

		/// <summary>Is the current world a Terralayer world? Gate every rule on this.</summary>
		public static bool Active => Instance.IsTerralayerWorld;

		public bool IsTerralayerWorld;
		public int LobbyCenterX;
		public int LobbyGroundY;
		public HashSet<string> Built = new();
		public int MaxFloorUnlocked = 1;
		public bool LayoutPinned;
		public int PinnedSeed;
		public HashSet<int> BossesSlain = new();
		public bool ShrineBuilt;
		public bool CreditSign;
		public bool ShrineAboveSpawn;
		/// <summary>The hidden sea has been carved (secret quest).</summary>
		public bool SeaBuilt;
		/// <summary>Duke Fishron's relic stands by the shrine.</summary>
		public bool DukeRelic;

		public const string CreditText = "shoutout Reshiro YT for the mod";

		// The secret fight. Not saved: the arena is rebuilt whenever the portal is used.
		public enum MoonState : byte { Idle, Countdown, Fighting, Won }
		public MoonState Moon;
		private int moonTimer;

		public bool AllGuardiansSlain
		{
			get {
				for (int i = 0; i < Homestead.Relics.Length; i++)
					if (Homestead.Relics[i].boss != NPCID.MoonLordCore && !BossesSlain.Contains(Homestead.Relics[i].boss))
						return false;
				return true;
			}
		}

		public bool InMoonArena(Point tile) => IsTerralayerWorld && Moon != MoonState.Idle && Homestead.MoonArena(this).Contains(tile);

		public string WorldId => Main.ActiveWorldFileData?.UniqueId.ToString() ?? "";

		/// <summary>The area rooms must stay out of: the valley and a margin around it.</summary>
		public Rectangle LobbyExclusion => new(LobbyCenterX - LobbyHalfWidth - 60, 0, (LobbyHalfWidth + 60) * 2, LobbyGroundY + 140);

		public bool InLobby(Point tile) => IsTerralayerWorld && Math.Abs(tile.X - LobbyCenterX) <= LobbyHalfWidth && tile.Y < LobbyGroundY + 40;

		/// <summary>The Runeforge stands on the plaza, a few tiles left of spawn (bottom-center tile).</summary>
		public Point RuneforgePos => new(LobbyCenterX - 8, LobbyGroundY);

		/// <summary>Hover/right-click area of the Runeforge, in world pixels.</summary>
		public Rectangle RuneforgeArea => IsTerralayerWorld
			? new Rectangle((RuneforgePos.X - 2) * 16, (RuneforgePos.Y - 4) * 16, 5 * 16, 4 * 16)
			: Rectangle.Empty;

		public StructureDef NextStructure => Structures.All.FirstOrDefault(s => !Built.Contains(s.Key));

		public bool HasBuilt(string key) => Built.Contains(key);

		public override void ClearWorld()
		{
			IsTerralayerWorld = false;
			LobbyCenterX = 0;
			LobbyGroundY = 0;
			Built = new HashSet<string>();
			MaxFloorUnlocked = 1;
			LayoutPinned = false;
			PinnedSeed = 0;
			BossesSlain = new HashSet<int>();
			ShrineBuilt = false;
			CreditSign = false;
			SeaBuilt = false;
			DukeRelic = false;
			Moon = MoonState.Idle;
		}

		public override void SaveWorldData(TagCompound tag)
		{
			if (!IsTerralayerWorld)
				return;
			tag["terralayer"] = true;
			tag["lobbyX"] = LobbyCenterX;
			tag["lobbyY"] = LobbyGroundY;
			tag["built"] = Built.ToList();
			tag["maxFloor"] = MaxFloorUnlocked;
			tag["pinned"] = LayoutPinned;
			tag["pinnedSeed"] = PinnedSeed;
			tag["bosses"] = BossesSlain.ToList();
			tag["shrine"] = ShrineBuilt;
			tag["creditSign"] = CreditSign;
			tag["shrineAboveSpawn"] = ShrineAboveSpawn;
			tag["seaBuilt"] = SeaBuilt;
			tag["dukeRelic"] = DukeRelic;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			IsTerralayerWorld = tag.GetBool("terralayer");
			LobbyCenterX = tag.GetInt("lobbyX");
			LobbyGroundY = tag.GetInt("lobbyY");
			Built = new HashSet<string>(tag.GetList<string>("built"));
			MaxFloorUnlocked = Math.Max(1, tag.GetInt("maxFloor"));
			LayoutPinned = tag.GetBool("pinned");
			PinnedSeed = tag.GetInt("pinnedSeed");
			BossesSlain = new HashSet<int>(tag.GetList<int>("bosses"));
			ShrineBuilt = tag.GetBool("shrine");
			CreditSign = tag.GetBool("creditSign");
			ShrineAboveSpawn = tag.GetBool("shrineAboveSpawn");
			SeaBuilt = tag.GetBool("seaBuilt");
			DukeRelic = tag.GetBool("dukeRelic");
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(IsTerralayerWorld);
			writer.Write(LobbyCenterX);
			writer.Write(LobbyGroundY);
			writer.Write((byte)MaxFloorUnlocked);
			writer.Write(LayoutPinned);
			writer.Write((byte)Built.Count);
			foreach (string b in Built)
				writer.Write(b);
			writer.Write((byte)BossesSlain.Count);
			foreach (int b in BossesSlain)
				writer.Write((short)b);
			writer.Write((byte)Moon);
		}

		public override void NetReceive(BinaryReader reader)
		{
			IsTerralayerWorld = reader.ReadBoolean();
			LobbyCenterX = reader.ReadInt32();
			LobbyGroundY = reader.ReadInt32();
			MaxFloorUnlocked = reader.ReadByte();
			LayoutPinned = reader.ReadBoolean();
			int n = reader.ReadByte();
			Built = new HashSet<string>();
			for (int i = 0; i < n; i++)
				Built.Add(reader.ReadString());
			int bosses = reader.ReadByte();
			BossesSlain = new HashSet<int>();
			for (int i = 0; i < bosses; i++)
				BossesSlain.Add(reader.ReadInt16());
			Moon = (MoonState)reader.ReadByte();
		}

		// ------------------------------------------------------------------ hooks into vanilla

		public override void Load()
		{
			// In Terralayer worlds, vanilla townsfolk never move in (no more arrival spam), the Travelling Merchant
			// never visits, and killing the Wall of Flesh doesn't repaint the world with hardmode stripes.
			On_WorldGen.SpawnTownNPC += (orig, x, y) =>
				Active && WorldGen.prioritizedTownNPCType > 0 && WorldGen.prioritizedTownNPCType < NPCID.Count
					? TownNPCSpawnResult.Blocked
					: orig(x, y);
			On_WorldGen.SpawnTravelNPC += orig => {
				if (!Active)
					orig();
			};
			On_WorldGen.StartHardmode += orig => {
				if (!Active)
					orig();
			};
			// No armor set bonuses in Terralayer worlds: each piece is judged on its own stats (and its rune), so a
			// better piece from a chest is always worth wearing. Individual armor stats are untouched.
			// Expert mode lets enemies scoop up coins on the ground and drop them later; with gold-find bonuses on top
			// that snowballed into platinum. Not here.
			On_Item.GetPickedUpByMonsters_Money += (orig, self, i) => {
				if (!Active)
					orig(self, i);
			};
			On_Player.UpdateArmorSets += (orig, self, i) => {
				if (Active) {
					self.setBonus = "";
					return;
				}
				orig(self, i);
			};
		}

		public override void PostWorldLoad()
		{
			if (!IsTerralayerWorld || Main.netMode == NetmodeID.MultiplayerClient)
				return;
			// Bring older Terralayer worlds up to date.
			if (!ShrineBuilt || !ShrineAboveSpawn) {
				if (ShrineBuilt)
					Homestead.RemoveOldShrine(this); // it used to stand 300 tiles east
				Homestead.BuildShrine(this);
				ShrineBuilt = true;
				ShrineAboveSpawn = true;
			}
			Homestead.PlaceDummies(this);
			Homestead.RepairStructures(this);
			if (!CreditSign)
				CreditSign = Homestead.PlaceCreditSign(this);
		}

		// ------------------------------------------------------------------ guardians, relics and the secret

		public void RecordBossKill(int type)
		{
			int index = Array.FindIndex(Homestead.Relics, r => r.boss == type);
			if (index < 0 || !BossesSlain.Add(type))
				return;
			Homestead.PlaceRelic(this, index);
			Chat.Announce($"A relic of {Homestead.Relics[index].name} rises in the Shrine of Guardians.", new Color(255, 215, 120));
			if (type != NPCID.MoonLordCore && AllGuardiansSlain)
				Chat.Announce("The sky above the Shrine of Guardians tears open...", new Color(110, 230, 255));
			Sync();
		}

		/// <summary>Teleports a player to the Moon Lord's arena, building it and starting the countdown if needed.</summary>
		public Vector2 StartMoonFight(Player player)
		{
			if (Moon == MoonState.Idle || Moon == MoonState.Won) {
				Homestead.BuildMoonArena(this);
				Moon = MoonState.Countdown;
				moonTimer = 6 * 60;
				Chat.Announce("You are being watched...", new Color(50, 255, 130));
				Sync();
			}
			return Homestead.MoonArrival(this, player);
		}

		public void OnMoonLordSlain()
		{
			Moon = MoonState.Won;
			RecordBossKill(NPCID.MoonLordCore);
			Chat.Announce("The Moon Lord is no more. Your house is legend. Step through the portal to return home.", new Color(110, 230, 255));
			Sync();
		}

		private void UpdateMoonFight()
		{
			if (Moon == MoonState.Idle || Moon == MoonState.Won)
				return;
			Rectangle arena = Homestead.MoonArena(this);
			bool anyone = false;
			foreach (Player p in Main.ActivePlayers)
				if (!p.dead && arena.Contains(p.Center.ToTileCoordinates()))
					anyone = true;

			if (!anyone) {
				// Everyone died or left: the Moon Lord withdraws.
				foreach (NPC n in Main.ActiveNPCs)
					if (n.type is NPCID.MoonLordCore or NPCID.MoonLordHead or NPCID.MoonLordHand or NPCID.MoonLordFreeEye)
						n.active = false;
				Moon = MoonState.Idle;
				Sync();
				return;
			}

			if (Moon == MoonState.Countdown && --moonTimer <= 0) {
				Depths.DepthsSystem.SpawnGate(() => NPC.NewNPC(Entity.GetSource_NaturalSpawn(), arena.Center.X * 16, (Homestead.MoonFloorY(this) - 40) * 16, NPCID.MoonLordCore));
				Moon = MoonState.Fighting;
				Sync();
			}
		}


		public static void Sync()
		{
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		// ------------------------------------------------------------------ world actions (purchases etc.)

		public void RequestBuild(string key)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				TerralayerNet.SendWorldAction(WorldAction.Build, 0, key);
				return;
			}
			ApplyAction(WorldAction.Build, 0, key);
		}

		public void ApplyAction(WorldAction action, int arg, string text)
		{
			switch (action) {
				case WorldAction.Build: {
					StructureDef next = NextStructure;
					if (next == null || next.Key != text)
						return; // strictly linear
					Structures.Build(next, Structures.PlotCenter(Structures.IndexOf(next.Key), LobbyCenterX), LobbyGroundY);
					Built.Add(next.Key);
					Chat.Announce($"The {next.Name} has been built on the homestead!", new Color(255, 215, 120));
					break;
				}
				case WorldAction.TogglePin:
					LayoutPinned = !LayoutPinned;
					break;
				case WorldAction.SetMaxFloor:
					MaxFloorUnlocked = Math.Clamp(arg, 1, Depths.FloorDefs.Count);
					break;
				case WorldAction.ToggleAllBosses: {
					// Test command: every guardian slain (relics up, Moon Lord portal open), or none.
					bool all = AllGuardiansSlain;
					if (all)
						BossesSlain.Clear();
					else
						foreach (var r in Homestead.Relics)
							if (r.boss != NPCID.MoonLordCore)
								BossesSlain.Add(r.boss);
					Homestead.BuildShrine(this);
					if (all)
						Homestead.ClearRelics(this);
					Chat.Announce(all ? "Every guardian forgotten: relics cleared." : "Every guardian slain: the sky above the shrine tears open...", new Color(110, 230, 255));
					break;
				}
				case WorldAction.BuildAll:
					while (NextStructure != null)
						ApplyAction(WorldAction.Build, 0, NextStructure.Key);
					break;
			}
			Sync();
		}

		// ------------------------------------------------------------------ world generation

		public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
		{
			GenPass reset = tasks.FirstOrDefault(t => t.Name == "Reset");
			tasks.Clear();
			if (reset != null)
				tasks.Add(reset);
			tasks.Add(new PassLegacy("Terralayer: Bedrock", GenBedrock, 100));
			tasks.Add(new PassLegacy("Terralayer: Homestead", GenHomestead, 20));
			totalWeight = tasks.Sum(t => t.Weight);
		}

		private void GenBedrock(GenerationProgress progress, GameConfiguration configuration)
		{
			progress.Message = "Raising the bedrock";
			int w = Main.maxTilesX;
			int h = Main.maxTilesY;
			int cx = w / 2;
			int ground = (int)(h * 0.3);

			IsTerralayerWorld = true;
			LobbyCenterX = cx;
			LobbyGroundY = ground;
			Main.worldSurface = ground + 30;
			Main.rockLayer = ground + 90;
			GenVars.worldSurfaceLow = ground - 20;
			GenVars.worldSurface = ground;
			GenVars.worldSurfaceHigh = ground + 30;
			GenVars.rockLayerLow = ground + 60;
			GenVars.rockLayer = ground + 90;
			GenVars.rockLayerHigh = ground + 120;

			var rng = new Random(WorldGen.genRand.Next());
			double phase = rng.NextDouble() * 10;

			for (int x = 0; x < w; x++) {
				int dist = Math.Abs(x - cx);
				int surface = ground;
				int edge = LobbyHalfWidth - 50;
				if (dist > edge) {
					// Cliffs rise around the valley, then rolling peaks out to the world edge.
					double rise = (dist - edge) * 2.4 + Math.Sin(x * 0.05 + phase) * 6 + Math.Sin(x * 0.013 + phase * 2) * 12;
					surface = Math.Max(45, ground - (int)rise);
				}

				for (int y = 0; y < h; y++) {
					Tile t = Main.tile[x, y];
					t.ClearEverything();
					if (y < surface)
						continue;

					t.HasTile = true;
					if (y == surface)
						t.TileType = TileID.Grass;
					else if (y < surface + 22 && y < ground + 22)
						t.TileType = TileID.Dirt;
					else
						t.TileType = TileID.Stone;

					if (y > surface + 1)
						t.WallType = y < ground + 22 ? WallID.DirtUnsafe : WallID.Stone;
				}

				if (x % 50 == 0)
					progress.Set(x / (double)w);
			}
		}

		private void GenHomestead(GenerationProgress progress, GameConfiguration configuration)
		{
			progress.Message = "Founding the homestead";
			int cx = LobbyCenterX;
			int ground = LobbyGroundY;
			var rng = new Random(WorldGen.genRand.Next());

			// Stone plaza at spawn.
			for (int x = cx - 14; x <= cx + 14; x++)
				Gen.Solid(x, ground, TileID.StoneSlab);

			Main.spawnTileX = cx;
			Main.spawnTileY = ground;

			// The two buildings you start with.
			Built.Clear();
			for (int i = 0; i < 2; i++) {
				StructureDef d = Structures.All[i];
				Structures.Build(d, Structures.PlotCenter(i, cx), ground);
				Built.Add(d.Key);
			}

			// Keep every future plot clear; plant trees and flowers elsewhere in the valley.
			var reserved = new List<Rectangle> { new(cx - 24, 0, 50, ground + 1) };
			for (int i = 0; i < Structures.All.Count; i++) {
				StructureDef d = Structures.All[i];
				int pc = Structures.PlotCenter(i, cx);
				reserved.Add(new Rectangle(pc - d.Width / 2 - 4, 0, d.Width + 8, ground + 1));
			}

			for (int x = cx - LobbyHalfWidth + 60; x < cx + LobbyHalfWidth - 60; x++) {
				if (reserved.Any(r => r.Contains(x, ground)))
					continue;
				if (rng.Next(9) == 0 && Gen.IsAir(x, ground - 1))
					WorldGen.GrowTree(x, ground);
				else if (rng.Next(3) == 0 && Gen.IsAir(x, ground - 1))
					Gen.Place(x, ground - 1, TileID.Plants);
			}

			Gen.PlaceOnFloor(cx - 12, ground, TileID.Lampposts);
			Gen.PlaceOnFloor(cx + 12, ground, TileID.Lampposts);
			Gen.PlaceOnFloor(cx + 5, ground, TileID.Campfire);
			BossesSlain.Clear();
			Homestead.BuildShrine(this);
			ShrineBuilt = true;
			ShrineAboveSpawn = true;
			Homestead.PlaceDummies(this);
			CreditSign = Homestead.PlaceCreditSign(this);
		}

		// ------------------------------------------------------------------ global rules

		public override void PostUpdateWorld()
		{
			if (!IsTerralayerWorld)
				return;

			// No invasions, moons or slime rain: the Depths are the only threat.
			if (Main.invasionType != 0) {
				Main.invasionType = 0;
				Main.invasionSize = 0;
			}
			Main.bloodMoon = false;
			Main.eclipse = false;
			Main.pumpkinMoon = false;
			Main.snowMoon = false;
			if (Main.slimeRain)
				Main.StopSlimeRain(false);
			Main.hardMode = false;
			UpdateMoonFight();
			if (Main.GameUpdateCount % 300 == 0)
				Content.NPCs.Herald.EnsurePresent();
		}
	}
}
