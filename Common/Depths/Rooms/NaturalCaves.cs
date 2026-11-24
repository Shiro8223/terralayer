using System;
using Microsoft.Xna.Framework;
using Terralayer.Common.World;
using Terraria;
using Terraria.ID;

namespace Terralayer.Common.Depths.Rooms
{
	// =====================================================================================================
	// Natural caves: the cavern layout (organic chambers, tunnels, a rope up to a hidden grotto) carved out of
	// other materials, each with the monsters that live in that kind of cave.
	// =====================================================================================================

	/// <summary>Glowing mushroom grass on mud, mushrooms everywhere. Fungus creatures.</summary>
	public class MushroomCaveRoom : CavernRoom
	{
		protected override int CavePlatform(FloorInfo f) => 18;
		protected override ushort CaveRock(FloorInfo f) => TileID.Mud;
		protected override ushort CaveWall(FloorInfo f) => WallID.MushroomUnsafe;
		protected override ushort CaveGrass(FloorInfo f) => TileID.MushroomGrass;
		protected override int CaveTorch(FloorInfo f) => 22; // mushroom torches

		public override int[] Enemies(int floor) => new int[] { NPCID.AnomuraFungus, NPCID.AnomuraFungus, NPCID.MushiLadybug, NPCID.FungiBulb };

		protected override void CaveDecor(RoomPlan r, FloorInfo f, Random rng)
		{
			Rectangle I = r.Interior;
			for (int x = I.X + 1; x < I.Right - 1; x++)
				for (int y = I.Y + 1; y < I.Bottom; y++) {
					Tile t = Main.tile[x, y];
					if (t.HasTile && t.TileType == TileID.MushroomGrass && Gen.IsAir(x, y - 1) && rng.NextDouble() < 0.35)
						Gen.Place(x, y - 1, TileID.MushroomPlants, rng.Next(5));
				}
		}
	}

	/// <summary>Snow caverns veined with ice (never on the surfaces you walk on). Ice slimes, bats and undead vikings.</summary>
	public class IceCaveRoom : CavernRoom
	{
		protected override int CavePlatform(FloorInfo f) => 35;
		protected override ushort CaveRock(FloorInfo f) => TileID.SnowBlock;
		protected override ushort CaveWall(FloorInfo f) => WallID.SnowWallUnsafe;
		protected override ushort CaveGrass(FloorInfo f) => 0;
		protected override int CaveTorch(FloorInfo f) => 9; // ice torches

		public override int[] Enemies(int floor) => new int[] { NPCID.IceSlime, NPCID.IceBat, NPCID.UndeadViking, NPCID.CyanBeetle };

		protected override void CaveExtras(RoomPlan r, FloorInfo f, Random rng)
		{
			Veins(r, f, rng, TileID.IceBlock, 14);
			Gen.Wall(new Rectangle(r.Interior.X, r.Interior.Y, r.Interior.Width, r.Interior.Height / 3), WallID.IceUnsafe);
		}
	}

	/// <summary>Sandstone caverns banded with hardened sand. Antlions and sand slimes.</summary>
	public class DesertCaveRoom : CavernRoom
	{
		protected override int CavePlatform(FloorInfo f) => 25;
		protected override ushort CaveRock(FloorInfo f) => TileID.Sandstone;
		protected override ushort CaveWall(FloorInfo f) => WallID.Sandstone;
		protected override ushort CaveGrass(FloorInfo f) => 0;
		protected override int CaveTorch(FloorInfo f) => 16; // desert torches

		public override int[] Enemies(int floor) => new int[] { NPCID.WalkingAntlion, NPCID.FlyingAntlion, NPCID.SandSlime };

		protected override void CaveExtras(RoomPlan r, FloorInfo f, Random rng) => Veins(r, f, rng, TileID.HardenedSand, 18);
	}

	/// <summary>Pearlstone caverns shot through with crystal. Illuminant creatures and chaos elementals.</summary>
	public class CrystalCaveRoom : CavernRoom
	{
		protected override int CavePlatform(FloorInfo f) => 30;
		protected override ushort CaveRock(FloorInfo f) => TileID.Pearlstone;
		protected override ushort CaveWall(FloorInfo f) => WallID.HallowUnsafe1;
		protected override ushort CaveGrass(FloorInfo f) => 0;
		protected override int CaveTorch(FloorInfo f) => 20; // hallowed torches

		public override int[] Enemies(int floor) => new int[] { NPCID.IlluminantBat, NPCID.IlluminantSlime, NPCID.ChaosElemental };

		protected override void CaveExtras(RoomPlan r, FloorInfo f, Random rng) => Veins(r, f, rng, TileID.CrystalBlock, 16);
	}

	/// <summary>A natural marble cave. Hoplites patrol it.</summary>
	public class MarbleCaveRoom : CavernRoom
	{
		protected override int CavePlatform(FloorInfo f) => 29;
		protected override ushort CaveRock(FloorInfo f) => TileID.Marble;
		protected override ushort CaveWall(FloorInfo f) => WallID.Marble;
		protected override ushort CaveGrass(FloorInfo f) => 0;

		public override int[] Enemies(int floor) => new int[] { NPCID.GreekSkeleton, NPCID.GreekSkeleton, NPCID.Skeleton, NPCID.CaveBat };
	}

	/// <summary>A natural granite cave. Granite golems and elementals.</summary>
	public class GraniteGrottoRoom : CavernRoom
	{
		protected override int CavePlatform(FloorInfo f) => 28;
		protected override ushort CaveRock(FloorInfo f) => TileID.Granite;
		protected override ushort CaveWall(FloorInfo f) => WallID.GraniteUnsafe;
		protected override ushort CaveGrass(FloorInfo f) => 0;
		protected override int CaveTorch(FloorInfo f) => 1; // blue

		public override int[] Enemies(int floor) => new int[] { NPCID.GraniteGolem, NPCID.GraniteFlyer, NPCID.GraniteFlyer };
	}
}
