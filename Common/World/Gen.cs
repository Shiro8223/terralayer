using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Common.World
{
	/// <summary>
	/// Low-level tile editing helpers shared by world generation, the homestead builder and the dungeon builder.
	/// Everything writes tiles directly; call <see cref="FrameArea"/> once shapes are final.
	/// </summary>
	public static class Gen
	{
		/// <summary>True while Terralayer itself is editing tiles, so the world protection hooks stand aside.</summary>
		public static bool Editing { get; private set; }

		public static void Run(Action edit)
		{
			bool was = Editing;
			Editing = true;
			try {
				edit();
			}
			finally {
				Editing = was;
			}
		}

		public static bool InWorld(int x, int y) => x > 1 && y > 1 && x < Main.maxTilesX - 2 && y < Main.maxTilesY - 2;

		public static void Solid(int x, int y, ushort type)
		{
			if (!InWorld(x, y))
				return;
			Tile t = Main.tile[x, y];
			t.HasTile = true;
			t.TileType = type;
			t.Slope = SlopeType.Solid;
			t.IsHalfBlock = false;
			t.TileFrameX = 0;
			t.TileFrameY = 0;
			t.LiquidAmount = 0;
		}

		public static void Solid(Rectangle r, ushort type)
		{
			for (int x = r.Left; x < r.Right; x++)
				for (int y = r.Top; y < r.Bottom; y++)
					Solid(x, y, type);
		}

		public static void Air(int x, int y)
		{
			if (!InWorld(x, y))
				return;
			Tile t = Main.tile[x, y];
			t.HasTile = false;
			t.Slope = SlopeType.Solid;
			t.IsHalfBlock = false;
		}

		public static void Air(Rectangle r)
		{
			for (int x = r.Left; x < r.Right; x++)
				for (int y = r.Top; y < r.Bottom; y++)
					Air(x, y);
		}

		public static void WallAt(int x, int y, ushort wall)
		{
			if (InWorld(x, y))
				Main.tile[x, y].WallType = wall;
		}

		public static void Wall(Rectangle r, ushort wall)
		{
			for (int x = r.Left; x < r.Right; x++)
				for (int y = r.Top; y < r.Bottom; y++)
					WallAt(x, y, wall);
		}

		/// <summary>Wipes everything (tiles, walls, liquid, wires) in a rectangle and fills it solid.</summary>
		public static void Reset(Rectangle r, ushort type, ushort wall)
		{
			for (int x = r.Left; x < r.Right; x++) {
				for (int y = r.Top; y < r.Bottom; y++) {
					if (!InWorld(x, y))
						continue;
					Tile t = Main.tile[x, y];
					t.ClearEverything();
					t.HasTile = true;
					t.TileType = type;
					t.WallType = wall;
				}
			}
		}

		public static bool IsAir(int x, int y) => InWorld(x, y) && !Main.tile[x, y].HasTile;

		public static bool IsSolid(int x, int y)
		{
			if (!InWorld(x, y))
				return true;
			Tile t = Main.tile[x, y];
			return t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType];
		}

		/// <summary>Solid or a platform: something you can stand on.</summary>
		public static bool IsStandable(int x, int y)
		{
			if (!InWorld(x, y))
				return false;
			Tile t = Main.tile[x, y];
			return t.HasTile && (Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType]);
		}

		public static void Platform(int x, int y, int width, int style)
		{
			for (int i = x; i < x + width; i++) {
				if (!InWorld(i, y))
					continue;
				Tile t = Main.tile[i, y];
				if (t.HasTile && t.TileType != TileID.Platforms)
					continue;
				t.HasTile = true;
				t.TileType = TileID.Platforms;
				t.TileFrameY = (short)(style * 18);
				t.Slope = SlopeType.Solid;
				t.IsHalfBlock = false;
			}
		}

		/// <summary>
		/// A jump-through ladder of platforms between two standing surfaces (<paramref name="topY"/>, e.g. a ledge, and
		/// <paramref name="bottomY"/>, e.g. the floor). Rungs are spaced evenly so no gap exceeds <paramref name="spacing"/>.
		/// </summary>
		public static void PlatformLadder(int x, int topY, int bottomY, int width, int style, int spacing = 4)
		{
			int distance = bottomY - topY;
			if (distance <= spacing)
				return;
			int rungs = (distance + spacing - 1) / spacing - 1;
			for (int i = 1; i <= rungs; i++)
				Platform(x, topY + distance * i / (rungs + 1), width, style);
		}

		/// <summary>A climbable rope from <paramref name="yTop"/> down to <paramref name="yBottom"/> (inclusive), only through air.</summary>
		public static void Rope(int x, int yTop, int yBottom, ushort type = TileID.Rope)
		{
			for (int y = yTop; y <= yBottom; y++) {
				if (!InWorld(x, y))
					continue;
				Tile t = Main.tile[x, y];
				if (t.HasTile)
					continue;
				t.HasTile = true;
				t.TileType = type;
			}
		}

		public static void Liquid(Rectangle r, int liquidType)
		{
			for (int x = r.Left; x < r.Right; x++) {
				for (int y = r.Top; y < r.Bottom; y++) {
					if (!InWorld(x, y))
						continue;
					Tile t = Main.tile[x, y];
					if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
						continue;
					t.LiquidType = liquidType;
					t.LiquidAmount = 255;
				}
			}
		}

		/// <summary>Carves (or fills) an ellipse, with a little noise on the edge for an organic look.</summary>
		public static void Ellipse(int cx, int cy, int rx, int ry, bool solid, ushort type, Random rng, float roughness = 0.12f, Rectangle? clip = null)
		{
			for (int x = cx - rx - 2; x <= cx + rx + 2; x++) {
				for (int y = cy - ry - 2; y <= cy + ry + 2; y++) {
					if (clip.HasValue && !clip.Value.Contains(x, y))
						continue;
					float dx = (x - cx) / (float)rx;
					float dy = (y - cy) / (float)ry;
					float d = dx * dx + dy * dy;
					float edge = 1f + ((float)rng.NextDouble() - 0.5f) * roughness * 2f;
					if (d <= edge) {
						if (solid) Solid(x, y, type);
						else Air(x, y);
					}
				}
			}
		}

		/// <summary>Carves a thick line (tunnel). Shallow slopes become walkable 1-tile steps.</summary>
		public static void Tunnel(Vector2 a, Vector2 b, int radius, Rectangle? clip = null)
		{
			float len = Vector2.Distance(a, b);
			int steps = Math.Max(1, (int)len);
			for (int s = 0; s <= steps; s++) {
				Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
				for (int x = (int)p.X - radius; x <= (int)p.X + radius; x++) {
					for (int y = (int)p.Y - radius; y <= (int)p.Y + radius; y++) {
						if (clip.HasValue && !clip.Value.Contains(x, y))
							continue;
						if ((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y) <= radius * radius + 1)
							Air(x, y);
					}
				}
			}
		}

		/// <summary>Places a tile object (furniture etc.) at its origin. Fails silently if it doesn't fit.</summary>
		public static bool Place(int x, int y, int type, int style = 0)
		{
			if (!InWorld(x, y))
				return false;
			// PlaceTile claims success for some multi-tile objects (relics) without placing them, so check the result
			// and fall back to PlaceObject.
			if (WorldGen.PlaceTile(x, y, type, mute: true, forced: false, plr: -1, style: style) && Main.tile[x, y].HasTile && Main.tile[x, y].TileType == type)
				return true;
			return WorldGen.PlaceObject(x, y, type, mute: true, style: style);
		}

		/// <summary>Places something on the floor so that it rests on row <paramref name="floorY"/>.</summary>
		public static bool PlaceOnFloor(int x, int floorY, int type, int style = 0) => Place(x, floorY - 1, type, style);

		public static void Torch(int x, int y, int style)
		{
			if (IsAir(x, y))
				Place(x, y, TileID.Torches, style);
		}

		/// <summary>A ledge you are guaranteed to be able to stand on, with head room above. Used under rifts.</summary>
		public static void Ledge(int cx, int floorY, int halfWidth, ushort type, int headroom = 5)
		{
			for (int x = cx - halfWidth; x <= cx + halfWidth; x++) {
				if (!IsStandable(x, floorY))
					Solid(x, floorY, type);
				for (int y = floorY - headroom; y < floorY; y++)
					Air(x, y);
			}
		}

		/// <summary>Turns exposed faces of <paramref name="from"/> into <paramref name="grass"/> (e.g. mud to jungle grass).</summary>
		public static void Grassify(Rectangle r, ushort from, ushort grass)
		{
			for (int x = r.Left; x < r.Right; x++) {
				for (int y = r.Top; y < r.Bottom; y++) {
					if (!InWorld(x, y))
						continue;
					Tile t = Main.tile[x, y];
					if (!t.HasTile || t.TileType != from)
						continue;
					if (IsAir(x, y - 1) || IsAir(x - 1, y) || IsAir(x + 1, y) || IsAir(x, y + 1))
						t.TileType = grass;
				}
			}
		}

		/// <summary>Rounds off jagged corners the way vanilla world gen does.</summary>
		public static void Smooth(Rectangle r)
		{
			for (int x = r.Left + 1; x < r.Right - 1; x++) {
				for (int y = r.Top + 1; y < r.Bottom - 1; y++) {
					Tile t = Main.tile[x, y];
					if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType] && !Main.tileFrameImportant[t.TileType])
						Tile.SmoothSlope(x, y, applyToNeighbors: false);
				}
			}
		}

		public static void FrameArea(Rectangle r)
		{
			for (int x = r.Left - 1; x <= r.Right; x++) {
				for (int y = r.Top - 1; y <= r.Bottom; y++) {
					if (!InWorld(x, y))
						continue;
					WorldGen.TileFrame(x, y, true, true);
					Framing.WallFrame(x, y, true);
				}
			}
		}

		/// <summary>Multiplayer: forces clients to re-download the tile sections covering an area.</summary>
		public static void Resync(Rectangle r)
		{
			if (Main.netMode != NetmodeID.Server)
				return;
			int sx0 = Netplay.GetSectionX(Math.Max(0, r.Left));
			int sx1 = Netplay.GetSectionX(Math.Min(Main.maxTilesX - 1, r.Right));
			int sy0 = Netplay.GetSectionY(Math.Max(0, r.Top));
			int sy1 = Netplay.GetSectionY(Math.Min(Main.maxTilesY - 1, r.Bottom));
			foreach (var client in Netplay.Clients) {
				if (!client.IsActive)
					continue;
				for (int sx = sx0; sx <= sx1; sx++)
					for (int sy = sy0; sy <= sy1; sy++)
						client.TileSections[sx, sy] = false;
			}
		}

		/// <summary>Removes chests, dropped items and non-town NPCs inside an area.</summary>
		public static void ClearEntities(Rectangle r, bool includeFriendly = false)
		{
			for (int i = 0; i < Main.maxChests; i++) {
				Chest chest = Main.chest[i];
				if (chest != null && r.Contains(chest.x, chest.y))
					Main.chest[i] = null;
			}

			Rectangle px = new(r.X * 16, r.Y * 16, r.Width * 16, r.Height * 16);
			for (int i = 0; i < Main.maxItems; i++) {
				Item item = Main.item[i];
				if (item.active && px.Contains(item.Center.ToPoint())) {
					item.active = false;
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.SyncItem, -1, -1, null, i);
				}
			}

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc.active && !npc.townNPC && (includeFriendly || !npc.friendly) && px.Contains(npc.Center.ToPoint())) {
					npc.active = false;
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, i);
				}
			}
		}
	}
}
