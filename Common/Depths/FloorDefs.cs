using Terraria.ID;

namespace Terralayer.Common.Depths
{
	/// <summary>Which vertical slice of the world a room is carved into. Each has its own background, light and music.</summary>
	public enum Band
	{
		Sky,         // real sky behind the room; space near the very top (low gravity)
		Underground,
		Underworld   // hell's background glow and music
	}

	public enum Archetype
	{
		Start, Hall, Cavern, Shaft, Flooded, LavaForge, SkyGarden, Trap, Workshop, Vault, Shrine, Merchant, Arena, HellBridge, Forge,
		// Themed combat halls with their own monsters
		HopliteBarracks, GraniteCave, Ossuary, Library, Hive, SpiderNest, ImpDen, UnicornMeadow, LihzahrdSanctum, SlimePit,
		// Natural caves in other materials
		MushroomCave, IceCave, DesertCave, CrystalCave, MarbleCave, GraniteGrotto
	}

	public record FloorInfo(
		string Name,
		string Flavor,
		Band Band,
		ushort Brick, ushort BrickWall,
		ushort Rock, ushort RockWall,
		ushort Accent,
		ushort Grass,          // grass grown on exposed rock (0 = none)
		int PlatformStyle, int TorchStyle, int ChandelierStyle, int PotBiome, int TrapStyle,
		(Archetype type, int weight)[] Archetypes,
		int[] Enemies,
		int[][] Guardians,
		float EnemyLife, float EnemyDamage, int EnemyValue,
		int RoomsMin, int RoomsMax,
		ushort ArenaFloor, ushort ArenaWall,
		Archetype BossArch = Archetype.Arena);

	public static class FloorDefs
	{
		private static readonly FloorInfo[] Floors = {
			new("Forgotten Halls", "Crumbling stone halls, flooded cellars and trapped corridors.",
				Band.Underground,
				TileID.GrayBrick, WallID.GrayBrick, TileID.Stone, WallID.Stone, TileID.StoneSlab, TileID.GreenMoss,
				28, 0, 0, 0, 0,
				new[] { (Archetype.Hall, 3), (Archetype.Cavern, 3), (Archetype.Shaft, 2), (Archetype.Trap, 1), (Archetype.HopliteBarracks, 2),
					(Archetype.GraniteCave, 2), (Archetype.Ossuary, 2), (Archetype.Library, 1), (Archetype.SlimePit, 1),
					(Archetype.MarbleCave, 2), (Archetype.GraniteGrotto, 1), (Archetype.IceCave, 2), (Archetype.DesertCave, 2), (Archetype.MushroomCave, 1) },
				new int[] { NPCID.Skeleton, NPCID.AngryBones, NPCID.CaveBat, NPCID.BlueSlime, NPCID.UndeadMiner, NPCID.CursedSkull, NPCID.GreekSkeleton },
				new[] { new int[] { NPCID.KingSlime }, new int[] { NPCID.EyeofCthulhu } },
				1.0f, 1.0f, 300, 10, 12,
				TileID.GrayBrick, WallID.GrayBrick),

			new("Verdant Depths", "Overgrown caverns, hidden pools and gardens open to the sky.",
				Band.Underground,
				TileID.RichMahogany, WallID.RichMaogany, TileID.Mud, WallID.MudUnsafe, TileID.LivingWood, TileID.JungleGrass,
				2, 3, 0, 2, 3,
				new[] { (Archetype.Cavern, 4), (Archetype.Hall, 2), (Archetype.Shaft, 2), (Archetype.SkyGarden, 2), (Archetype.Trap, 1),
					(Archetype.Hive, 2), (Archetype.SpiderNest, 2), (Archetype.GraniteCave, 1),
					(Archetype.MushroomCave, 3), (Archetype.GraniteGrotto, 1), (Archetype.DesertCave, 1), (Archetype.IceCave, 1) },
				new int[] { NPCID.Hornet, NPCID.JungleBat, NPCID.SpikedJungleSlime, NPCID.JungleCreeper, NPCID.Salamander, NPCID.DoctorBones },
				new[] { new int[] { NPCID.QueenBee }, new int[] { NPCID.Deerclops }, new int[] { NPCID.SkeletronHead } },
				1.2f, 1.15f, 800, 12, 14,
				TileID.Mud, WallID.JungleUnsafe),

			new("Cinder Keep", "A fortress above the underworld, all lava channels and forges.",
				Band.Underworld,
				TileID.ObsidianBrick, WallID.ObsidianBrick, TileID.Ash, WallID.ObsidianBrick, TileID.HellstoneBrick, 0,
				13, 7, 0, 4, 2,
				new[] { (Archetype.LavaForge, 4), (Archetype.Hall, 2), (Archetype.Shaft, 2), (Archetype.Trap, 2), (Archetype.Workshop, 1), (Archetype.Cavern, 1),
					(Archetype.ImpDen, 3) },
				new int[] { NPCID.FireImp, NPCID.Hellbat, NPCID.Demon, NPCID.BoneSerpentHead, NPCID.Lavabat, NPCID.RedDevil },
				new[] { new int[] { NPCID.WallofFlesh } },
				0.8f, 0.9f, 2000, 13, 15,
				TileID.ObsidianBrick, WallID.ObsidianBrick, Archetype.HellBridge),

			new("Prism Spire", "Crystal towers and cloud gardens, up where the air runs thin.",
				Band.Sky,
				TileID.PearlstoneBrick, WallID.PearlstoneBrick, TileID.Sunplate, WallID.DiscWall, TileID.CrystalBlock, 0,
				22, 5, 0, 0, 1,
				new[] { (Archetype.SkyGarden, 4), (Archetype.Hall, 2), (Archetype.Shaft, 2), (Archetype.Workshop, 2), (Archetype.Cavern, 1),
					(Archetype.UnicornMeadow, 3), (Archetype.SlimePit, 1), (Archetype.CrystalCave, 3) },
				new int[] { NPCID.IlluminantBat, NPCID.ChaosElemental, NPCID.Pixie, NPCID.Gastropod, NPCID.Unicorn, NPCID.EnchantedSword },
				new[] { new int[] { NPCID.TheDestroyer }, new int[] { NPCID.SkeletronPrime }, new int[] { NPCID.Retinazer, NPCID.Spazmatism }, new int[] { NPCID.QueenSlimeBoss } },
				1.0f, 1.0f, 4000, 14, 16,
				TileID.PearlstoneBrick, WallID.PearlstoneBrick),

			new("Sunken Temple", "The deepest ruins: spear traps, drowned halls and the jungle's heart.",
				Band.Underground,
				TileID.LihzahrdBrick, WallID.LihzahrdBrickUnsafe, TileID.Mud, WallID.JungleUnsafe, TileID.Mudstone, TileID.JungleGrass,
				33, 3, 0, 2, 4,
				new[] { (Archetype.Trap, 3), (Archetype.Hall, 2), (Archetype.Cavern, 2), (Archetype.Shaft, 2), (Archetype.Workshop, 1),
					(Archetype.LihzahrdSanctum, 3), (Archetype.Hive, 1), (Archetype.SpiderNest, 1), (Archetype.MushroomCave, 2) },
				new int[] { NPCID.Lihzahrd, NPCID.FlyingSnake, NPCID.GiantTortoise, NPCID.Derpling, NPCID.JungleCreeper, NPCID.GiantFlyingFox },
				new[] { new int[] { NPCID.Plantera }, new int[] { NPCID.Golem }, new int[] { NPCID.HallowBoss } },
				1.0f, 1.0f, 7000, 15, 18,
				TileID.Mud, WallID.LihzahrdBrickUnsafe),
		};

		public static int Count => Floors.Length;

		public static FloorInfo Get(int floor) => Floors[System.Math.Clamp(floor, 1, Floors.Length) - 1];
	}
}
