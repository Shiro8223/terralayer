using System;
using Microsoft.Xna.Framework;
using Terralayer.Common.World;
using Terraria;
using Terraria.ID;

namespace Terralayer.Common.Depths.Rooms
{
	/// <summary>How a guardian's arena is dressed: floor, the rock its ceiling and crags are made of, walls, light.</summary>
	public record ArenaTheme(string Name, ushort Floor, ushort Rock, ushort Accent, ushort Wall, ushort Wall2, int Torch, ushort Grass = 0, int Platform = -1);

	public static class ArenaThemes
	{
		/// <summary>
		/// The arena matches its guardian. Some are also what the boss needs: Plantera fights in jungle mud and grass,
		/// Golem in front of lihzahrd brick walls.
		/// </summary>
		public static ArenaTheme For(int guardian, FloorInfo f) => guardian switch {
			NPCID.KingSlime => new("Royal Slime Pit", TileID.SlimeBlock, TileID.Stone, TileID.SlimeBlock, WallID.Slime, WallID.CaveUnsafe, 1, Platform: 20),
			NPCID.EyeofCthulhu => new("The Watching Hollow", TileID.Crimstone, TileID.Crimstone, TileID.FleshBlock, WallID.CrimstoneUnsafe, WallID.Flesh, 19, Platform: 34),
			NPCID.QueenBee => new("The Royal Hive", TileID.Hive, TileID.Hive, TileID.Hive, WallID.HiveUnsafe, WallID.Hive, 6, Platform: 24),
			NPCID.Deerclops => new("The Frozen Den", TileID.SnowBlock, TileID.SnowBlock, TileID.IceBlock, WallID.SnowWallUnsafe, WallID.IceUnsafe, 9, Platform: 35),
			NPCID.SkeletronHead => new("The Bone Cathedral", TileID.BlueDungeonBrick, TileID.BlueDungeonBrick, TileID.BoneBlock, WallID.BlueDungeonUnsafe, WallID.BlueDungeonSlabUnsafe, 13, Platform: 4),
			NPCID.TheDestroyer or NPCID.SkeletronPrime or NPCID.Retinazer =>
				new("The Iron Foundry", TileID.TinPlating, TileID.CopperPlating, TileID.MartianConduitPlating, WallID.TinPlating, WallID.MartianConduit, 12, Platform: 26),
			NPCID.QueenSlimeBoss => new("The Crystal Court", TileID.Pearlstone, TileID.Pearlstone, TileID.CrystalBlock, WallID.PearlstoneBrickUnsafe, WallID.Crystal, 20, Platform: 30),
			NPCID.Plantera => new("The Overgrowth", TileID.Mud, TileID.Mud, TileID.Mud, WallID.JungleUnsafe, WallID.LivingLeaf, 21, TileID.JungleGrass, 23),
			NPCID.Golem => new("The Sun Altar", TileID.LihzahrdBrick, TileID.LihzahrdBrick, TileID.LihzahrdBrick, WallID.LihzahrdBrickUnsafe, WallID.LihzahrdBrickUnsafe, 10, Platform: 33),
			NPCID.HallowBoss => new("The Prism Sanctum", TileID.PearlstoneBrick, TileID.Pearlstone, TileID.CrystalBlock, WallID.PearlstoneBrick, WallID.HallowUnsafe1, 20, Platform: 30),
			_ => new("Arena", f.ArenaFloor, f.ArenaFloor, f.Accent, f.ArenaWall, f.ArenaWall, f.TorchStyle, f.ArenaFloor == TileID.Mud ? TileID.JungleGrass : (ushort)0),
		};
	}

	// =====================================================================================================
	// Arena: the guardian's chamber, built for the guardian waiting in it. Very wide and tall (you may not have
	// any mobility gear, so there's room to run), with a flat floor and three tiers of long platforms; the ceiling
	// and walls are rough rock hung with stalactites and chains.
	// =====================================================================================================
	public class ArenaRoom : RoomArchetype
	{
		public override (int w, int h) Size(Random rng) => (Rand(rng, 240, 280), Rand(rng, 100, 118));

		public override void Layout(RoomPlan r, Random rng)
		{
			StandardAnchors(r, r.FloorY - 48);
			r.Feature = new Point(r.Interior.Center.X, r.FloorY);
		}

		/// <summary>How far the rough ceiling hangs down at column <paramref name="x"/> (never into the fighting space).</summary>
		private static int CeilingDepth(RoomPlan r, int x)
		{
			double phase = (r.Seed & 1023) * 0.01;
			double d = 7 + 3.5 * Math.Sin(x * 0.12 + phase) + 2.5 * Math.Sin(x * 0.047 + phase * 2);
			return Math.Clamp((int)d, 3, 13);
		}

		public override void Build(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			int floor = r.FloorY;
			int cx = I.Center.X;
			ArenaTheme th = ArenaThemes.For(r.Guardian, f);
			int ps = th.Platform >= 0 ? th.Platform : f.PlatformStyle;

			// A flat floor, three tiles deep, in the arena's material.
			for (int x = I.X - 2; x <= I.Right + 1; x++)
				for (int y = floor; y < floor + 3; y++)
					Gen.Solid(x, y, th.Floor);

			// Walls, with darker blotches for depth.
			Gen.Wall(I, th.Wall);
			for (int i = 0; i < 16; i++) {
				int bx = Rand(rng, I.X, I.Right), by = Rand(rng, I.Y, floor - 4), rx = Rand(rng, 6, 16), ry = Rand(rng, 4, 10);
				for (int x = bx - rx; x <= bx + rx; x++)
					for (int y = by - ry; y <= by + ry; y++) {
						float dx = (x - bx) / (float)rx, dy = (y - by) / (float)ry;
						if (dx * dx + dy * dy <= 1f && I.Contains(x, y))
							Gen.WallAt(x, y, th.Wall2);
					}
			}

			// A rugged ceiling of rock, with stalactites hanging from it.
			int lowest = I.Y;
			for (int x = I.X; x < I.Right; x++) {
				int d = CeilingDepth(r, x) + (rng.Next(4) == 0 ? 1 : 0);
				Gen.Solid(new Rectangle(x, I.Y, 1, d), rng.Next(6) == 0 ? th.Accent : th.Rock);
				lowest = Math.Max(lowest, I.Y + d);
			}
			var ceilingClip = new Rectangle(I.X, I.Y, I.Width, 22);
			for (int x = I.X + 6; x < I.Right - 6; x += Rand(rng, 9, 16)) {
				int top = I.Y + CeilingDepth(r, x);
				Gen.Ellipse(x, top, Rand(rng, 1, 3), Rand(rng, 3, 7), true, rng.Next(3) == 0 ? th.Accent : th.Rock, rng, 0.2f, ceilingClip);
			}

			// Crags jutting from both walls, high enough to leave the floor and platforms clear.
			foreach (int side in new[] { I.X, I.Right - 1 })
				for (int y = I.Y + 24; y < floor - 26; y += Rand(rng, 8, 14))
					Gen.Ellipse(side, y, Rand(rng, 3, 6), Rand(rng, 3, 7), true, rng.Next(4) == 0 ? th.Accent : th.Rock, rng, 0.25f,
						new Rectangle(I.X, I.Y, I.Width, floor - 24 - I.Y));
			Gen.Smooth(new Rectangle(I.X, I.Y, I.Width, 24));

			if (th.Grass != 0)
				Gen.Grassify(new Rectangle(I.X - 2, I.Y - 3, I.Width + 4, I.Height + 6), th.Floor, th.Grass);

			// Long platform tiers for dodging, joined by ladders, and the central ledge for the upper rift.
			int seg = (int)(I.Width * 0.34f);
			for (int tier = 1; tier <= 2; tier++) {
				int y = floor - 16 * tier;
				Gen.Platform(I.X + 8, y, seg, ps);
				Gen.Platform(I.Right - 8 - seg, y, seg, ps);
			}
			Gen.Platform(cx - 40, floor - 48, 81, ps);
			Gen.PlatformLadder(I.X + 8 + seg - 6, floor - 32, floor, 6, ps);
			Gen.PlatformLadder(I.Right - 8 - seg, floor - 32, floor, 6, ps);
			UpLedge(cx, floor - 48, floor, ps);
		}

		public override void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			ArenaTheme th = ArenaThemes.For(r.Guardian, f);
			for (int y = r.FloorY - 4; y > I.Y + 16; y -= 12) {
				Gen.Torch(I.X + 1, y, th.Torch);
				Gen.Torch(I.Right - 2, y, th.Torch);
			}
			for (int x = I.X + 20; x < I.Right - 20; x += 26)
				Gen.Torch(x, r.FloorY - 18, th.Torch);
			// Chains hanging from the ceiling.
			for (int x = I.X + 14; x < I.Right - 14; x += Rand(rng, 18, 30)) {
				int top = I.Y + CeilingDepth(r, x) + 1;
				if (Gen.IsAir(x, top))
					Gen.Rope(x, top, top + Rand(rng, 4, 12), TileID.Chain);
			}
			Gen.PlaceOnFloor(I.X + 12, r.FloorY, TileID.Campfire);
			Gen.PlaceOnFloor(I.Right - 14, r.FloorY, TileID.Campfire);
		}
	}
}
