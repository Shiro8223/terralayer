using System;
using Microsoft.Xna.Framework;
using Terralayer.Common.World;
using Terraria;
using Terraria.ID;

namespace Terralayer.Common.Depths.Rooms
{
	/// <summary>
	/// A kind of room. <see cref="Layout"/> is pure (no tiles) and runs on every client to work out where the rifts go;
	/// <see cref="Build"/> carves the tiles on the server. Before Build runs, the room's bounds are solid brick with brick
	/// walls and the interior has been hollowed out.
	/// </summary>
	public abstract class RoomArchetype
	{
		private static readonly RoomArchetype[] All = {
			new StartRoom(), new HallRoom(), new CavernRoom(), new ShaftRoom(), new FloodedRoom(), new LavaForgeRoom(),
			new SkyGardenRoom(), new TrapRoom(), new WorkshopRoom(), new VaultRoom(), new ShrineRoom(), new MerchantRoom(), new ArenaRoom(),
			new HellBridgeRoom(), new ForgeRoom(),
			new HopliteBarracksRoom(), new GraniteCaveRoom(), new OssuaryRoom(), new LibraryRoom(), new HiveRoom(), new SpiderNestRoom(),
			new ImpDenRoom(), new UnicornMeadowRoom(), new LihzahrdSanctumRoom(), new SlimePitRoom(),
			new MushroomCaveRoom(), new IceCaveRoom(), new DesertCaveRoom(), new CrystalCaveRoom(), new MarbleCaveRoom(), new GraniteGrottoRoom(),
		};

		public static RoomArchetype For(Archetype a) => All[(int)a];

		public abstract (int w, int h) Size(Random rng);

		public virtual Band PreferredBand(FloorInfo floor, Random rng) => floor.Band;

		/// <summary>Themed rooms bring their own monsters; null means the floor's usual mix.</summary>
		public virtual int[] Enemies(int floor) => null;

		public abstract void Layout(RoomPlan r, Random rng);

		public abstract void Build(RoomPlan r, FloorInfo f, Random rng);

		/// <summary>Runs after framing: torches and other things that need neighbours to attach to.</summary>
		public virtual void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			WallTorches(r, f);
		}

		// ------------------------------------------------------------------ shared helpers

		protected static int Rand(Random rng, int min, int max) => rng.Next(min, max + 1);

		/// <summary>Anchors for rooms with a flat floor across the whole width and an upper ledge in the middle.</summary>
		protected static void StandardAnchors(RoomPlan r, int upY)
		{
			Rectangle I = r.Interior;
			int f = I.Bottom;
			int cx = I.Center.X;
			r.Anchors[(int)Dir.Left] = new Point(I.X + 5, f);
			r.Anchors[(int)Dir.Right] = new Point(I.Right - 6, f);
			r.Anchors[(int)Dir.Down] = new Point(cx + 14, f);
			r.Anchors[(int)Dir.Up] = new Point(cx, upY);
			r.Home = new Point(cx - 14, f);
			r.ExitA = new Point(cx - 24, f);
			r.ExitB = new Point(cx + 24, f);
			r.Feature = new Point(cx, f);
		}

		/// <summary>The central ledge for the "up" rift, with a jump-through platform ladder under it.</summary>
		protected static void UpLedge(int cx, int ledgeY, int floorY, int style)
		{
			Gen.Platform(cx - 9, ledgeY, 19, style);
			Gen.PlatformLadder(cx - 3, ledgeY, floorY, 7, style);
		}

		protected static void WallTorches(RoomPlan r, FloorInfo f, int spacing = 12)
		{
			Rectangle I = r.Interior;
			for (int y = I.Bottom - 5; y > I.Y + 2; y -= spacing) {
				Gen.Torch(I.X + 1, y, f.TorchStyle);
				Gen.Torch(I.Right - 2, y, f.TorchStyle);
			}
			for (int x = I.X + 10; x < I.Right - 10; x += 18)
				Gen.Torch(x, I.Y + 2, f.TorchStyle);
		}

		/// <summary>Is <paramref name="x"/> clear of every rift spot on the floor and of the central ledge's ladder?</summary>
		protected static bool ClearOfRifts(RoomPlan r, int x, int margin = 4)
		{
			if (Math.Abs(x - r.Interior.Center.X) <= 10)
				return false;
			foreach (Point a in r.Anchors)
				if (Math.Abs(x - a.X) <= margin)
					return false;
			return true;
		}

		/// <summary>
		/// Breaks up a boxy room: stalactites along the ceiling, bulges in the walls (between balcony tiers) and low
		/// humps on the floor (one or two tiles, away from the rifts). All small enough to walk or hop over.
		/// </summary>
		protected static void Roughen(RoomPlan r, Random rng, ushort rock, ushort floorTop, ushort floorFill, bool floor = true, bool walls = true)
		{
			Rectangle I = r.Interior;
			int fy = r.FloorY;
			var ceiling = new Rectangle(I.X, I.Y, I.Width, 7);
			for (int x = I.X + 2; x < I.Right - 2; x += Rand(rng, 4, 9))
				Gen.Ellipse(x, I.Y, Rand(rng, 2, 6), Rand(rng, 2, 6), true, rock, rng, 0.35f, ceiling);
			if (walls) {
				foreach (int side in new[] { I.X, I.Right - 1 })
					for (int y = I.Y + 7; y < fy - 8; y += Rand(rng, 5, 9)) {
						int fromFloor = (fy - y) % 12;
						if (fromFloor <= 3 || fromFloor >= 9)
							continue; // keep balcony tiers clear
						Gen.Ellipse(side, y, Rand(rng, 2, 4), Rand(rng, 2, 4), true, rock, rng, 0.35f, new Rectangle(I.X, I.Y, I.Width, fy - 8 - I.Y));
					}
			}
			if (floor) {
				for (int x = I.X + 8; x < I.Right - 12; x += Rand(rng, 7, 14)) {
					int w = Rand(rng, 3, 7);
					bool clear = true;
					for (int i = -1; i <= w; i++)
						if (!ClearOfRifts(r, x + i, 5) || !Gen.IsAir(x + i, fy - 1) || !Gen.IsAir(x + i, fy - 2))
							clear = false;
					if (!clear)
						continue;
					int h = rng.Next(3) == 0 ? 2 : 1;
					for (int i = 0; i < w; i++) {
						int hh = h == 2 && i > 0 && i < w - 1 ? 2 : 1;
						for (int k = 1; k <= hh; k++)
							Gen.Solid(x + i, fy - k, k == hh ? floorTop : floorFill);
					}
					Gen.Smooth(new Rectangle(x - 1, fy - 3, w + 2, 4));
				}
			}
			Gen.Smooth(new Rectangle(I.X, I.Y, I.Width, 8));
		}

		/// <summary>A little loose rock on the floor (in place of the old random statues).</summary>
		protected static void Rubble(int x, int floorY, Random rng)
		{
			if (Gen.IsAir(x, floorY - 1) && Gen.IsSolid(x, floorY))
				WorldGen.PlaceSmallPile(x, floorY - 1, rng.Next(6), 0);
		}

		protected static void Chandeliers(RoomPlan r, FloorInfo f, Random rng, int spacing = 26)
		{
			Rectangle I = r.Interior;
			for (int x = I.X + 12 + rng.Next(6); x < I.Right - 12; x += spacing)
				Gen.Place(x, I.Y, TileID.Chandeliers, f.ChandelierStyle);
		}

		/// <summary>Finds a spot where something <paramref name="width"/> wide can rest on solid ground with headroom.</summary>
		public static bool FindFloorSpot(Rectangle area, Random rng, int width, out Point spot, int headroom = 3, bool allowPlatforms = false)
		{
			for (int attempt = 0; attempt < 80; attempt++) {
				int x = rng.Next(area.X + 2, area.Right - width - 2);
				for (int y = rng.Next(area.Y + headroom, area.Bottom); y <= area.Bottom; y++) {
					bool ok = true;
					for (int i = 0; i < width && ok; i++) {
						bool ground = allowPlatforms ? Gen.IsStandable(x + i, y) : Gen.IsSolid(x + i, y);
						if (!ground || Main.tile[x + i, y].TileType == TileID.Spikes)
							ok = false;
						for (int h = 1; h <= headroom && ok; h++)
							if (!Gen.IsAir(x + i, y - h) || Main.tile[x + i, y - h].LiquidAmount > 0)
								ok = false;
					}
					if (ok) {
						spot = new Point(x, y);
						return true;
					}
					if (!Gen.IsAir(x, y) && !Gen.IsStandable(x, y))
						break;
				}
			}
			spot = Point.Zero;
			return false;
		}

		protected static void ScatterPots(RoomPlan r, FloorInfo f, Random rng, int count)
		{
			for (int i = 0; i < count; i++) {
				if (FindFloorSpot(r.Interior, rng, 2, out Point p, 2))
					WorldGen.PlacePot(p.X, p.Y - 1, TileID.Pots, rng.Next(3) + 3 * f.PotBiome);
			}
		}
	}
}
