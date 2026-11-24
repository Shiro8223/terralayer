using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terralayer.Common.Depths.Rooms;
using Terralayer.Common.World;
using Terraria;

namespace Terralayer.Common.Depths
{
	public enum RoomKind
	{
		Start, Combat, Ambush, Treasure, Shrine, Merchant, Boss, Forge
	}

	public enum Dir
	{
		Left, Right, Up, Down
	}

	public enum RiftKind
	{
		Link,       // to another room on this floor
		Retreat,    // start room: abandon the descent and go home
		Descend,    // guardian room, after victory: next floor
		ReturnHome  // guardian room, after victory: bank everything and go home
	}

	public class RoomPlan
	{
		public int Id;
		public Point Grid;
		public RoomKind Kind;
		public Archetype Arch;
		public Band Band;
		public int Seed;
		public Rectangle Interior;
		public Rectangle Bounds;
		public readonly int[] Links = { -1, -1, -1, -1 };
		public readonly Point[] Anchors = new Point[4];
		public Point Home, ExitA, ExitB, Feature;
		/// <summary>Extra Return/Descend rift pairs, for arenas too long to walk back across (the hellbridge).</summary>
		public List<Point> ExtraExits = new();
		/// <summary>Boss rooms: the guardian (NPC type) this arena is built for.</summary>
		public int Guardian;
		public object Data;

		public int FloorY => Interior.Bottom;
		public int Area => Interior.Width * Interior.Height;
		public int LinkCount => Links.Count(l => l >= 0);
	}

	public class RiftPlan
	{
		public int Id;
		public int Room;
		public Point Pos;   // bottom-center tile the rift stands on
		public RiftKind Kind;
		public int Target = -1;
		public Dir Side;

		/// <summary>The 3x4 tile area you stand in to use the rift, in world pixels.</summary>
		public Rectangle Hitbox => new((Pos.X - 1) * 16, (Pos.Y - 4) * 16, 48, 64);
	}

	/// <summary>
	/// The layout of one floor: a grid-embedded maze of rooms (for the map), each carved into its own spot somewhere in
	/// the world and linked to its neighbours by rifts. Deterministic from (seed, floor), so clients rebuild it locally.
	/// </summary>
	public class FloorPlan
	{
		public const int Shell = 10;
		private const int GapX = 56;
		private const int GapY = 36;

		public int Seed;
		public int Floor;
		public List<RoomPlan> Rooms = new();
		public List<RiftPlan> Rifts = new();
		public int StartRoom;
		public int BossRoom;
		/// <summary>Which of the floor's guardians waits in the arena (fixed per layout, so the arena can match it).</summary>
		public int GuardianPick;
		public Dictionary<Point, int> Grid = new();

		public FloorInfo Info => FloorDefs.Get(Floor);

		public static Dir Opposite(Dir d) => d switch { Dir.Left => Dir.Right, Dir.Right => Dir.Left, Dir.Up => Dir.Down, _ => Dir.Up };

		public static Point Step(Dir d) => d switch { Dir.Left => new(-1, 0), Dir.Right => new(1, 0), Dir.Up => new(0, -1), _ => new(0, 1) };

		public RoomPlan RoomAt(Point tile)
		{
			foreach (RoomPlan r in Rooms)
				if (r.Bounds.Contains(tile))
					return r;
			return null;
		}

		public static FloorPlan Generate(int seed, int floor)
		{
			var plan = new FloorPlan { Seed = seed, Floor = floor };
			var rng = new Random(seed * 31 + floor);
			FloorInfo info = FloorDefs.Get(floor);

			plan.BuildGraph(rng, info);
			plan.AssignKinds(rng, floor);
			// A guardian you've already beaten sits out until you've beaten every guardian of this floor.
			var slain = World.TerralayerWorld.Instance.BossesSlain;
			var pool = Enumerable.Range(0, info.Guardians.Length).Where(i => !slain.Contains(info.Guardians[i][0])).ToList();
			if (pool.Count == 0)
				pool = Enumerable.Range(0, info.Guardians.Length).ToList();
			plan.GuardianPick = pool[new Random(seed * 13 + floor * 101).Next(pool.Count)];
			plan.Rooms[plan.BossRoom].Guardian = info.Guardians[plan.GuardianPick][0];
			plan.PlaceInWorld(rng, info);
			foreach (RoomPlan r in plan.Rooms)
				RoomArchetype.For(r.Arch).Layout(r, new Random(r.Seed));
			plan.CreateRifts();
			return plan;
		}

		// ------------------------------------------------------------------ graph

		private RoomPlan AddRoom(Point grid)
		{
			var r = new RoomPlan { Id = Rooms.Count, Grid = grid };
			Rooms.Add(r);
			Grid[grid] = r.Id;
			return r;
		}

		private void Link(RoomPlan a, RoomPlan b)
		{
			Point d = new(b.Grid.X - a.Grid.X, b.Grid.Y - a.Grid.Y);
			Dir dir = d.X == 1 ? Dir.Right : d.X == -1 ? Dir.Left : d.Y == 1 ? Dir.Down : Dir.Up;
			a.Links[(int)dir] = b.Id;
			b.Links[(int)Opposite(dir)] = a.Id;
		}

		private void BuildGraph(Random rng, FloorInfo info)
		{
			int target = rng.Next(info.RoomsMin, info.RoomsMax + 1);
			int mainLength = Math.Clamp(target / 2 + 1, 5, 9);

			RoomPlan start = AddRoom(Point.Zero);
			StartRoom = start.Id;
			var path = new List<RoomPlan> { start };

			// Main path: a wandering walk that mostly heads east, ending at the guardian.
			RoomPlan cur = start;
			for (int i = 1; i < mainLength; i++) {
				var options = new List<(Dir dir, int weight)>();
				foreach (Dir d in new[] { Dir.Right, Dir.Up, Dir.Down }) {
					Point g = cur.Grid + Step(d);
					if (!Grid.ContainsKey(g))
						options.Add((d, d == Dir.Right ? 3 : 1));
				}
				if (options.Count == 0)
					break;
				Dir pick = Weighted(options, rng);
				RoomPlan next = AddRoom(cur.Grid + Step(pick));
				Link(cur, next);
				path.Add(next);
				cur = next;
			}
			BossRoom = cur.Id;
			Point bossGrid = cur.Grid;

			// Side branches hanging off the main path and each other (never touching the guardian).
			int attempts = 0;
			while (Rooms.Count < target && attempts++ < 600) {
				RoomPlan from = Rooms[rng.Next(Rooms.Count)];
				if (from.Id == BossRoom)
					continue;
				Dir d = (Dir)rng.Next(4);
				Point g = from.Grid + Step(d);
				if (Grid.ContainsKey(g) || Adjacent(g, bossGrid))
					continue;
				Link(from, AddRoom(g));
			}

			// A few loops so the maze isn't a pure tree.
			foreach (RoomPlan a in Rooms) {
				if (a.Id == BossRoom)
					continue;
				foreach (Dir d in new[] { Dir.Right, Dir.Down }) {
					if (a.Links[(int)d] >= 0)
						continue;
					if (Grid.TryGetValue(a.Grid + Step(d), out int bId) && bId != BossRoom && rng.NextDouble() < 0.12)
						Link(a, Rooms[bId]);
				}
			}
		}

		private static bool Adjacent(Point a, Point b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;

		private static T Weighted<T>(List<(T item, int weight)> options, Random rng)
		{
			int total = options.Sum(o => o.weight);
			int roll = rng.Next(total);
			foreach (var o in options) {
				roll -= o.weight;
				if (roll < 0)
					return o.item;
			}
			return options[0].item;
		}

		private void AssignKinds(Random rng, int floor)
		{
			foreach (RoomPlan r in Rooms)
				r.Kind = RoomKind.Combat;
			Rooms[StartRoom].Kind = RoomKind.Start;
			Rooms[BossRoom].Kind = RoomKind.Boss;

			// Dead ends become special rooms, furthest from the start first.
			var leaves = Rooms.Where(r => r.Kind == RoomKind.Combat && r.LinkCount == 1)
				.OrderByDescending(r => Math.Abs(r.Grid.X) + Math.Abs(r.Grid.Y)).ToList();
			var specials = new List<RoomKind> { RoomKind.Treasure };
			if (rng.NextDouble() < 0.6) specials.Add(RoomKind.Forge);
			if (rng.NextDouble() < 0.7) specials.Add(RoomKind.Shrine);
			if (floor >= 2 && rng.NextDouble() < 0.75) specials.Add(RoomKind.Merchant);
			if (rng.NextDouble() < 0.5) specials.Add(RoomKind.Treasure);
			for (int i = 0; i < specials.Count && i < leaves.Count; i++)
				leaves[i].Kind = specials[i];

			var combat = Rooms.Where(r => r.Kind == RoomKind.Combat && !Adjacent(r.Grid, Rooms[StartRoom].Grid)).ToList();
			// One ambush (and so one champion) a floor; from floor 3 sometimes a second.
			int ambushes = Math.Min(combat.Count, 1 + (floor >= 3 && rng.NextDouble() < 0.35 ? 1 : 0));
			for (int i = 0; i < ambushes; i++) {
				RoomPlan pick = combat[rng.Next(combat.Count)];
				pick.Kind = RoomKind.Ambush;
				combat.Remove(pick);
			}

			FloorInfo info = FloorDefs.Get(floor);
			var assigned = new HashSet<int>();
			foreach (RoomPlan r in Rooms) {
				r.Seed = rng.Next();
				// No two connected rooms share a layout.
				var neighbours = r.Links.Where(l => l >= 0 && assigned.Contains(l)).Select(l => Rooms[l].Arch).ToHashSet();
				var options = info.Archetypes.Where(a => !neighbours.Contains(a.type)).Select(a => (a.type, a.weight)).ToList();
				if (options.Count == 0)
					options = info.Archetypes.Select(a => (a.type, a.weight)).ToList();
				assigned.Add(r.Id);
				r.Arch = r.Kind switch {
					RoomKind.Start => Archetype.Start,
					RoomKind.Boss => info.BossArch,
					RoomKind.Forge => Archetype.Forge,
					RoomKind.Treasure => Archetype.Vault,
					RoomKind.Shrine => Archetype.Shrine,
					RoomKind.Merchant => Archetype.Merchant,
					_ => Weighted(options, rng)
				};
			}
		}

		// ------------------------------------------------------------------ world placement

		private static (int top, int bottom) BandRange(Band band)
		{
			int h = Main.maxTilesY;
			int surface = (int)Main.worldSurface;
			return band switch {
				Band.Sky => (55, surface - 30),
				Band.Underworld => (h - 200, h - 45),
				_ => (surface + 50, h - 230)
			};
		}

		private void PlaceInWorld(Random rng, FloorInfo info)
		{
			var placed = new List<Rectangle>();
			Rectangle lobby = TerralayerWorld.Instance.LobbyExclusion;

			// Biggest rooms first so they get the best spots.
			foreach (RoomPlan r in Rooms.OrderByDescending(r => r.Kind == RoomKind.Boss ? 1 : 0)) {
				RoomArchetype arch = RoomArchetype.For(r.Arch);
				var sizeRng = new Random(r.Seed ^ 0x2F6B);
				(int w, int h) = arch.Size(sizeRng);
				r.Band = arch.PreferredBand(info, sizeRng);

				bool done = false;
				for (int shrink = 0; shrink < 4 && !done; shrink++) {
					foreach (Band band in new[] { r.Band, Band.Underground, Band.Sky, Band.Underworld }.Distinct()) {
						(int top, int bottom) = BandRange(band);
						int bw = w + Shell * 2;
						int bh = h + Shell * 2;
						if (bottom - top < bh)
							continue;

						for (int attempt = 0; attempt < 300; attempt++) {
							int x = rng.Next(60, Main.maxTilesX - 60 - bw);
							int y = rng.Next(top, bottom - bh);
							var bounds = new Rectangle(x, y, bw, bh);
							var padded = new Rectangle(x - GapX, y - GapY, bw + GapX * 2, bh + GapY * 2);
							if (padded.Intersects(lobby) || padded.Intersects(Secrets.OceanArena.Exclusion) || placed.Any(p => p.Intersects(padded)))
								continue;

							r.Band = band;
							r.Bounds = bounds;
							r.Interior = new Rectangle(x + Shell, y + Shell, w, h);
							placed.Add(bounds);
							done = true;
							break;
						}
						if (done)
							break;
					}
					w = (int)(w * 0.85f);
					h = (int)(h * 0.85f);
				}

				if (!done)
					throw new InvalidOperationException($"Terralayer: could not fit room {r.Id} ({r.Arch}) into this world. Try a larger world size.");
			}
		}

		// ------------------------------------------------------------------ rifts

		private void CreateRifts()
		{
			var riftAt = new Dictionary<(int room, Dir side), RiftPlan>();
			foreach (RoomPlan r in Rooms) {
				for (int d = 0; d < 4; d++) {
					if (r.Links[d] < 0)
						continue;
					var rift = new RiftPlan { Id = Rifts.Count, Room = r.Id, Pos = r.Anchors[d], Kind = RiftKind.Link, Side = (Dir)d };
					Rifts.Add(rift);
					riftAt[(r.Id, (Dir)d)] = rift;
				}
			}
			foreach (RiftPlan rift in Rifts.ToList()) {
				int other = Rooms[rift.Room].Links[(int)rift.Side];
				rift.Target = riftAt[(other, Opposite(rift.Side))].Id;
			}

			RoomPlan start = Rooms[StartRoom];
			Rifts.Add(new RiftPlan { Id = Rifts.Count, Room = start.Id, Pos = start.Home, Kind = RiftKind.Retreat });

			RoomPlan boss = Rooms[BossRoom];
			Rifts.Add(new RiftPlan { Id = Rifts.Count, Room = boss.Id, Pos = boss.ExitA, Kind = RiftKind.ReturnHome });
			Rifts.Add(new RiftPlan { Id = Rifts.Count, Room = boss.Id, Pos = boss.ExitB, Kind = RiftKind.Descend });
			foreach (Point p in boss.ExtraExits) {
				Rifts.Add(new RiftPlan { Id = Rifts.Count, Room = boss.Id, Pos = p, Kind = RiftKind.ReturnHome });
				Rifts.Add(new RiftPlan { Id = Rifts.Count, Room = boss.Id, Pos = new Point(p.X + 8, p.Y), Kind = RiftKind.Descend });
			}
		}

		/// <summary>Where a player arriving through a rift should be placed (pixels, top-left of the player hitbox).</summary>
		public static Vector2 StandPosition(Point riftPos, Player player) =>
			new(riftPos.X * 16 + 8 - player.width / 2f, riftPos.Y * 16 - player.height - 1);
	}
}
