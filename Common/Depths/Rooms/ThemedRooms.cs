using System;
using Microsoft.Xna.Framework;
using Terralayer.Common.World;
using Terraria;
using Terraria.ID;

namespace Terralayer.Common.Depths.Rooms
{
	/// <summary>
	/// A combat hall built around one kind of monster: its own floor, walls and trim, balconies to fight across, and
	/// its own enemy list instead of the floor's. Every one keeps a flat floor and the standard rift spots, so all
	/// four rifts stay reachable.
	/// </summary>
	public abstract class ThemedHall : RoomArchetype
	{
		protected abstract (int minW, int maxW, int minH, int maxH) Dims { get; }
		protected abstract ushort FloorTile { get; }
		protected virtual ushort SubFloorTile => FloorTile;
		protected virtual ushort Trim => FloorTile;
		protected abstract ushort WallMain { get; }
		protected virtual ushort WallStripe => WallMain;
		protected virtual int Tiers => 2;
		protected virtual float BalconyShare => 0.26f;
		protected virtual int Torch(FloorInfo f) => f.TorchStyle;
		/// <summary>Platform style matching the room's materials.</summary>
		protected virtual int Platforms(FloorInfo f) => f.PlatformStyle;

		public override (int w, int h) Size(Random rng) => (Rand(rng, Dims.minW, Dims.maxW), Rand(rng, Dims.minH, Dims.maxH));

		public override void Layout(RoomPlan r, Random rng) => StandardAnchors(r, r.FloorY - 14);

		public override void Build(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			int floor = r.FloorY;
			int ps = Platforms(f);

			// Floor, ceiling and side frame in the room's own materials.
			for (int x = I.X - 2; x <= I.Right + 1; x++) {
				Gen.Solid(x, floor, FloorTile);
				Gen.Solid(x, floor + 1, SubFloorTile);
				Gen.Solid(x, floor + 2, SubFloorTile);
				Gen.Solid(x, I.Y - 1, Trim);
				Gen.Solid(x, I.Y - 2, Trim);
			}
			for (int y = I.Y - 2; y < floor; y++)
				foreach (int x in new[] { I.X - 2, I.X - 1, I.Right, I.Right + 1 })
					Gen.Solid(x, y, Trim);
			Gen.Wall(I, WallMain);
			if (WallStripe != WallMain)
				for (int x = I.X + 5; x < I.Right - 5; x += 11)
					Gen.Wall(new Rectangle(x, I.Y, 2, I.Height), WallStripe);

			UpLedge(I.Center.X, floor - 14, floor, ps);

			// Balconies on both sides, one per tier, joined by ladders at their inner ends.
			int balcony = (int)(I.Width * BalconyShare);
			int top = floor;
			for (int k = 1; k <= Tiers; k++) {
				int y = floor - 12 * k;
				if (y < I.Y + 7)
					break;
				Gen.Platform(I.X, y, balcony, ps);
				Gen.Platform(I.Right - balcony, y, balcony, ps);
				top = y;
			}
			if (top < floor) {
				Gen.PlatformLadder(I.X + balcony - 6, top, floor, 6, ps);
				Gen.PlatformLadder(I.Right - balcony, top, floor, 6, ps);
			}
			Roughen(r, rng, Trim, FloorTile, SubFloorTile);
			BuildExtras(r, f, rng);
		}

		protected virtual void BuildExtras(RoomPlan r, FloorInfo f, Random rng) { }

		public override void Decorate(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			for (int y = I.Bottom - 5; y > I.Y + 2; y -= 11) {
				Gen.Torch(I.X, y, Torch(f));
				Gen.Torch(I.Right - 1, y, Torch(f));
			}
			for (int x = I.X + 10; x < I.Right - 10; x += 20)
				Gen.Torch(x, I.Y + 1, Torch(f));
			DecorateExtras(r, f, rng);
			ScatterPots(r, f, rng, 3);
		}

		protected virtual void DecorateExtras(RoomPlan r, FloorInfo f, Random rng) { }

		/// <summary>Hangs blobs of <paramref name="type"/> from the ceiling, never lower than <paramref name="maxDepth"/> tiles.</summary>
		protected static void Hanging(RoomPlan r, Random rng, ushort type, int count, int maxDepth)
		{
			Rectangle I = r.Interior;
			for (int i = 0; i < count; i++) {
				int x = Rand(rng, I.X + 6, I.Right - 6);
				int ry = Rand(rng, 2, Math.Max(2, maxDepth - 1));
				Gen.Ellipse(x, I.Y, Rand(rng, 3, 7), ry, true, type, rng, 0.25f, new Rectangle(I.X, I.Y, I.Width, maxDepth));
			}
		}
	}

	// =====================================================================================================
	// Hoplite Barracks: a marble hall of balconies; the Hoplites hurl javelins down from them.
	// =====================================================================================================
	public class HopliteBarracksRoom : ThemedHall
	{
		protected override int Platforms(FloorInfo f) => 29;
		protected override (int, int, int, int) Dims => (130, 170, 50, 60);
		protected override ushort FloorTile => TileID.MarbleBlock;
		protected override ushort SubFloorTile => TileID.Marble;
		protected override ushort WallMain => WallID.MarbleBlock;
		protected override ushort WallStripe => WallID.Marble;

		public override int[] Enemies(int floor) => floor <= 1
			? new int[] { NPCID.GreekSkeleton, NPCID.GreekSkeleton, NPCID.GreekSkeleton, NPCID.Skeleton }
			: new int[] { NPCID.GreekSkeleton, NPCID.GreekSkeleton, NPCID.Skeleton };

		protected override void DecorateExtras(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			for (int x = I.X + 14; x < I.Right - 14; x += Rand(rng, 16, 24))
				if (ClearOfRifts(r, x))
					Rubble(x, r.FloorY, rng);
			for (int x = I.X + 8; x < I.Right - 8; x += 16)
				Gen.Place(x, I.Y, TileID.Banners, 0);
			Chandeliers(r, f, rng, 30);
		}
	}

	// =====================================================================================================
	// Granite Hollow: a low cave under a heavy granite ceiling, home to granite golems and elementals.
	// =====================================================================================================
	public class GraniteCaveRoom : ThemedHall
	{
		protected override int Platforms(FloorInfo f) => 28;
		protected override (int, int, int, int) Dims => (130, 170, 44, 52);
		protected override ushort FloorTile => TileID.Granite;
		protected override ushort Trim => TileID.Granite;
		protected override ushort WallMain => WallID.GraniteUnsafe;
		protected override ushort WallStripe => WallID.GraniteBlock;
		protected override int Tiers => 1;

		public override int[] Enemies(int floor) => new int[] { NPCID.GraniteGolem, NPCID.GraniteGolem, NPCID.GraniteFlyer };

		protected override void BuildExtras(RoomPlan r, FloorInfo f, Random rng)
		{
			// The ceiling comes down to 24 tiles above the floor, with knobbly stalactites (never below 20).
			Rectangle I = r.Interior;
			int ceiling = r.FloorY - 24;
			if (ceiling > I.Y)
				Gen.Solid(new Rectangle(I.X, I.Y, I.Width, ceiling - I.Y), TileID.Granite);
			var clip = new Rectangle(I.X, I.Y, I.Width, r.FloorY - 20 - I.Y);
			for (int x = I.X + 4; x < I.Right - 4; x += Rand(rng, 6, 11))
				Gen.Ellipse(x, ceiling, Rand(rng, 2, 5), Rand(rng, 2, 4), true, TileID.Granite, rng, 0.3f, clip);
			Gen.Smooth(new Rectangle(I.X, I.Y, I.Width, ceiling + 6 - I.Y));
		}
	}

	// =====================================================================================================
	// Ossuary: three stacked galleries of bone, lined with graves. Angry bones and cursed skulls.
	// =====================================================================================================
	public class OssuaryRoom : ThemedHall
	{
		protected override int Platforms(FloorInfo f) => 4;
		protected override (int, int, int, int) Dims => (130, 170, 50, 58);
		protected override ushort FloorTile => TileID.BoneBlock;
		protected override ushort Trim => TileID.BoneBlock;
		protected override ushort WallMain => WallID.Bone;
		protected override ushort WallStripe => WallID.BlueDungeonUnsafe;
		protected override int Tiers => 3;
		protected override float BalconyShare => 0.36f;
		protected override int Torch(FloorInfo f) => 13; // bone torches

		public override int[] Enemies(int floor) => new int[] { NPCID.AngryBones, NPCID.AngryBones, NPCID.CursedSkull, NPCID.Skeleton };

		protected override void DecorateExtras(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			for (int x = I.X + 8; x < I.Right - 8; x += Rand(rng, 6, 10))
				if (ClearOfRifts(r, x) && rng.NextDouble() < 0.6)
					Gen.PlaceOnFloor(x, r.FloorY, TileID.Tombstones, rng.Next(5));
			Chandeliers(r, f, rng, 24);
		}
	}

	// =====================================================================================================
	// Forbidden Library: tiers of shelves where dark casters teleport between the stacks.
	// =====================================================================================================
	public class LibraryRoom : ThemedHall
	{
		protected override int Platforms(FloorInfo f) => 12;
		protected override (int, int, int, int) Dims => (110, 150, 52, 62);
		protected override ushort FloorTile => TileID.BlueDungeonBrick;
		protected override ushort WallMain => WallID.BlueDungeonSlabUnsafe;
		protected override ushort WallStripe => WallID.Wood;
		protected override int Tiers => 3;
		protected override float BalconyShare => 0.34f;

		public override int[] Enemies(int floor) => new int[] { NPCID.DarkCaster, NPCID.DarkCaster, NPCID.CursedSkull };

		protected override void DecorateExtras(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			int balcony = (int)(I.Width * BalconyShare);
			// Bookcases along the floor and on every balcony.
			for (int x = I.X + 4; x < I.Right - 6; x += Rand(rng, 6, 9))
				if (ClearOfRifts(r, x, 5))
					Gen.PlaceOnFloor(x, r.FloorY, TileID.Bookcases, rng.Next(3));
			for (int k = 1; k <= Tiers; k++) {
				int y = r.FloorY - 12 * k;
				if (y < I.Y + 7)
					break;
				for (int x = I.X + 2; x < I.X + balcony - 8; x += 6)
					Gen.PlaceOnFloor(x, y, TileID.Bookcases, rng.Next(3));
				for (int x = I.Right - balcony + 8; x < I.Right - 3; x += 6)
					Gen.PlaceOnFloor(x, y, TileID.Bookcases, rng.Next(3));
			}
			Chandeliers(r, f, rng, 28);
		}
	}

	// =====================================================================================================
	// Hive: honeycomb walls and dripping combs. Hornets, and bees.
	// =====================================================================================================
	public class HiveRoom : ThemedHall
	{
		protected override int Platforms(FloorInfo f) => 24;
		protected override (int, int, int, int) Dims => (130, 170, 50, 60);
		protected override ushort FloorTile => TileID.Hive;
		protected override ushort WallMain => WallID.HiveUnsafe;
		protected override ushort WallStripe => WallID.Hive;
		protected override int Torch(FloorInfo f) => 6; // yellow

		public override int[] Enemies(int floor) => floor <= 2
			? new int[] { NPCID.Hornet, NPCID.HornetHoney, NPCID.HornetLeafy, NPCID.HornetFatty, NPCID.Bee }
			: new int[] { NPCID.MossHornet, NPCID.HornetLeafy, NPCID.Hornet };

		protected override void BuildExtras(RoomPlan r, FloorInfo f, Random rng) => Hanging(r, rng, TileID.Hive, 9, 6);
	}

	// =====================================================================================================
	// Spider Nest: webbed corners and spider-silk walls the creepers scuttle along.
	// =====================================================================================================
	public class SpiderNestRoom : ThemedHall
	{
		protected override int Platforms(FloorInfo f) => 36;
		protected override (int, int, int, int) Dims => (120, 160, 50, 60);
		protected override ushort FloorTile => TileID.Stone;
		protected override ushort WallMain => WallID.SpiderUnsafe;
		protected override ushort WallStripe => WallID.CaveUnsafe;

		public override int[] Enemies(int floor) => floor <= 2
			? new int[] { NPCID.WallCreeper, NPCID.WallCreeper, NPCID.JungleCreeper }
			: new int[] { NPCID.BlackRecluse, NPCID.JungleCreeper };

		protected override void BuildExtras(RoomPlan r, FloorInfo f, Random rng)
		{
			// Webs only up under the ceiling, never across the floor.
			Rectangle I = r.Interior;
			foreach (int x in new[] { I.X + 4, I.Right - 5, I.X + I.Width / 3, I.Right - I.Width / 3 })
				Gen.Ellipse(x, I.Y + 2, Rand(rng, 4, 7), 3, true, TileID.Cobweb, rng, 0.4f, new Rectangle(I.X, I.Y, I.Width, 7));
		}
	}

	// =====================================================================================================
	// Imp Den: an obsidian chapel to a demon altar, guarded by fire imps and demons.
	// =====================================================================================================
	public class ImpDenRoom : ThemedHall
	{
		protected override int Platforms(FloorInfo f) => 13;
		protected override (int, int, int, int) Dims => (130, 170, 50, 60);
		protected override ushort FloorTile => TileID.ObsidianBrick;
		protected override ushort Trim => TileID.HellstoneBrick;
		protected override ushort WallMain => WallID.ObsidianBrick;
		protected override ushort WallStripe => WallID.HellstoneBrick;
		protected override int Torch(FloorInfo f) => 7; // demon torches

		public override int[] Enemies(int floor) => new int[] { NPCID.FireImp, NPCID.FireImp, NPCID.Demon, NPCID.Hellbat };

		protected override void DecorateExtras(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			Gen.PlaceOnFloor(I.Center.X - 26, r.FloorY, TileID.DemonAltar);
			Gen.PlaceOnFloor(I.X + I.Width / 4, r.FloorY, TileID.Hellforge);
			Gen.PlaceOnFloor(I.Right - I.Width / 4, r.FloorY, TileID.Hellforge);
		}
	}

	// =====================================================================================================
	// Unicorn Meadow: open hallowed grassland under the sky, long enough for unicorns to charge.
	// =====================================================================================================
	public class UnicornMeadowRoom : ThemedHall
	{
		protected override int Platforms(FloorInfo f) => 30;
		protected override (int, int, int, int) Dims => (180, 220, 50, 58);
		protected override ushort FloorTile => TileID.HallowedGrass;
		protected override ushort SubFloorTile => TileID.Dirt;
		protected override ushort Trim => TileID.Pearlstone;
		protected override ushort WallMain => 0; // the real sky shows through
		protected override int Tiers => 1;
		protected override float BalconyShare => 0.18f;

		public override int[] Enemies(int floor) => new int[] { NPCID.Unicorn, NPCID.Unicorn, NPCID.Pixie, NPCID.Pixie, NPCID.Pixie };

		protected override void DecorateExtras(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			foreach (int x in new[] { I.X + 22, I.Center.X - 32, I.Center.X + 34, I.Right - 22 })
				if (ClearOfRifts(r, x, 6))
					WorldGen.GrowTree(x, r.FloorY);
			for (int x = I.X + 2; x < I.Right - 2; x++)
				if (rng.NextDouble() < 0.4 && Gen.IsAir(x, r.FloorY - 1))
					Gen.Place(x, r.FloorY - 1, TileID.HallowedPlants, rng.Next(10));
		}
	}

	// =====================================================================================================
	// Lihzahrd Sanctum: temple brick, and spear traps waiting under pressure plates by the walls.
	// =====================================================================================================
	public class LihzahrdSanctumRoom : ThemedHall
	{
		protected override int Platforms(FloorInfo f) => 33;
		protected override (int, int, int, int) Dims => (140, 180, 50, 60);
		protected override ushort FloorTile => TileID.LihzahrdBrick;
		protected override ushort WallMain => WallID.LihzahrdBrickUnsafe;

		public override int[] Enemies(int floor) => new int[] { NPCID.Lihzahrd, NPCID.Lihzahrd, NPCID.FlyingSnake };

		protected override void BuildExtras(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			Traps.Armed(I.X + 12, r.FloorY, I.X - 1, r.FloorY - 2, faceRight: true, f.TrapStyle);
			Traps.Armed(I.Right - 13, r.FloorY, I.Right, r.FloorY - 2, faceRight: false, f.TrapStyle);
		}

		protected override void DecorateExtras(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			for (int x = I.X + 18; x < I.Right - 18; x += Rand(rng, 18, 26))
				if (ClearOfRifts(r, x))
					Rubble(x, r.FloorY, rng);
		}
	}

	// =====================================================================================================
	// Slime Pit: a soft gel floor with bouncy pads up to the balconies. Slimes of every kind.
	// =====================================================================================================
	public class SlimePitRoom : ThemedHall
	{
		protected override int Platforms(FloorInfo f) => 20;
		protected override (int, int, int, int) Dims => (130, 170, 46, 54);
		protected override ushort FloorTile => TileID.SlimeBlock;
		protected override ushort SubFloorTile => TileID.Dirt;
		protected override ushort Trim => TileID.Stone;
		protected override ushort WallMain => WallID.Slime;
		protected override ushort WallStripe => WallID.CaveUnsafe;

		public override int[] Enemies(int floor) => floor <= 2
			? new int[] { NPCID.BlueSlime, NPCID.MotherSlime, NPCID.DungeonSlime, NPCID.SpikedIceSlime }
			: new int[] { NPCID.IlluminantSlime, NPCID.ToxicSludge };

		protected override void BuildExtras(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			foreach (int x in new[] { I.X + 18, I.Center.X - 30, I.Center.X + 32, I.Right - 21 })
				if (ClearOfRifts(r, x, 5))
					Gen.Solid(new Rectangle(x, r.FloorY, 3, 1), TileID.PinkSlimeBlock);
		}
	}
}
