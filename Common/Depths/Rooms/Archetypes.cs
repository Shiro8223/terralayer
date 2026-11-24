using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terralayer.Common.World;
using Terraria;
using Terraria.ID;

namespace Terralayer.Common.Depths.Rooms
{
	// =====================================================================================================
	// Start: a small safe room with a campfire and the Retreat rift.
	// =====================================================================================================
	public class StartRoom : RoomArchetype
	{
		public override (int w, int h) Size(Random rng) => (Rand(rng, 84, 96), Rand(rng, 40, 46));

		public override void Layout(RoomPlan r, Random rng) => StandardAnchors(r, r.FloorY - 14);

		public override void Build(RoomPlan r, FloorInfo f, Random rng) => UpLedge(r.Interior.Center.X, r.FloorY - 14, r.FloorY, f.PlatformStyle);

		public override void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			base.Decorate(r, f, rng);
			Rectangle I = r.Interior;
			Gen.PlaceOnFloor(I.Center.X + 7, r.FloorY, TileID.Campfire);
			Gen.Place(I.X + 6, I.Y, TileID.Banners, rng.Next(4));
			Gen.Place(I.Right - 7, I.Y, TileID.Banners, rng.Next(4));
			Chandeliers(r, f, rng, 30);
		}
	}

	// =====================================================================================================
	// Hall: a wide, tall chamber with balconies on both sides, columns and chandeliers.
	// =====================================================================================================
	public class HallRoom : RoomArchetype
	{
		public override (int w, int h) Size(Random rng) => (Rand(rng, 130, 190), Rand(rng, 55, 75));

		private static int UpY(RoomPlan r) => Math.Max(r.Interior.Y + 8, r.FloorY - 30);

		public override void Layout(RoomPlan r, Random rng) => StandardAnchors(r, UpY(r));

		public override void Build(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			int floor = r.FloorY;
			int ps = f.PlatformStyle;

			for (int x = I.X + 10; x < I.Right - 10; x += 22)
				Gen.Wall(new Rectangle(x, I.Y, 3, I.Height), f.RockWall);

			UpLedge(I.Center.X, UpY(r), floor, ps);

			// Balconies on both sides at every tier, joined by ladders at their inner ends.
			int balcony = (int)(I.Width * Rand(rng, 22, 30) / 100f);
			int topTier = floor;
			for (int y = floor - 12; y > I.Y + 8; y -= 12) {
				Gen.Platform(I.X, y, balcony, ps);
				Gen.Platform(I.Right - balcony, y, balcony, ps);
				topTier = y;
			}
			if (topTier < floor) {
				Gen.PlatformLadder(I.X + balcony - 6, topTier, floor, 6, ps);
				Gen.PlatformLadder(I.Right - balcony, topTier, floor, 6, ps);
			}

			Roughen(r, rng, f.Rock, f.Brick, f.Brick);
		}

		public override void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			base.Decorate(r, f, rng);
			Rectangle I = r.Interior;
			for (int x = I.X + 16; x < I.Right - 16; x += Rand(rng, 8, 16))
				if (ClearOfRifts(r, x))
					Rubble(x, r.FloorY, rng);
			for (int x = I.X + 8; x < I.Right - 8; x += Rand(rng, 14, 22))
				Gen.Place(x, I.Y, TileID.Banners, rng.Next(4));
			Chandeliers(r, f, rng);
			ScatterPots(r, f, rng, 4);
		}
	}

	// =====================================================================================================
	// Cavern: organic chambers joined by tunnels, a rope shaft up to a hidden grotto, sometimes a pool.
	// =====================================================================================================
	public class CavernRoom : RoomArchetype
	{
		public class Data
		{
			public List<(int cx, int cy, int rx, int ry)> Chambers = new();
			public (int cx, int cy, int rx, int ry) Grotto;
			public int GrottoParent = -1;
			public int Pool = -1;
		}

		public override (int w, int h) Size(Random rng) => (Rand(rng, 150, 210), Rand(rng, 75, 100));

		// Natural-cave variants (mushroom, ice, desert...) reuse this layout with their own materials.
		protected virtual ushort CaveRock(FloorInfo f) => f.Rock;
		protected virtual ushort CaveWall(FloorInfo f) => f.RockWall;
		protected virtual ushort CaveGrass(FloorInfo f) => f.Grass;
		protected virtual int CaveTorch(FloorInfo f) => f.TorchStyle;
		protected virtual int CavePlatform(FloorInfo f) => f.PlatformStyle;
		/// <summary>After carving, before smoothing: veins, pockets and the like.</summary>
		protected virtual void CaveExtras(RoomPlan r, FloorInfo f, Random rng) { }
		protected virtual void CaveDecor(RoomPlan r, FloorInfo f, Random rng) { }

		/// <summary>Swaps buried tiles (solid on all four sides) of the cave rock for <paramref name="type"/> in blobs.</summary>
		protected void Veins(RoomPlan r, FloorInfo f, Random rng, ushort type, int count)
		{
			Rectangle I = r.Interior;
			ushort rock = CaveRock(f);
			for (int k = 0; k < count; k++) {
				int cx = Rand(rng, I.X + 4, I.Right - 4), cy = Rand(rng, I.Y + 4, I.Bottom - 4);
				int rx = Rand(rng, 3, 8), ry = Rand(rng, 2, 5);
				for (int x = cx - rx; x <= cx + rx; x++)
					for (int y = cy - ry; y <= cy + ry; y++) {
						float dx = (x - cx) / (float)rx, dy = (y - cy) / (float)ry;
						if (dx * dx + dy * dy > 1f || !I.Contains(x, y))
							continue;
						Tile t = Main.tile[x, y];
						if (t.HasTile && t.TileType == rock && Gen.IsSolid(x, y - 1) && Gen.IsSolid(x, y + 1) && Gen.IsSolid(x - 1, y) && Gen.IsSolid(x + 1, y))
							t.TileType = type;
					}
			}
		}

		private static int FloorOf((int cx, int cy, int rx, int ry) c) => c.cy + (int)(c.ry * 0.72f);

		public override void Layout(RoomPlan r, Random rng)
		{
			Rectangle I = r.Interior;
			var d = new Data();
			int k = Math.Clamp(I.Width / 45, 3, 5);
			int spacing = I.Width / k;

			int prevCy = 0;
			for (int i = 0; i < k; i++) {
				int rx = (int)(spacing * (0.52 + rng.NextDouble() * 0.1));
				int ry = (int)(I.Height * (0.17 + rng.NextDouble() * 0.07));
				int cx = I.X + spacing * i + spacing / 2 + rng.Next(-spacing / 10, spacing / 10 + 1);
				int minY = I.Y + ry + 18;
				int maxY = I.Bottom - ry - 3;
				int cy = i == 0 ? rng.Next(minY + (maxY - minY) / 3, maxY + 1) : Math.Clamp(prevCy + rng.Next(-spacing * 2 / 5, spacing * 2 / 5 + 1), minY, maxY);
				d.Chambers.Add((cx, cy, Math.Max(10, rx), Math.Max(7, ry)));
				prevCy = cy;
			}

			// The grotto hangs above the highest middle chamber.
			int parent = Enumerable.Range(1, k - 2).OrderBy(i => d.Chambers[i].cy).First();
			var p = d.Chambers[parent];
			int gry = 5;
			int gcy = p.cy - p.ry - 10 - gry;
			if (gcy - gry > I.Y + 2) {
				d.GrottoParent = parent;
				d.Grotto = (p.cx, gcy, 13, gry);
			}

			int lowest = Enumerable.Range(0, k).OrderByDescending(i => d.Chambers[i].cy).First();
			// (Pools used to flood a middle chamber here; they slowed every crossing to a swim, so they're gone.)

			var first = d.Chambers[0];
			var last = d.Chambers[k - 1];
			var low = d.Chambers[lowest];
			r.Anchors[(int)Dir.Left] = new Point(first.cx - first.rx / 3, FloorOf(first));
			r.Anchors[(int)Dir.Right] = new Point(last.cx + last.rx / 3, FloorOf(last));
			r.Anchors[(int)Dir.Down] = new Point(low.cx + 6, FloorOf(low));
			r.Anchors[(int)Dir.Up] = d.GrottoParent >= 0
				? new Point(d.Grotto.cx + 7, d.Grotto.cy + d.Grotto.ry - 1)
				: new Point(p.cx - 6, FloorOf(p));
			r.Home = new Point(first.cx, FloorOf(first));
			r.ExitA = r.Home;
			r.ExitB = new Point(last.cx, FloorOf(last));
			r.Feature = new Point(low.cx - 6, FloorOf(low));
			r.Data = d;
		}

		public override void Build(RoomPlan r, FloorInfo f, Random rng)
		{
			var d = (Data)r.Data;
			Rectangle I = r.Interior;
			Gen.Solid(I, CaveRock(f));
			Gen.Wall(I, CaveWall(f));

			foreach (var c in d.Chambers)
				Gen.Ellipse(c.cx, c.cy, c.rx, c.ry, false, 0, rng, 0.14f, I);

			for (int i = 0; i + 1 < d.Chambers.Count; i++) {
				var a = d.Chambers[i];
				var b = d.Chambers[i + 1];
				Gen.Tunnel(new Vector2(a.cx, FloorOf(a) - 3), new Vector2(b.cx, FloorOf(b) - 3), 3, I);
			}

			if (d.GrottoParent >= 0) {
				var p = d.Chambers[d.GrottoParent];
				var g = d.Grotto;
				Gen.Ellipse(g.cx, g.cy, g.rx, g.ry, false, 0, rng, 0.1f, I);
				int grottoFloor = g.cy + g.ry - 1;
				Gen.Air(new Rectangle(g.cx - 2, grottoFloor, 5, p.cy - grottoFloor));
				Gen.Platform(g.cx - 2, grottoFloor, 5, CavePlatform(f));
				Gen.Rope(g.cx, grottoFloor + 1, FloorOf(p) - 1);
			}

			if (d.Pool >= 0) {
				var c = d.Chambers[d.Pool];
				int waterTop = c.cy + (int)(c.ry * 0.45f);
				Gen.Liquid(new Rectangle(c.cx - c.rx, waterTop, c.rx * 2, c.ry + 2), LiquidID.Water);
			}

			CaveExtras(r, f, rng);
			Gen.Smooth(I);
			if (CaveGrass(f) != 0)
				Gen.Grassify(I, CaveRock(f), CaveGrass(f));
		}

		public override void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			// Caves stay dark, except around the rifts and in a few chambers.
			var d = (Data)r.Data;
			foreach (Point a in r.Anchors) {
				Gen.Torch(a.X - 3, a.Y - 3, CaveTorch(f));
				Gen.Torch(a.X + 3, a.Y - 3, CaveTorch(f));
			}
			foreach (var c in d.Chambers)
				if (rng.NextDouble() < 0.5)
					Gen.Torch(c.cx, c.cy - c.ry + 2, CaveTorch(f));
			CaveDecor(r, f, rng);
			ScatterPots(r, f, rng, 7);
		}
	}

	// =====================================================================================================
	// Shaft: a tall climb. A rope runs up the middle; ledges alternate sides; some ledges are trapped.
	// =====================================================================================================
	public class ShaftRoom : RoomArchetype
	{
		public class Data
		{
			public List<(int y, bool left, bool trapped)> Ledges = new();
		}

		public override (int w, int h) Size(Random rng) => (Rand(rng, 56, 72), Rand(rng, 110, 150));

		public override void Layout(RoomPlan r, Random rng)
		{
			Rectangle I = r.Interior;
			var d = new Data();
			bool left = rng.Next(2) == 0;
			for (int y = r.FloorY - 12; y > I.Y + 8; y -= 12) {
				d.Ledges.Add((y, left, rng.NextDouble() < 0.35));
				left = !left;
			}

			int cx = I.Center.X;
			int mid = I.Y + I.Height / 2;
			var top = d.Ledges[^1];
			var rest = d.Ledges.Take(d.Ledges.Count - 1).ToList();
			var midLeft = rest.Where(l => l.left).OrderBy(l => Math.Abs(l.y - mid)).First();
			var midRight = rest.Where(l => !l.left).OrderBy(l => Math.Abs(l.y - mid)).First();

			r.Anchors[(int)Dir.Down] = new Point(cx + 9, r.FloorY);
			r.Anchors[(int)Dir.Left] = new Point(I.X + 5, midLeft.y);
			r.Anchors[(int)Dir.Right] = new Point(I.Right - 6, midRight.y);
			r.Anchors[(int)Dir.Up] = top.left ? new Point(I.X + 8, top.y) : new Point(I.Right - 9, top.y);
			r.Home = new Point(cx - 10, r.FloorY);
			r.ExitA = new Point(cx - 16, r.FloorY);
			r.ExitB = new Point(cx + 16, r.FloorY);
			r.Feature = new Point(I.X + 6, r.FloorY);
			r.Data = d;
		}

		public override void Build(RoomPlan r, FloorInfo f, Random rng)
		{
			var d = (Data)r.Data;
			Rectangle I = r.Interior;
			int cx = I.Center.X;

			foreach (var l in d.Ledges) {
				Rectangle ledge = l.left ? new Rectangle(I.X, l.y, cx - 2 - I.X, 2) : new Rectangle(cx + 3, l.y, I.Right - (cx + 3), 2);
				Gen.Solid(ledge, f.Brick);

				// A pressure plate near the tip arms a trap in the wall behind you.
				if (l.trapped) {
					int wallX = l.left ? I.X - 1 : I.Right;
					int plateX = l.left ? cx - 6 : cx + 6;
					Traps.Armed(plateX, l.y, wallX, l.y - 2, faceRight: l.left, f.TrapStyle);
				}
			}

			Gen.Rope(cx, I.Y + 2, r.FloorY - 1);

			for (int i = 0; i < 6; i++) {
				int x = rng.Next(I.X + 1, I.Right - 3);
				int y = rng.Next(I.Y + 2, r.FloorY - 4);
				if (Gen.IsAir(x, y) && x != cx)
					Gen.Solid(new Rectangle(x, y, 2, 2), TileID.Cobweb);
			}
		}

		public override void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			WallTorches(r, f, 10);
			ScatterPots(r, f, rng, 2);
		}
	}

	// =====================================================================================================
	// Flooded: shores at both ends, a sunken hall between them, pillars and floating platforms to cross.
	// =====================================================================================================
	public class FloodedRoom : RoomArchetype
	{
		public class Data
		{
			public int WaterLine;
			public int ShoreW = 14;
			public List<(int x, int top)> Pillars = new();
		}

		public override (int w, int h) Size(Random rng) => (Rand(rng, 150, 200), Rand(rng, 60, 80));

		public override void Layout(RoomPlan r, Random rng)
		{
			Rectangle I = r.Interior;
			var d = new Data();
			int depth = Math.Min(Rand(rng, 14, 20), I.Height - 22);
			d.WaterLine = r.FloorY - depth;
			int shoreTop = d.WaterLine - 2;

			for (int x = I.X + d.ShoreW + Rand(rng, 7, 10); x < I.Right - d.ShoreW - 7; x += Rand(rng, 12, 17))
				d.Pillars.Add((x, shoreTop + Rand(rng, -2, 1)));

			var mid = d.Pillars[d.Pillars.Count / 2];
			int upY = Math.Max(I.Y + 8, shoreTop - 16);
			r.Anchors[(int)Dir.Left] = new Point(I.X + 6, shoreTop);
			r.Anchors[(int)Dir.Right] = new Point(I.Right - 7, shoreTop);
			r.Anchors[(int)Dir.Down] = new Point(mid.x + 1, mid.top);
			r.Anchors[(int)Dir.Up] = new Point(mid.x + 1, upY);
			r.Home = new Point(I.X + 10, shoreTop);
			r.ExitA = r.Home;
			r.ExitB = new Point(I.Right - 11, shoreTop);
			r.Feature = new Point(I.X + d.ShoreW + 3, r.FloorY);
			r.Data = d;
		}

		public override void Build(RoomPlan r, FloorInfo f, Random rng)
		{
			var d = (Data)r.Data;
			Rectangle I = r.Interior;
			int floor = r.FloorY;
			int shoreTop = d.WaterLine - 2;

			Gen.Solid(new Rectangle(I.X, shoreTop, d.ShoreW, floor - shoreTop), f.Brick);
			Gen.Solid(new Rectangle(I.Right - d.ShoreW, shoreTop, d.ShoreW, floor - shoreTop), f.Brick);

			int prevX = I.X + d.ShoreW;
			int prevTop = shoreTop;
			foreach (var p in d.Pillars) {
				Gen.Solid(new Rectangle(p.x, p.top, 3, floor - p.top), f.Accent);
				if (p.x - prevX > 6)
					Gen.Platform((prevX + p.x) / 2 - 2, Math.Min(prevTop, p.top) - 1, 5, f.PlatformStyle);
				prevX = p.x + 3;
				prevTop = p.top;
			}
			if (I.Right - d.ShoreW - prevX > 6)
				Gen.Platform((prevX + I.Right - d.ShoreW) / 2 - 2, Math.Min(prevTop, shoreTop) - 1, 5, f.PlatformStyle);

			var mid = d.Pillars[d.Pillars.Count / 2];
			Point up = r.Anchors[(int)Dir.Up];
			Gen.Platform(up.X - 9, up.Y, 19, f.PlatformStyle);
			Gen.PlatformLadder(mid.x - 2, up.Y, mid.top, 7, f.PlatformStyle);

			Gen.Liquid(new Rectangle(I.X + d.ShoreW, d.WaterLine, I.Width - d.ShoreW * 2, floor - d.WaterLine), LiquidID.Water);
		}

		public override void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			var d = (Data)r.Data;
			Rectangle I = r.Interior;
			for (int y = d.WaterLine - 6; y > I.Y + 2; y -= 12) {
				Gen.Torch(I.X + 1, y, f.TorchStyle);
				Gen.Torch(I.Right - 2, y, f.TorchStyle);
			}
			Chandeliers(r, f, rng, 30);
		}
	}

	// =====================================================================================================
	// Lava Forge: a foundry floor broken by lava channels, with a walkway overhead and chains to climb.
	// =====================================================================================================
	public class LavaForgeRoom : RoomArchetype
	{
		public class Data
		{
			public List<(int x, int w, bool bridged)> Pits = new();
		}

		public override (int w, int h) Size(Random rng) => (Rand(rng, 150, 200), Rand(rng, 60, 85));

		public override void Layout(RoomPlan r, Random rng)
		{
			Rectangle I = r.Interior;
			var d = new Data();
			int x = I.X + 14;
			int cx = I.Center.X;
			while (true) {
				x += Rand(rng, 10, 18);
				int w = Rand(rng, 4, 7);
				if (x + w > I.Right - 16)
					break;
				bool nearCenter = x + w > cx - 6 && x < cx + 20;
				if (!nearCenter)
					d.Pits.Add((x, w, rng.NextDouble() < 0.5));
				x += w;
			}
			StandardAnchors(r, r.FloorY - 14);
			r.Data = d;
		}

		public override void Build(RoomPlan r, FloorInfo f, Random rng)
		{
			var d = (Data)r.Data;
			Rectangle I = r.Interior;
			int floor = r.FloorY;

			foreach (var p in d.Pits) {
				Gen.Air(new Rectangle(p.x, floor, p.w, 4));
				Gen.Liquid(new Rectangle(p.x, floor + 1, p.w, 3), LiquidID.Lava);
				if (p.bridged)
					Gen.Platform(p.x, floor, p.w, f.PlatformStyle);
			}

			Gen.Platform(I.X + 8, floor - 14, I.Width - 16, f.PlatformStyle);
			UpLedge(I.Center.X, floor - 14, floor, f.PlatformStyle);
			for (int x = I.X + 20; x < I.Right - 20; x += Rand(rng, 36, 48))
				Gen.PlatformLadder(x, floor - 14, floor, 6, f.PlatformStyle);

			for (int i = 0; i < 6; i++) {
				int x = rng.Next(I.X + 4, I.Right - 4);
				Gen.Rope(x, I.Y, I.Y + Rand(rng, 6, 18), TileID.Chain);
			}
			for (int x = I.X + 6; x < I.Right - 6; x += 18)
				Gen.Wall(new Rectangle(x, I.Y, 2, I.Height), WallID.HellstoneBrickUnsafe);
			Roughen(r, rng, f.Rock, f.Brick, f.Brick, floor: false);
		}

		public override void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			base.Decorate(r, f, rng);
			for (int i = 0; i < 3; i++)
				if (FindFloorSpot(new Rectangle(r.Interior.X, r.FloorY - 4, r.Interior.Width, 5), rng, 3, out Point p, 3))
					Gen.PlaceOnFloor(p.X + 1, p.Y, TileID.Hellforge);
			ScatterPots(r, f, rng, 3);
		}
	}

	// =====================================================================================================
	// Sky Garden: no back walls, so the real sky shows through. Floating islands over a cloud floor.
	// =====================================================================================================
	public class SkyGardenRoom : RoomArchetype
	{
		public class Data
		{
			public int Surface;
			public List<(int cx, int top, int hw)> Islands = new();
		}

		public override (int w, int h) Size(Random rng) => (Rand(rng, 170, 220), Rand(rng, 75, 100));

		public override Band PreferredBand(FloorInfo floor, Random rng) => Band.Sky;

		public override void Layout(RoomPlan r, Random rng)
		{
			Rectangle I = r.Interior;
			var d = new Data { Surface = r.FloorY - 3 };
			int k = Rand(rng, 3, 4);
			for (int i = 0; i < k; i++) {
				int cx = I.X + I.Width * (i + 1) / (k + 1) + Rand(rng, -8, 8);
				int top = Rand(rng, I.Y + 14, d.Surface - 18);
				d.Islands.Add((cx, top, Rand(rng, 8, 12)));
			}

			var high = d.Islands.OrderBy(i => i.top).First();
			int cxRoom = I.Center.X;
			r.Anchors[(int)Dir.Left] = new Point(I.X + 5, d.Surface);
			r.Anchors[(int)Dir.Right] = new Point(I.Right - 6, d.Surface);
			r.Anchors[(int)Dir.Down] = new Point(cxRoom + 14, d.Surface);
			r.Anchors[(int)Dir.Up] = new Point(high.cx, high.top);
			r.Home = new Point(cxRoom - 14, d.Surface);
			r.ExitA = new Point(cxRoom - 24, d.Surface);
			r.ExitB = new Point(cxRoom + 24, d.Surface);
			r.Feature = new Point(cxRoom, d.Surface);
			r.Data = d;
		}

		public override void Build(RoomPlan r, FloorInfo f, Random rng)
		{
			var d = (Data)r.Data;
			Rectangle I = r.Interior;
			Gen.Wall(I, 0);
			Gen.Solid(new Rectangle(I.X, d.Surface, I.Width, r.FloorY - d.Surface), TileID.Cloud);

			foreach (var isl in d.Islands) {
				int ry = (int)(isl.hw * 0.7f);
				for (int x = isl.cx - isl.hw; x <= isl.cx + isl.hw; x++) {
					float dx = (x - isl.cx) / (float)isl.hw;
					int depth = (int)(ry * Math.Sqrt(Math.Max(0, 1 - dx * dx)));
					for (int y = isl.top; y <= isl.top + Math.Max(1, depth); y++)
						Gen.Solid(x, y, y == isl.top ? TileID.Grass : TileID.Dirt);
				}

				// Cloud stepping stones up to the island's left edge, zig-zagging every 4 tiles.
				int edge = isl.cx - isl.hw;
				bool outer = true;
				for (int y = d.Surface - 4; y > isl.top + 2; y -= 4) {
					int sx = outer ? edge - 8 : edge - 3;
					Gen.Solid(new Rectangle(sx, y, 4, 1), TileID.Cloud);
					outer = !outer;
				}

				// A bounce pad beneath, and a rain cloud above.
				Gen.Solid(new Rectangle(isl.cx - 1, d.Surface, 3, 1), TileID.PinkSlimeBlock);
				if (isl.top - 12 > I.Y + 2)
					Gen.Solid(new Rectangle(isl.cx - 4, isl.top - 12, 8, 2), TileID.RainCloud);
			}
		}

		public override void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			var d = (Data)r.Data;
			foreach (var isl in d.Islands) {
				Gen.Torch(isl.cx - isl.hw + 2, isl.top - 1, f.TorchStyle);
				Gen.Torch(isl.cx + isl.hw - 2, isl.top - 1, f.TorchStyle);
				if (rng.NextDouble() < 0.6)
					WorldGen.GrowTree(isl.cx + 3, isl.top);
			}
			foreach (Point a in r.Anchors)
				Gen.Torch(a.X + 3, a.Y - 1, f.TorchStyle);
		}
	}

	// =====================================================================================================
	// Trap Gauntlet: a long corridor of spike pits and trap posts wired to pressure plates.
	// =====================================================================================================
	public class TrapRoom : RoomArchetype
	{
		public class Data
		{
			public List<(int x, bool faceRight)> Posts = new();
			public List<(int x, int w)> Pits = new();
		}

		public override (int w, int h) Size(Random rng) => (Rand(rng, 170, 230), Rand(rng, 30, 40));

		public override void Layout(RoomPlan r, Random rng)
		{
			Rectangle I = r.Interior;
			var d = new Data();
			int cx = I.Center.X;
			int x = I.X + 18;
			while (x < I.Right - 18) {
				if (x > cx - 22 && x < cx + 22) {
					x = cx + 22;
					continue;
				}
				if (rng.NextDouble() < 0.55) {
					d.Posts.Add((x, rng.Next(2) == 0));
					x += 2;
				}
				else {
					int w = Rand(rng, 3, 5);
					d.Pits.Add((x, w));
					x += w;
				}
				x += Rand(rng, 12, 20);
			}
			StandardAnchors(r, r.FloorY - 12);
			r.Data = d;
		}

		public override void Build(RoomPlan r, FloorInfo f, Random rng)
		{
			var d = (Data)r.Data;
			Rectangle I = r.Interior;
			int floor = r.FloorY;
			ushort spikes = f.Brick == TileID.LihzahrdBrick ? TileID.WoodenSpikes : TileID.Spikes;

			Gen.Platform(I.Center.X - 26, floor - 12, 53, f.PlatformStyle);
			UpLedge(I.Center.X, floor - 12, floor, f.PlatformStyle);

			foreach (var p in d.Pits) {
				Gen.Air(new Rectangle(p.x, floor, p.w, 3));
				Gen.Solid(new Rectangle(p.x, floor + 2, p.w, 1), spikes);
			}

			foreach (var post in d.Posts) {
				Gen.Solid(new Rectangle(post.x, floor - 3, 2, 3), f.Brick);
				int trapX = post.faceRight ? post.x + 1 : post.x;
				int plateX = post.faceRight ? post.x + 8 : post.x - 7;
				if (Gen.IsSolid(plateX, floor) && Gen.IsAir(plateX, floor - 1))
					Traps.Armed(plateX, floor, trapX, floor - 3, post.faceRight, f.TrapStyle);
			}
		}

		public override void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			base.Decorate(r, f, rng);
			ScatterPots(r, f, rng, 3);
		}
	}

	// =====================================================================================================
	// Workshop: conveyor belts, bounce pads, tiers of catwalks and grinding cogs.
	// =====================================================================================================
	public class WorkshopRoom : RoomArchetype
	{
		public override (int w, int h) Size(Random rng) => (Rand(rng, 140, 180), Rand(rng, 60, 75));

		public override void Layout(RoomPlan r, Random rng) => StandardAnchors(r, r.FloorY - 26);

		public override void Build(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			int floor = r.FloorY;
			int cx = I.Center.X;
			int ps = f.PlatformStyle;

			UpLedge(cx, floor - 26, floor, ps);
			Gen.Platform(I.X, floor - 13, 34, ps);
			Gen.Platform(I.Right - 34, floor - 13, 34, ps);
			Gen.PlatformLadder(I.X + 28, floor - 13, floor, 6, ps);
			Gen.PlatformLadder(I.Right - 34, floor - 13, floor, 6, ps);

			// Conveyor strips, kept clear of the rifts in the middle and at the ends.
			for (int x = I.X + 12; x < I.Right - 24; x += Rand(rng, 20, 30)) {
				int len = Rand(rng, 8, 14);
				if (x + len > cx - 30 && x < cx + 30)
					continue;
				ushort belt = rng.Next(2) == 0 ? TileID.ConveyorBeltLeft : TileID.ConveyorBeltRight;
				Gen.Solid(new Rectangle(x, floor, len, 1), belt);
			}

			for (int i = 0; i < 3; i++) {
				int x = rng.Next(I.X + 14, I.Right - 16);
				if (Math.Abs(x - cx) > 30)
					Gen.Solid(new Rectangle(x, floor, 3, 1), TileID.PinkSlimeBlock);
			}

			for (int i = 0; i < 4; i++)
				Gen.Solid(new Rectangle(rng.Next(I.X + 4, I.Right - 8), I.Y, 4, Rand(rng, 2, 4)), TileID.Cog);
			for (int i = 0; i < 5; i++) {
				int x = rng.Next(I.X + 4, I.Right - 4);
				Gen.Rope(x, I.Y, I.Y + Rand(rng, 8, 20), TileID.Chain);
			}
			Roughen(r, rng, f.Brick, f.Brick, f.Brick, floor: false);
		}

		public override void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			base.Decorate(r, f, rng);
			Gen.PlaceOnFloor(r.Interior.X + 8, r.FloorY - 13, TileID.Sawmill);
			Gen.PlaceOnFloor(r.Interior.Right - 10, r.FloorY - 13, TileID.Anvils);
			ScatterPots(r, f, rng, 3);
		}
	}

	// =====================================================================================================
	// Vault (treasure): a gilded dead end with a big chest. On deeper floors the chest may bite back.
	// =====================================================================================================
	public class VaultRoom : RoomArchetype
	{
		public override (int w, int h) Size(Random rng) => (Rand(rng, 64, 74), Rand(rng, 34, 40));

		public override void Layout(RoomPlan r, Random rng) => StandardAnchors(r, r.FloorY - 12);

		public override void Build(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			Gen.Solid(new Rectangle(I.X - 1, r.FloorY, I.Width + 2, 2), TileID.GoldBrick);
			Gen.Wall(I, WallID.GoldBrick);
			UpLedge(I.Center.X, r.FloorY - 12, r.FloorY, f.PlatformStyle);
		}

		public override void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			base.Decorate(r, f, rng);
			Rectangle I = r.Interior;
			for (int x = I.X + 3; x < I.Right - 3; x++)
				if (rng.NextDouble() < 0.35 && Math.Abs(x - I.Center.X) > 4 && Gen.IsAir(x, r.FloorY - 1))
					Gen.PlaceOnFloor(x, r.FloorY, rng.Next(3) == 0 ? TileID.GoldCoinPile : TileID.SilverCoinPile);
			Gen.Place(I.Center.X, I.Y, TileID.Chandeliers, f.ChandelierStyle);
		}
	}

	// =====================================================================================================
	// Shrine: an altar that offers a bargain. Each kind of shrine dresses its chamber in its own materials: a
	// checkered casino floor for the Gambler, gold for Fortune, a marble spring with a pool, and so on.
	// =====================================================================================================
	public class ShrineRoom : RoomArchetype
	{
		public override (int w, int h) Size(Random rng) => (Rand(rng, 70, 80), Rand(rng, 40, 44));

		public override void Layout(RoomPlan r, Random rng)
		{
			StandardAnchors(r, r.FloorY - 14);
			r.Feature = new Point(r.Interior.Center.X - 22, r.FloorY);
		}

		public override void Build(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			ShrineType type = Shrines.TypeOf(r);
			ShrineTheme th = Shrines.Theme(type);

			// A themed floor (three tiles deep), framed by the theme's trim on the ceiling and both side walls.
			for (int x = I.X - 2; x <= I.Right + 1; x++) {
				for (int y = r.FloorY; y < r.FloorY + 3; y++)
					Gen.Solid(x, y, th.Floor != th.Floor2 && (x + y) % 2 == 0 ? th.Floor2 : th.Floor);
				Gen.Solid(x, I.Y - 1, th.Trim);
				Gen.Solid(x, I.Y - 2, th.Trim);
			}
			for (int y = I.Y - 2; y < r.FloorY; y++)
				foreach (int x in new[] { I.X - 2, I.X - 1, I.Right, I.Right + 1 })
					Gen.Solid(x, y, th.Trim);

			// Back wall in the theme's material, broken up by contrasting panels, and a bright band behind the altar.
			Gen.Wall(I, th.Wall);
			for (int x = I.X + 3; x < I.Right - 3; x += 9)
				Gen.Wall(new Rectangle(x, I.Y, 2, I.Height), th.Wall2);
			Gen.Wall(new Rectangle(r.Feature.X - 6, I.Y, 13, I.Height), th.Wall2);

			UpLedge(I.Center.X, r.FloorY - 14, r.FloorY, th.Platform);

			// The altar's dais, with half-block steps at either end.
			Gen.Solid(new Rectangle(r.Feature.X - 5, r.FloorY - 1, 11, 1), th.Dais);
			foreach (int x in new[] { r.Feature.X - 5, r.Feature.X + 5 }) {
				Tile t = Main.tile[x, r.FloorY - 1];
				t.IsHalfBlock = true;
			}

			if (type == ShrineType.Spring) {
				// A shallow pool set into the floor beside the altar.
				Rectangle pool = new(r.Feature.X + 8, r.FloorY, 7, 2);
				Gen.Air(pool);
				Gen.Liquid(pool, LiquidID.Water);
			}
			else if (type == ShrineType.Wayward) {
				// Drifting clouds under the ceiling.
				for (int i = 0; i < 4; i++)
					Gen.Ellipse(Rand(rng, I.X + 8, I.Right - 8), I.Y + Rand(rng, 3, 6), Rand(rng, 4, 7), 2, true, TileID.Cloud, rng, 0.25f, I);
			}
		}

		public override void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			ShrineType type = Shrines.TypeOf(r);
			ShrineTheme th = Shrines.Theme(type);
			int fx = r.Feature.X;

			for (int y = I.Bottom - 5; y > I.Y + 2; y -= 10) {
				Gen.Torch(I.X + 1, y, th.Torch);
				Gen.Torch(I.Right - 2, y, th.Torch);
			}
			Gen.Place(fx - 5, I.Y, TileID.Banners, th.Banner);
			Gen.Place(fx + 5, I.Y, TileID.Banners, th.Banner);
			Chandeliers(r, f, rng, 34);

			switch (type) {
				case ShrineType.Blood:
					Gen.PlaceOnFloor(fx - 8, r.FloorY, TileID.Candelabras);
					Gen.PlaceOnFloor(fx + 8, r.FloorY, TileID.Candelabras);
					break;
				case ShrineType.Fortune:
				case ShrineType.Gamble:
					for (int x = I.X + 3; x < I.Right - 3; x++)
						if (Math.Abs(x - fx) > 6 && Math.Abs(x - I.Center.X) > 5 && Math.Abs(x - (I.Center.X + 14)) > 3
							&& Math.Abs(x - (I.X + 5)) > 3 && Math.Abs(x - (I.Right - 6)) > 3 && rng.NextDouble() < 0.3 && Gen.IsAir(x, r.FloorY - 1))
							Gen.PlaceOnFloor(x, r.FloorY, rng.Next(4) switch { 0 => TileID.GoldCoinPile, 1 => TileID.PlatinumCoinPile, _ => TileID.SilverCoinPile });
					break;
				case ShrineType.Change:
					Gen.PlaceOnFloor(fx - 8, r.FloorY, TileID.CrystalBall);
					Gen.PlaceOnFloor(fx + 9, r.FloorY, TileID.CrystalBall);
					break;
				case ShrineType.Wayward:
					Gen.PlaceOnFloor(fx + 9, r.FloorY, TileID.SkyMill);
					break;
				case ShrineType.Trial:
					Gen.PlaceOnFloor(fx - 9, r.FloorY, TileID.Hellforge);
					Gen.PlaceOnFloor(fx + 9, r.FloorY, TileID.Campfire);
					break;
				case ShrineType.Spring:
					Gen.PlaceOnFloor(fx - 8, r.FloorY, TileID.WaterFountain);
					break;
				case ShrineType.Refinement:
					Gen.PlaceOnFloor(fx - 9, r.FloorY, TileID.MythrilAnvil);
					Gen.PlaceOnFloor(fx + 9, r.FloorY, TileID.Hellforge);
					break;
			}
		}
	}

	// =====================================================================================================
	// Merchant: a quiet campsite where a wandering peddler sells supplies.
	// =====================================================================================================
	public class MerchantRoom : RoomArchetype
	{
		public override (int w, int h) Size(Random rng) => (Rand(rng, 76, 86), Rand(rng, 36, 40));

		public override void Layout(RoomPlan r, Random rng)
		{
			StandardAnchors(r, r.FloorY - 12);
			r.Feature = new Point(r.Interior.Center.X - 22, r.FloorY);
		}

		/// <summary>A trader's camp: plank floor and walls, a striped awning over the stall, crates and goods.</summary>
		public override void Build(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			for (int x = I.X - 2; x <= I.Right + 1; x++) {
				Gen.Solid(x, r.FloorY, TileID.WoodBlock);
				Gen.Solid(x, I.Y - 1, TileID.RichMahogany);
			}
			Gen.Wall(I, WallID.Planked);
			for (int x = I.X + 4; x < I.Right - 4; x += 10)
				Gen.Wall(new Rectangle(x, I.Y, 2, I.Height), WallID.Wood);
			// The stall: an awning of alternating red and white over the trader's spot.
			int fx = r.Feature.X;
			for (int x = fx - 9; x <= fx + 9; x++) {
				Gen.Solid(x, r.FloorY - 9, (x / 2) % 2 == 0 ? TileID.RedDynastyShingles : TileID.Cloud);
				if (x == fx - 9 || x == fx + 9)
					Gen.Wall(new Rectangle(x, r.FloorY - 8, 1, 8), WallID.RichMaogany);
			}
			UpLedge(I.Center.X, r.FloorY - 12, r.FloorY, 2);
		}

		public override void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			base.Decorate(r, f, rng);
			int fx = r.Feature.X, fy = r.FloorY;
			// The counter and goods under the awning, the trader stands at the middle.
			Gen.PlaceOnFloor(fx - 5, fy, TileID.Tables);
			Gen.Place(fx - 5, fy - 3, TileID.Bottles, rng.Next(3));
			Gen.Place(fx - 4, fy - 3, TileID.Bottles, rng.Next(3));
			Gen.PlaceOnFloor(fx + 5, fy, TileID.Tables);
			Gen.Place(fx + 5, fy - 3, TileID.Candles);
			// Crates and a campfire around the camp.
			foreach (int x in new[] { fx - 12, fx - 14, fx + 12, fx + 14 })
				Gen.PlaceOnFloor(x, fy, TileID.FishingCrate, 0);
			Gen.PlaceOnFloor(fx + 20, fy, TileID.Campfire);
			Gen.PlaceOnFloor(fx + 24, fy, TileID.Chairs);
			for (int x = fx - 8; x <= fx + 8; x += 8)
				Gen.Place(x, fy - 8, TileID.HangingLanterns);
		}
	}

	// =====================================================================================================
	// Hellbridge: the floor-3 guardian's arena. A bridge hundreds of tiles long over a lava lake, open to hell's
	// sky, so the Wall of Flesh has room to advance. Rifts sit near the start; exit rifts appear along its length.
	// =====================================================================================================
	public class HellBridgeRoom : RoomArchetype
	{
		public override (int w, int h) Size(Random rng) => (Math.Min(860, Main.maxTilesX - 700), 64);

		public override Band PreferredBand(FloorInfo floor, Random rng) => Band.Underworld;

		public static int BridgeY(RoomPlan r) => r.Interior.Bottom - 16;

		public override void Layout(RoomPlan r, Random rng)
		{
			Rectangle I = r.Interior;
			int b = BridgeY(r);
			r.Anchors[(int)Dir.Left] = new Point(I.X + 34, b);
			r.Anchors[(int)Dir.Right] = new Point(I.X + 46, b);
			r.Anchors[(int)Dir.Up] = new Point(I.X + 58, b);
			r.Anchors[(int)Dir.Down] = new Point(I.X + 70, b);
			r.Home = new Point(I.X + 82, b);
			r.ExitA = new Point(I.X + 96, b);
			r.ExitB = new Point(I.X + 104, b);
			r.Feature = new Point(I.X + 4, b);
			for (int x = I.X + 240; x < I.Right - 30; x += 160)
				r.ExtraExits.Add(new Point(x, b));
		}

		public override void Build(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			int b = BridgeY(r);
			Gen.Wall(I, 0); // hell's own background shows through

			for (int x = I.X; x < I.Right; x += Rand(rng, 6, 14))
				Gen.Ellipse(x, I.Y, Rand(rng, 3, 8), Rand(rng, 2, 6), true, TileID.Ash, rng, 0.2f, I);

			Gen.Liquid(new Rectangle(I.X, I.Bottom - 6, I.Width, 6), LiquidID.Lava);
			for (int x = I.X + 20; x < I.Right - 4; x += 40)
				Gen.Solid(new Rectangle(x, b + 2, 3, I.Bottom - 6 - (b + 2)), TileID.HellstoneBrick);
			Gen.Solid(new Rectangle(I.X, b, I.Width, 2), TileID.ObsidianBrick);
		}

		public override void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			int b = BridgeY(r);
			for (int x = r.Interior.X + 10; x < r.Interior.Right - 10; x += 24)
				Gen.Torch(x, b - 1, f.TorchStyle);
		}
	}

	// =====================================================================================================
	// Forge: a quiet sanctum holding a Runeforge, so runes can be inscribed mid-descent.
	// =====================================================================================================
	public class ForgeRoom : RoomArchetype
	{
		public override (int w, int h) Size(Random rng) => (Rand(rng, 70, 80), Rand(rng, 40, 44));

		public override void Layout(RoomPlan r, Random rng)
		{
			StandardAnchors(r, r.FloorY - 14);
			r.Feature = new Point(r.Interior.Center.X - 24, r.FloorY);
		}

		public override void Build(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			UpLedge(I.Center.X, r.FloorY - 14, r.FloorY, f.PlatformStyle);
			Gen.Solid(new Rectangle(r.Feature.X - 5, r.FloorY, 11, 1), f.Accent);
			Gen.Wall(new Rectangle(r.Feature.X - 7, I.Y, 15, I.Height), WallID.AmethystGemspark);
		}

		public override void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			base.Decorate(r, f, rng);
			Gen.Place(r.Feature.X - 6, r.Interior.Y, TileID.Banners, 2);
			Gen.Place(r.Feature.X + 6, r.Interior.Y, TileID.Banners, 2);
		}
	}

	/// <summary>Wiring helpers for trap posts.</summary>
	public static class Traps
	{
		/// <summary>
		/// A pressure plate resting on <paramref name="plateFloorY"/> wired to a trap at (<paramref name="trapX"/>,
		/// <paramref name="trapY"/>). The wire runs down through the floor, along it, and up to the trap.
		/// </summary>
		public static void Armed(int plateX, int plateFloorY, int trapX, int trapY, bool faceRight, int style)
		{
			Tile trap = Main.tile[trapX, trapY];
			trap.HasTile = true;
			trap.TileType = TileID.Traps;
			trap.TileFrameY = (short)(style * 18);
			trap.TileFrameX = (short)(faceRight ? 18 : 0);
			trap.Slope = SlopeType.Solid;
			trap.IsHalfBlock = false;

			Tile plate = Main.tile[plateX, plateFloorY - 1];
			plate.HasTile = true;
			plate.TileType = TileID.PressurePlates;
			plate.TileFrameX = 0;
			plate.TileFrameY = 0;

			Wire(plateX, plateFloorY - 1);
			Wire(plateX, plateFloorY);
			int step = Math.Sign(trapX - plateX);
			for (int x = plateX; x != trapX; x += step)
				Wire(x, plateFloorY);
			int ystep = Math.Sign(trapY - plateFloorY);
			for (int y = plateFloorY; y != trapY + ystep; y += ystep == 0 ? 1 : ystep) {
				Wire(trapX, y);
				if (ystep == 0)
					break;
			}
		}

		private static void Wire(int x, int y)
		{
			if (!Gen.InWorld(x, y))
				return;
			Tile t = Main.tile[x, y];
			t.RedWire = true;
		}
	}
}
