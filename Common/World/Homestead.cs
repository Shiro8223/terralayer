using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;

namespace Terralayer.Common.World
{
	/// <summary>
	/// Fixtures on the homestead beyond the buildings: the Shrine of Guardians with its relics, training dummies at
	/// spawn, and the hidden Moon Lord arena high above the valley.
	/// </summary>
	public static class Homestead
	{
		/// <summary>Guardians in shrine order, with the Master Mode relic each one raises.</summary>
		public static readonly (int boss, int relic, string name)[] Relics = {
			(NPCID.KingSlime, ItemID.KingSlimeMasterTrophy, "King Slime"),
			(NPCID.EyeofCthulhu, ItemID.EyeofCthulhuMasterTrophy, "Eye of Cthulhu"),
			(NPCID.QueenBee, ItemID.QueenBeeMasterTrophy, "Queen Bee"),
			(NPCID.Deerclops, ItemID.DeerclopsMasterTrophy, "Deerclops"),
			(NPCID.SkeletronHead, ItemID.SkeletronMasterTrophy, "Skeletron"),
			(NPCID.WallofFlesh, ItemID.WallofFleshMasterTrophy, "Wall of Flesh"),
			(NPCID.MoonLordCore, ItemID.MoonLordMasterTrophy, "Moon Lord"),
			(NPCID.TheDestroyer, ItemID.DestroyerMasterTrophy, "The Destroyer"),
			(NPCID.SkeletronPrime, ItemID.SkeletronPrimeMasterTrophy, "Skeletron Prime"),
			(NPCID.Retinazer, ItemID.TwinsMasterTrophy, "The Twins"),
			(NPCID.QueenSlimeBoss, ItemID.QueenSlimeMasterTrophy, "Queen Slime"),
			(NPCID.Plantera, ItemID.PlanteraMasterTrophy, "Plantera"),
			(NPCID.Golem, ItemID.GolemMasterTrophy, "Golem"),
			(NPCID.HallowBoss, ItemID.FairyQueenMasterTrophy, "Empress of Light"),
		};

		public static int GuardianCount => Relics.Length - 1; // everyone but the Moon Lord

		/// <summary>
		/// The Shrine of Guardians floats over the spawn point: two marble tiers of seven relics each, the lower one
		/// 15 tiles above the plaza, reached by a ladder of platforms on its left. It sits between the Founder's Hall and
		/// the Gatehouse roofs.
		/// </summary>
		public const int ShrineHalfWidth = 15;
		public const int TierA = 15;   // lower tier floor: tiles above the ground
		public const int TierB = 25;   // upper tier floor

		public static int ShrineLeft(TerralayerWorld w) => w.LobbyCenterX - ShrineHalfWidth;

		public static int ShrineRight(TerralayerWorld w) => w.LobbyCenterX + ShrineHalfWidth;

		/// <summary>Everything the floating shrine and its ladder occupy.</summary>
		public static Rectangle ShrineArea(TerralayerWorld w) => new(w.LobbyCenterX - 22, w.LobbyGroundY - TierB - 12, 2 * ShrineHalfWidth + 8, TierB + 1);

		public static int RelicX(TerralayerWorld w, int index) => w.LobbyCenterX - 12 + (index % 7) * 4;

		/// <summary>The floor row a relic stands on: the first seven on the lower tier, the rest on the upper.</summary>
		public static int RelicFloor(TerralayerWorld w, int index) => w.LobbyGroundY - (index < 7 ? TierA : TierB);

		/// <summary>The secret portal floats just above the upper tier (bottom-center tile), in reach from it.</summary>
		public static Point MoonPortalPos(TerralayerWorld w) => new(w.LobbyCenterX, w.LobbyGroundY - TierB - 6);

		public static Rectangle PortalArea(Point p) => new((p.X - 1) * 16, (p.Y - 4) * 16, 48, 64);

		public static bool IsRelicItem(int type) => Array.Exists(Relics, r => r.relic == type) || type == ItemID.DukeFishronMasterTrophy;

		public static void BuildShrine(TerralayerWorld w)
		{
			Gen.Run(() => {
				int g = w.LobbyGroundY;
				int cx = w.LobbyCenterX;
				int left = ShrineLeft(w), right = ShrineRight(w);
				Rectangle area = ShrineArea(w);

				// Clear the air (and any trees growing up into it) above the plaza's furniture.
				for (int x = area.Left; x < area.Right; x++)
					for (int y = area.Top; y < g; y++) {
						Tile t = Main.tile[x, y];
						if (t.HasTile && (t.TileType == TileID.Trees || y < g - 10))
							WorldGen.KillTile(x, y, noItem: true);
						if (y < g - 10)
							Gen.WallAt(x, y, 0);
					}

				foreach (int tier in new[] { TierA, TierB }) {
					int fy = g - tier;
					// A two-tile marble slab with a tapering underside.
					Gen.Solid(new Rectangle(left, fy, right - left + 1, 2), TileID.MarbleBlock);
					Gen.Solid(new Rectangle(left + 3, fy + 2, right - left - 5, 1), TileID.Marble);
					Gen.Solid(new Rectangle(left + 7, fy + 3, right - left - 13, 1), TileID.Marble);
					// Back wall with columns.
					for (int x = left; x <= right; x++)
						for (int y = fy - 9; y < fy; y++)
							Gen.WallAt(x, y, (x - left) % 5 == 0 ? WallID.MarbleBlock : WallID.Marble);
				}

				// The ladder: rungs from the plaza to each tier, with a landing at both tier heights.
				int lx = left - 6;
				Gen.Platform(lx, g - TierA, 6, 0);
				Gen.Platform(lx, g - TierB, 6, 0);
				Gen.PlatformLadder(lx, g - TierA, g, 6, 0);
				Gen.PlatformLadder(lx, g - TierB, g - TierA, 6, 0);

				Gen.FrameArea(area);
				foreach (int tier in new[] { TierA, TierB }) {
					Gen.Torch(left + 1, g - tier - 6, 0);
					Gen.Torch(right - 1, g - tier - 6, 0);
				}
				Gen.Place(left + 1, g - TierA + 4, TileID.Banners, 2);
				Gen.Place(right - 1, g - TierA + 4, TileID.Banners, 2);
				Gen.FrameArea(area);
				Gen.Resync(area);
			});
			for (int i = 0; i < Relics.Length; i++)
				if (w.BossesSlain.Contains(Relics[i].boss))
					PlaceRelic(w, i);
			if (w.DukeRelic)
				PlaceDukeRelic(w);
		}

		/// <summary>Older worlds had the shrine 300 tiles east, on the ground: take it down (no items left behind).</summary>
		public static void RemoveOldShrine(TerralayerWorld w)
		{
			Gen.Run(() => {
				int g = w.LobbyGroundY;
				var old = new Rectangle(w.LobbyCenterX + 297, g - 24, 66, 25);
				for (int x = old.Left; x < old.Right; x++) {
					for (int y = old.Top; y < g; y++) {
						Tile t = Main.tile[x, y];
						if (t.HasTile)
							WorldGen.KillTile(x, y, noItem: true);
						t.ClearEverything();
					}
					Gen.Solid(x, g, TileID.Grass);
				}
				Gen.FrameArea(old);
				Gen.Resync(old);
			});
		}

		/// <summary>Raises a relic in its spot. Idempotent: a relic already standing there is left alone.</summary>
		public static void PlaceRelic(TerralayerWorld w, int index)
		{
			int x = RelicX(w, index);
			int floor = RelicFloor(w, index);
			Gen.Run(() => {
				if (RelicStandsAt(x, floor))
					return;
				int style = ContentSamples.ItemsByType[Relics[index].relic].placeStyle;
				for (int dx = -1; dx <= 1; dx++)
					for (int y = floor - 4; y < floor; y++) {
						if (Main.tile[x + dx, y].HasTile)
							WorldGen.KillTile(x + dx, y, noItem: true);
						Gen.Air(x + dx, y);
					}
				WorldGen.PlaceObject(x, floor - 1, TileID.MasterTrophyBase, mute: true, style: style);
				// Never leave half a relic standing (it would break and drop as an item).
				if (!RelicStandsAt(x, floor))
					for (int dx = -1; dx <= 1; dx++)
						for (int y = floor - 4; y < floor; y++)
							if (Main.tile[x + dx, y].TileType == TileID.MasterTrophyBase)
								Gen.Air(x + dx, y);
				Gen.FrameArea(new Rectangle(x - 2, floor - 5, 5, 6));
				Gen.Resync(new Rectangle(x - 2, floor - 5, 5, 6));
			});
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendTileSquare(-1, x - 1, floor - 4, 3, 4);
		}

		/// <summary>Duke Fishron's relic: its own floating pedestal right of the shrine (secret quest).</summary>
		public static void PlaceDukeRelic(TerralayerWorld w)
		{
			int x = w.LobbyCenterX + 20, floor = w.LobbyGroundY - TierA;
			Gen.Run(() => {
				Gen.Solid(new Rectangle(x - 2, floor, 5, 2), TileID.MarbleBlock);
				Gen.Solid(new Rectangle(x - 1, floor + 2, 3, 1), TileID.Marble);
				for (int dx = -1; dx <= 1; dx++)
					for (int y = floor - 4; y < floor; y++) {
						if (Main.tile[x + dx, y].HasTile)
							WorldGen.KillTile(x + dx, y, noItem: true);
						Gen.Air(x + dx, y);
					}
				Gen.FrameArea(new Rectangle(x - 3, floor - 5, 7, 9));
				WorldGen.PlaceObject(x, floor - 1, TileID.MasterTrophyBase, mute: true, style: ContentSamples.ItemsByType[ItemID.DukeFishronMasterTrophy].placeStyle);
				Gen.FrameArea(new Rectangle(x - 3, floor - 5, 7, 9));
				Gen.Resync(new Rectangle(x - 3, floor - 5, 7, 9));
			});
		}

		/// <summary>The rift to the hidden sea, on the plaza past the Gatehouse (bottom-center tile).</summary>
		public static Point SeaPortalPos(TerralayerWorld w) => new(w.LobbyCenterX + 60, w.LobbyGroundY);

		/// <summary>Takes down every relic that isn't earned (test command).</summary>
		public static void ClearRelics(TerralayerWorld w)
		{
			Gen.Run(() => {
				for (int i = 0; i < Relics.Length; i++) {
					if (w.BossesSlain.Contains(Relics[i].boss))
						continue;
					int x = RelicX(w, i), floor = RelicFloor(w, i);
					for (int dx = -1; dx <= 1; dx++)
						for (int y = floor - 4; y < floor; y++)
							if (Main.tile[x + dx, y].TileType == TileID.MasterTrophyBase)
								Gen.Air(x + dx, y);
					Gen.FrameArea(new Rectangle(x - 2, floor - 5, 5, 6));
					Gen.Resync(new Rectangle(x - 2, floor - 5, 5, 6));
				}
			});
		}

		private static bool RelicStandsAt(int x, int floor)
		{
			int n = 0;
			for (int dx = -1; dx <= 1; dx++)
				for (int y = floor - 4; y < floor; y++)
					if (Main.tile[x + dx, y].HasTile && Main.tile[x + dx, y].TileType == TileID.MasterTrophyBase)
						n++;
			return n == 12;
		}

		/// <summary>Two target dummies just east of spawn, for trying out gear.</summary>
		public static void PlaceDummies(TerralayerWorld w)
		{
			Gen.Run(() => {
				foreach (int x in new[] { w.LobbyCenterX + 17, w.LobbyCenterX + 21 }) {
					if (HasTileNear(x, w.LobbyGroundY - 1, TileID.TargetDummy))
						continue;
					for (int dx = 0; dx < 2; dx++)
						for (int y = w.LobbyGroundY - 3; y < w.LobbyGroundY; y++)
							Gen.Air(x + dx, y);
					if (!WorldGen.PlaceObject(x, w.LobbyGroundY - 1, TileID.TargetDummy, mute: true))
						continue;
					// The dummy needs its tile entity at its top-left tile.
					for (int ty = w.LobbyGroundY - 4; ty < w.LobbyGroundY; ty++) {
						for (int tx = x - 2; tx <= x + 2; tx++) {
							Tile t = Main.tile[tx, ty];
							if (t.HasTile && t.TileType == TileID.TargetDummy && t.TileFrameX % 36 == 0 && t.TileFrameY == 0) {
								TETrainingDummy.Place(tx, ty);
								ty = w.LobbyGroundY;
								break;
							}
						}
					}
				}
				Gen.Resync(new Rectangle(w.LobbyCenterX + 15, w.LobbyGroundY - 4, 10, 5));
			});
		}

		/// <summary>A wooden sign on the plaza crediting the mod's inspiration. Returns true once it stands.</summary>
		public static bool PlaceCreditSign(TerralayerWorld w)
		{
			bool placed = false;
			Gen.Run(() => {
				int x = w.LobbyCenterX + 8;
				int g = w.LobbyGroundY;
				for (int dx = 0; dx < 2; dx++) {
					Gen.Solid(x + dx, g, TileID.StoneSlab);
					for (int y = g - 2; y < g; y++)
						Gen.Air(x + dx, y);
				}
				Gen.FrameArea(new Rectangle(x - 1, g - 3, 4, 4));
				if (!Gen.PlaceOnFloor(x, g, TileID.Signs))
					return;
				for (int ty = g - 3; ty < g && !placed; ty++)
					for (int tx = x - 1; tx <= x + 2 && !placed; tx++) {
						Tile t = Main.tile[tx, ty];
						if (!t.HasTile || t.TileType != TileID.Signs)
							continue;
						int id = Sign.ReadSign(tx, ty, true);
						if (id >= 0) {
							Sign.TextSign(id, TerralayerWorld.CreditText);
							placed = true;
						}
					}
				Gen.Resync(new Rectangle(x - 1, g - 3, 4, 4));
			});
			return placed;
		}

		private static bool HasTileNear(int x, int y, int type)
		{
			for (int dx = -1; dx <= 2; dx++)
				for (int dy = -3; dy <= 0; dy++)
					if (Main.tile[x + dx, y + dy].HasTile && Main.tile[x + dx, y + dy].TileType == type)
						return true;
			return false;
		}

		/// <summary>
		/// Buildings without a resident must not count as housing, or vanilla keeps spawning (and announcing) townsfolk
		/// into them. Strip their tables and chairs.
		/// </summary>
		public static void RepairStructures(TerralayerWorld w)
		{
			Gen.Run(() => {
				for (int i = 0; i < Structures.All.Count; i++) {
					StructureDef d = Structures.All[i];
					if (d.Resident != null || !d.House || !w.HasBuilt(d.Key))
						continue;
					Rectangle area = Structures.Footprint(d, Structures.PlotCenter(i, w.LobbyCenterX), w.LobbyGroundY);
					for (int x = area.Left; x < area.Right; x++)
						for (int y = area.Top; y < area.Bottom; y++) {
							Tile t = Main.tile[x, y];
							if (t.HasTile && (t.TileType == TileID.Tables || t.TileType == TileID.Chairs || t.TileType == TileID.WorkBenches))
								WorldGen.KillTile(x, y, noItem: true);
						}
					Gen.Resync(area);
				}
			});
		}

		// ------------------------------------------------------------------ Moon Lord arena

		/// <summary>A wide lunar platform high above the valley, in the thin air near space.</summary>
		public static Rectangle MoonArena(TerralayerWorld w)
		{
			int floorY = Math.Clamp(w.LobbyGroundY - 200, 110, w.LobbyGroundY - 120);
			return new Rectangle(w.LobbyCenterX - 170, floorY - 90, 340, 96);
		}

		public static int MoonFloorY(TerralayerWorld w) => MoonArena(w).Bottom - 6;

		public static Point MoonReturnPos(TerralayerWorld w) => new(MoonArena(w).X + 12, MoonFloorY(w));

		public static Vector2 MoonArrival(TerralayerWorld w, Player p) => new(MoonArena(w).X * 16 + 24 * 16, (MoonFloorY(w) - 3) * 16 - 1);

		public static void BuildMoonArena(TerralayerWorld w)
		{
			Gen.Run(() => {
				Rectangle a = MoonArena(w);
				int f = MoonFloorY(w);
				for (int x = a.Left; x < a.Right; x++)
					for (int y = a.Top; y < a.Bottom; y++) {
						Tile t = Main.tile[x, y];
						t.ClearEverything();
					}
				Gen.Solid(new Rectangle(a.X, f, a.Width, 3), TileID.LunarBrick);
				for (int tier = 1; tier <= 4; tier++) {
					int y = f - 14 * tier;
					int seg = a.Width / 3;
					if (tier % 2 == 1) {
						Gen.Platform(a.X + 10, y, seg, 0);
						Gen.Platform(a.Right - 10 - seg, y, seg, 0);
					}
					else {
						Gen.Platform(a.Center.X - seg / 2, y, seg, 0);
					}
				}
				Gen.FrameArea(a);
				Gen.PlaceOnFloor(a.X + 40, f, TileID.Campfire);
				Gen.PlaceOnFloor(a.Right - 40, f, TileID.Campfire);
				Gen.PlaceOnFloor(a.Center.X, f, TileID.Campfire);
				Gen.FrameArea(a);
				Gen.Resync(a);
			});
		}
	}
}
