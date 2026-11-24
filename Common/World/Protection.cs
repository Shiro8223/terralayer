using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Common.World
{
	/// <summary>
	/// In a Terralayer world the terrain is fixed: nothing can be mined, bombed, hammered or built.
	/// Pots and "cuttable" scenery (grass, vines, cobwebs) can still be broken.
	/// </summary>
	public class ProtectedTiles : GlobalTile
	{
		private static bool Locked => TerralayerWorld.Active && !Gen.Editing && !WorldGen.gen;

		private static bool Breakable(int type) => type == TileID.Pots || Main.tileCut[type];

		public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged) => !Locked || Breakable(type);

		public override bool CanExplode(int i, int j, int type) => !Locked || Breakable(type);

		public override bool CanPlace(int i, int j, int type) => !Locked;

		public override bool CanReplace(int i, int j, int type, int tileTypeBeingPlaced) => !Locked;

		public override bool Slope(int i, int j, int type) => !Locked;
	}

	public class ProtectedWalls : GlobalWall
	{
		private static bool Locked => TerralayerWorld.Active && !Gen.Editing && !WorldGen.gen;

		public override bool CanExplode(int i, int j, int type) => !Locked;

		public override bool CanPlace(int i, int j, int type) => !Locked;

		public override void KillWall(int i, int j, int type, ref bool fail)
		{
			if (Locked)
				fail = true;
		}
	}
}
